using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services
{
    public class MenuSyncService : BaseService, IMenuSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISyncConfigurationService _syncConfigurationService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MenuSyncService> _logger;

        public MenuSyncService(
            ApplicationDbContext context,
            ISyncConfigurationService syncConfigurationService,
            IHttpClientFactory httpClientFactory,
            ILogger<MenuSyncService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
            _context = context;
            _syncConfigurationService = syncConfigurationService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<MenuSyncResult> SyncMenuAsync(int organizationId)
        {
            try
            {
                // Authorization check: Only SuperAdmin or users from the same organization can trigger sync
                var (userOrgId, isSuperAdmin) = GetUserContext();
                if (!isSuperAdmin && userOrgId != organizationId)
                {
                    throw new UnauthorizedAccessException("User is not authorized to sync menu for this organization.");
                }

                // Get the sync configuration
                var syncConfig = await _syncConfigurationService.GetSyncConfigurationAsync(organizationId);
                if (syncConfig == null || !syncConfig.IsEnabled)
                {
                    return MenuSyncResult.CreateFailure("Sync configuration not found or disabled for this organization.");
                }

                // Fetch all active areas, categories, and menu items
                var areas = await _context.Areas
                    .Where(a => a.OrganizationId == organizationId)
                    .Include(a => a.MenuCategories)
                        .ThenInclude(mc => mc.MenuItems)
                    .ToListAsync();

                if (!areas.Any())
                {
                    return MenuSyncResult.CreateFailure("No areas found for this organization.");
                }

                // Transform data into the required format
                var menuSyncData = new MenuSyncData
                {
                    Areas = areas.Select(a => new AreaData
                    {
                        LocalAreaId = a.Id,
                        Name = a.Name,
                        Slug = !string.IsNullOrEmpty(a.Slug) ? a.Slug : GenerateSlug(a.Name),
                        GuestCharge = a.GuestCharge,
                        TakeawayCharge = a.TakeawayCharge,
                        Categories = a.MenuCategories.Select(mc => new CategoryData
                        {
                            LocalCategoryId = mc.Id,
                            Name = mc.Name,
                            Items = mc.MenuItems.Select(mi => new MenuItemData
                            {
                                LocalMenuItemId = mi.Id,
                                Name = mi.Name,
                                Description = mi.Description,
                                Price = mi.Price,
                                IsNoteRequired = mi.IsNoteRequired,
                                NoteSuggestion = mi.NoteSuggestion
                            }).ToList()
                        }).ToList()
                    }).ToList()
                };

                // Serialize the data to JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };
                var jsonContent = JsonSerializer.Serialize(menuSyncData, jsonOptions);

                // Create HTTP client and request
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{syncConfig.PlatformBaseUrl.TrimEnd('/')}/api/sync/menu");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("Authorization", $"ApiKey {syncConfig.ApiKey}");
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send the request
                var response = await client.SendAsync(request);

                // Handle the response
                if (response.IsSuccessStatusCode)
                {
                    return MenuSyncResult.CreateSuccess();
                }
                else
                {
                    var statusCode = (int)response.StatusCode;
                    var errorContent = await response.Content.ReadAsStringAsync();

                    string errorMessage = statusCode switch
                    {
                        400 => "The menu data is invalid. Please check the format and try again.",
                        401 => "Authentication failed. Please check your API key.",
                        500 => "An error occurred on the SagraPreOrdine server.",
                        _ => $"Unexpected error (HTTP {statusCode})."
                    };

                    _logger.LogError("Menu sync failed with status code {StatusCode}: {ErrorContent}", statusCode, errorContent);
                    return MenuSyncResult.CreateFailure(errorMessage, errorContent, statusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during menu synchronization for organization {OrganizationId}", organizationId);
                return MenuSyncResult.CreateFailure("An error occurred during synchronization.", ex.ToString());
            }
        }

        /// <summary>
        /// Generates a URL-friendly slug from a string
        /// </summary>
        /// <param name="input">The input string</param>
        /// <returns>A URL-friendly slug</returns>
        private string GenerateSlug(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Convert to lowercase
            var slug = input.ToLowerInvariant();

            // Remove accents
            slug = RemoveDiacritics(slug);

            // Replace spaces with hyphens
            slug = Regex.Replace(slug, @"\s+", "-");

            // Remove invalid characters
            slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

            // Remove duplicate hyphens
            slug = Regex.Replace(slug, @"-+", "-");

            // Trim hyphens from start and end
            slug = slug.Trim('-');

            return slug;
        }

        /// <summary>
        /// Removes diacritics (accents) from a string
        /// </summary>
        private string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }

    /// <summary>
    /// Data structure for menu synchronization
    /// </summary>
    public class MenuSyncData
    {
        public List<AreaData> Areas { get; set; } = new List<AreaData>();
    }

    public class AreaData
    {
        public int LocalAreaId { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public decimal GuestCharge { get; set; }
        public decimal TakeawayCharge { get; set; }
        public List<CategoryData> Categories { get; set; } = new List<CategoryData>();
    }

    public class CategoryData
    {
        public int LocalCategoryId { get; set; }
        public required string Name { get; set; }
        public List<MenuItemData> Items { get; set; } = new List<MenuItemData>();
    }

    public class MenuItemData
    {
        public int LocalMenuItemId { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public bool IsNoteRequired { get; set; }
        public string? NoteSuggestion { get; set; }
    }
}
