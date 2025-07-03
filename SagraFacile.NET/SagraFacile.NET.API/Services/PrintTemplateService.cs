using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;

namespace SagraFacile.NET.API.Services
{
    public class PrintTemplateService : BaseService, IPrintTemplateService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrintTemplateService> _logger;
        private readonly IPdfService _pdfService;

        public PrintTemplateService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<PrintTemplateService> logger, IPdfService pdfService)
            : base(httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _pdfService = pdfService;
        }

        public async Task<(bool Success, PaginatedResult<PrintTemplateDto>? Result, string? Error)> GetAllAsync(Guid organizationId, QueryParameters queryParameters)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                return (false, null, "Unauthorized to access templates for this organization.");
            }

            var query = _context.PrintTemplates
                .Where(t => t.OrganizationId == organizationId)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(queryParameters.SortBy))
            {
                query = query.OrderBy($"{queryParameters.SortBy} {(queryParameters.SortAscending ? "ascending" : "descending")}");
            }

            var totalCount = await query.CountAsync();

            var templates = await query
                .Skip((queryParameters.Page - 1) * queryParameters.PageSize)
                .Take(queryParameters.PageSize)
                .Select(t => MapTemplateToDto(t))
                .ToListAsync();

            var paginatedResult = new PaginatedResult<PrintTemplateDto>
            {
                Items = templates,
                TotalCount = totalCount,
                Page = queryParameters.Page,
                PageSize = queryParameters.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)queryParameters.PageSize)
            };

            return (true, paginatedResult, null);
        }

        public async Task<(bool Success, PrintTemplateDto? Template, string? Error)> GetByIdAsync(int id, Guid organizationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            var template = await _context.PrintTemplates.AsNoTracking()
                                       .FirstOrDefaultAsync(t => t.Id == id);

            if (template == null)
            {
                return (false, null, "Template not found.");
            }

            if (!isSuperAdmin && template.OrganizationId != userOrgId)
            {
                return (false, null, "Unauthorized to access this template.");
            }
            
            if (template.OrganizationId != organizationId)
            {
                return (false, null, "Template does not belong to the specified organization.");
            }

            return (true, MapTemplateToDto(template), null);
        }

        public async Task<(bool Success, PrintTemplateDto? CreatedTemplate, string? Error)> CreateAsync(PrintTemplateUpsertDto createDto, Guid organizationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                return (false, null, "Unauthorized to create a template for this organization.");
            }

            // Validate HTML content before saving
            if (createDto.DocumentType == DocumentType.HtmlPdf && !string.IsNullOrWhiteSpace(createDto.HtmlContent))
            {
                var (isValid, validationError) = await IsHtmlTemplateValidAsync(createDto.HtmlContent, organizationId);
                if (!isValid)
                {
                    return (false, null, validationError);
                }
            }

            if (createDto.IsDefault)
            {
                await UnsetExistingDefaultAsync(createDto.OrganizationId, createDto.TemplateType, createDto.DocumentType);
            }

            var template = new PrintTemplate
            {
                Name = createDto.Name,
                OrganizationId = createDto.OrganizationId,
                TemplateType = createDto.TemplateType,
                DocumentType = createDto.DocumentType,
                HtmlContent = createDto.HtmlContent,
                EscPosHeader = createDto.EscPosHeader,
                EscPosFooter = createDto.EscPosFooter,
                IsDefault = createDto.IsDefault
            };

            _context.PrintTemplates.Add(template);
            await _context.SaveChangesAsync();

            return (true, MapTemplateToDto(template), null);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, PrintTemplateUpsertDto updateDto, Guid organizationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            var template = await _context.PrintTemplates.FindAsync(id);

            if (template == null)
            {
                return (false, "Template not found.");
            }

            if (!isSuperAdmin && template.OrganizationId != userOrgId)
            {
                return (false, "Unauthorized to update this template.");
            }
            
            if (template.OrganizationId != organizationId)
            {
                return (false, "Template does not belong to the specified organization.");
            }

            // Validate HTML content before saving
            if (updateDto.DocumentType == DocumentType.HtmlPdf && !string.IsNullOrWhiteSpace(updateDto.HtmlContent))
            {
                var (isValid, validationError) = await IsHtmlTemplateValidAsync(updateDto.HtmlContent, organizationId);
                if (!isValid)
                {
                    return (false, validationError);
                }
            }

            if (updateDto.IsDefault && !template.IsDefault)
            {
                await UnsetExistingDefaultAsync(updateDto.OrganizationId, updateDto.TemplateType, updateDto.DocumentType, id);
            }

            template.Name = updateDto.Name;
            template.TemplateType = updateDto.TemplateType;
            template.DocumentType = updateDto.DocumentType;
            template.HtmlContent = updateDto.HtmlContent;
            template.EscPosHeader = updateDto.EscPosHeader;
            template.EscPosFooter = updateDto.EscPosFooter;
            template.IsDefault = updateDto.IsDefault;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(int id, Guid organizationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            var template = await _context.PrintTemplates.FindAsync(id);

            if (template == null)
            {
                return (false, "Template not found.");
            }

            if (!isSuperAdmin && template.OrganizationId != userOrgId)
            {
                return (false, "Unauthorized to delete this template.");
            }

            if (template.OrganizationId != organizationId)
            {
                return (false, "Template does not belong to the specified organization.");
            }

            _context.PrintTemplates.Remove(template);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> RestoreDefaultHtmlTemplatesAsync(Guid organizationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                return (false, "Unauthorized to restore templates for this organization.");
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                
                // Restore Receipt
                var receiptTemplateName = "SagraFacile.NET.API.PrintTemplates.Html.receipt.html";
                await using var receiptStream = assembly.GetManifestResourceStream(receiptTemplateName);
                if (receiptStream == null) return (false, "Embedded receipt template not found.");
                using var receiptReader = new StreamReader(receiptStream);
                var receiptHtml = await receiptReader.ReadToEndAsync();
                await CreateOrUpdateDefaultTemplate(organizationId, PrintJobType.Receipt, receiptHtml);

                // Restore Comanda
                var comandaTemplateName = "SagraFacile.NET.API.PrintTemplates.Html.comanda.html";
                await using var comandaStream = assembly.GetManifestResourceStream(comandaTemplateName);
                if (comandaStream == null) return (false, "Embedded comanda template not found.");
                using var comandaReader = new StreamReader(comandaStream);
                var comandaHtml = await comandaReader.ReadToEndAsync();
                await CreateOrUpdateDefaultTemplate(organizationId, PrintJobType.Comanda, comandaHtml);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore default HTML templates for organization {OrgId}", organizationId);
                return (false, "An unexpected error occurred while restoring templates.");
            }
        }

        public async Task<(bool Success, byte[]? PdfBytes, string? Error)> GeneratePreviewAsync(Guid organizationId, PreviewRequestDto previewRequest)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                return (false, null, "Unauthorized to generate preview for this organization.");
            }

            try
            {
                var sampleOrder = CreateSampleOrder(organizationId);
                var pdfBytes = await _pdfService.CreatePdfFromHtmlAsync(sampleOrder, previewRequest.HtmlContent);
                return (true, pdfBytes, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF preview for organization {OrgId}", organizationId);
                // Return the specific error message if available
                return (false, null, $"Failed to generate PDF preview: {ex.Message}");
            }
        }

        private async Task<(bool IsValid, string? Error)> IsHtmlTemplateValidAsync(string htmlContent, Guid organizationId)
        {
            try
            {
                var sampleOrder = CreateSampleOrder(organizationId);
                // We don't need the result, just to see if it throws
                await _pdfService.CreatePdfFromHtmlAsync(sampleOrder, htmlContent);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Template validation failed for organization {OrgId}", organizationId);
                return (false, $"Template validation failed: {ex.Message}");
            }
        }

        private async Task CreateOrUpdateDefaultTemplate(Guid organizationId, PrintJobType templateType, string htmlContent)
        {
            await UnsetExistingDefaultAsync(organizationId, templateType, DocumentType.HtmlPdf);

            var existingDefault = await _context.PrintTemplates
                .FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.TemplateType == templateType && t.DocumentType == DocumentType.HtmlPdf);

            if (existingDefault != null)
            {
                existingDefault.HtmlContent = htmlContent;
                existingDefault.IsDefault = true;
                existingDefault.Name = $"Default {templateType} HTML";
            }
            else
            {
                var newTemplate = new PrintTemplate
                {
                    Name = $"Default {templateType} HTML",
                    OrganizationId = organizationId,
                    TemplateType = templateType,
                    DocumentType = DocumentType.HtmlPdf,
                    HtmlContent = htmlContent,
                    IsDefault = true
                };
                _context.PrintTemplates.Add(newTemplate);
            }
            await _context.SaveChangesAsync();
        }

        private async Task UnsetExistingDefaultAsync(Guid organizationId, PrintJobType templateType, DocumentType documentType, int? excludeTemplateId = null)
        {
            var query = _context.PrintTemplates
                .Where(t => t.OrganizationId == organizationId &&
                            t.TemplateType == templateType &&
                            t.DocumentType == documentType &&
                            t.IsDefault);

            if (excludeTemplateId.HasValue)
            {
                query = query.Where(t => t.Id != excludeTemplateId.Value);
            }

            var existingDefaults = await query.ToListAsync();

            if (existingDefaults.Any())
            {
                foreach (var template in existingDefaults)
                {
                    template.IsDefault = false;
                }
                await _context.SaveChangesAsync();
            }
        }

        private static PrintTemplateDto MapTemplateToDto(PrintTemplate template)
        {
            return new PrintTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                OrganizationId = template.OrganizationId,
                TemplateType = template.TemplateType,
                DocumentType = template.DocumentType,
                HtmlContent = template.HtmlContent,
                EscPosHeader = template.EscPosHeader,
                EscPosFooter = template.EscPosFooter,
                IsDefault = template.IsDefault
            };
        }

        private Order CreateSampleOrder(Guid organizationId)
        {
            var orderId = $"PREVIEW_{Guid.NewGuid().ToString().Substring(0, 8)}";
            return new Order
            {
                Id = orderId,
                DisplayOrderNumber = "P-123",
                OrganizationId = organizationId,
                Organization = new Organization { Name = "Sagra di Prova" },
                Area = new Area { Name = "Cassa Centrale", GuestCharge = 1.5m, TakeawayCharge = 0.5m },
                CashierStation = new CashierStation { Name = "Cassa 1" },
                OrderDateTime = DateTime.Now,
                CustomerName = "Mario Rossi",
                NumberOfGuests = 2,
                TotalAmount = 48.50m,
                IsTakeaway = false,
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        MenuItem = new MenuItem { Name = "Bigoli al Ragù d'Anatra", MenuCategory = new MenuCategory { Name = "Primi Piatti" } },
                        Quantity = 2,
                        UnitPrice = 12.00m,
                        Note = "Senza formaggio"
                    },
                    new OrderItem
                    {
                        MenuItem = new MenuItem { Name = "Grigliata Mista", MenuCategory = new MenuCategory { Name = "Secondi Piatti" } },
                        Quantity = 1,
                        UnitPrice = 18.00m
                    },
                    new OrderItem
                    {
                        MenuItem = new MenuItem { Name = "Patatine Fritte", MenuCategory = new MenuCategory { Name = "Contorni" } },
                        Quantity = 1,
                        UnitPrice = 4.50m
                    },
                    new OrderItem
                    {
                        MenuItem = new MenuItem { Name = "Acqua Naturale 1L", MenuCategory = new MenuCategory { Name = "Bevande" } },
                        Quantity = 1,
                        UnitPrice = 2.00m
                    }
                }
            };
        }
    }
}
