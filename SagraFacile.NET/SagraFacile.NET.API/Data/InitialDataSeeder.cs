using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions; // For slug generation
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Data
{
    public interface IInitialDataSeeder
    {
        Task SeedAsync();
    }

    public class InitialDataSeeder : IInitialDataSeeder
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InitialDataSeeder> _logger;

        public InitialDataSeeder(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<InitialDataSeeder> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var services = scope.ServiceProvider;
                var dbContext = services.GetRequiredService<ApplicationDbContext>();
                var userManager = services.GetRequiredService<UserManager<User>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                _logger.LogInformation("Starting database seeding process.");

                await SeedSystemDefaultsAsync(roleManager);

                // SAGRAFACILE_SEED_DEMO_DATA:
                // If true, seed demo data.
                // If false, attempt to seed initial org/admin from env vars.
                // If not set (null), default to false (attempt initial org/admin).
                bool seedDemoData = _configuration.GetValue<bool?>("SAGRAFACILE_SEED_DEMO_DATA") ?? false;

                if (seedDemoData)
                {
                    _logger.LogInformation("SAGRAFACILE_SEED_DEMO_DATA is true. Seeding Sagra Facile demo data.");
                    await SeedSagraDiTencarolaDataAsync(dbContext, userManager, roleManager);
                }
                else
                {
                    _logger.LogInformation("SAGRAFACILE_SEED_DEMO_DATA is false or not set. Attempting to seed initial organization and admin user if configured.");
                    await SeedInitialOrganizationAndAdminAsync(dbContext, userManager, roleManager);
                }
                _logger.LogInformation("Database seeding process completed.");
            }
        }

        private async Task SeedSystemDefaultsAsync(RoleManager<IdentityRole> roleManager)
        {
            _logger.LogInformation("Seeding system defaults (System Org, Roles, SuperAdmin).");

            // --- Seed Roles ---
            try
            {
                string[] roleNames = { "SuperAdmin", "Admin", "AreaAdmin", "Cashier", "Waiter" };
                IdentityResult roleResult;

                foreach (var roleName in roleNames)
                {
                    var roleExist = await roleManager.RoleExistsAsync(roleName);
                    if (!roleExist)
                    {
                        roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                        if (roleResult.Succeeded)
                        {
                            _logger.LogInformation("Role '{RoleName}' created successfully.", roleName);
                        }
                        else
                        {
                            foreach (var error in roleResult.Errors)
                            {
                                _logger.LogError("Error creating role '{RoleName}': {ErrorDescription}", roleName, error.Description);
                            }
                        }
                    }
                }
                _logger.LogInformation("Role seeding completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding roles.");
            }

        }

        private async Task SeedSagraDiTencarolaDataAsync(ApplicationDbContext dbContext, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _logger.LogInformation("Seeding 'Sagra Facile' demo data.");
            try
            {
                string orgName = "Sagra Facile";
                string orgSlug = "sagra-facile";

                var sagraOrg = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Name == orgName);
                if (sagraOrg == null)
                {
                    _logger.LogInformation("Creating '{OrgName}' organization.", orgName);
                    sagraOrg = new Organization { Name = orgName, Slug = orgSlug };
                    dbContext.Organizations.Add(sagraOrg);
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("'{OrgName}' organization created successfully with ID {OrgId}.", orgName, sagraOrg.Id);
                }
                else
                {
                    _logger.LogInformation("'{OrgName}' organization already exists with ID {OrgId}.", orgName, sagraOrg.Id);
                    if (string.IsNullOrEmpty(sagraOrg.Slug))
                    {
                        sagraOrg.Slug = orgSlug;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Updated slug for '{OrgName}'.", orgName);
                    }
                }

                string defaultPassword = _configuration["DEMO_USER_PASSWORD"] ?? "Password123!";
                var usersToSeed = new List<(string Email, string FirstName, string LastName, string Role)>
                {
                    ("cashier@sagrafacile.it", "Cassa", "SagraFacile", "Cashier"),
                    ("waiter@sagrafacile.it", "Cameriere", "SagraFacile", "Waiter"),
                    ("admin@sagrafacile.it", "Admin", "SagraFacile", "Admin")
                };

                foreach (var (email, firstName, lastName, role) in usersToSeed)
                {
                    var user = await userManager.FindByEmailAsync(email);
                    if (user == null)
                    {
                        user = new User { UserName = email, Email = email, FirstName = firstName, LastName = lastName, EmailConfirmed = true, OrganizationId = sagraOrg.Id };
                        var result = await userManager.CreateAsync(user, defaultPassword);
                        if (result.Succeeded)
                        {
                            if (await roleManager.RoleExistsAsync(role))
                            {
                                await userManager.AddToRoleAsync(user, role);
                                _logger.LogInformation("User '{Email}' created and assigned '{Role}' role for Org ID {OrgId}.", email, role, sagraOrg.Id);
                            }
                            else
                            {
                                _logger.LogWarning("Role '{Role}' does not exist. Cannot assign to user '{Email}'.", role, email);
                            }
                        }
                        else { _logger.LogError("Error creating user {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description))); }
                    }
                    else { _logger.LogInformation("User '{Email}' already exists.", email); }
                }

                var areasToSeed = new List<(string Name, string Slug)>
                {
                    ("Paninoteca", "paninoteca"),
                    ("Stand Gastronomico", "stand-gastronomico")
                };

                Area areaPaninoteca = null;
                Area areaStandGastronomico = null;

                foreach (var (areaName, areaSlug) in areasToSeed)
                {
                    var area = await dbContext.Areas.FirstOrDefaultAsync(a => a.OrganizationId == sagraOrg.Id && a.Name == areaName);
                    if (area == null)
                    {
                        area = new Area { Name = areaName, Slug = areaSlug, OrganizationId = sagraOrg.Id };
                        dbContext.Areas.Add(area);
                        _logger.LogInformation("Area '{AreaName}' created for Org ID {OrgId}.", areaName, sagraOrg.Id);
                    }
                    else { _logger.LogInformation("Area '{AreaName}' already exists for Org ID {OrgId}.", areaName, sagraOrg.Id); }

                    if (areaName == "Paninoteca") areaPaninoteca = area;
                    if (areaName == "Stand Gastronomico") areaStandGastronomico = area;
                }
                await dbContext.SaveChangesAsync(); // Save Areas to get IDs

                // Ensure area instances are up-to-date if they existed
                areaPaninoteca ??= await dbContext.Areas.FirstAsync(a => a.OrganizationId == sagraOrg.Id && a.Name == "Paninoteca");
                areaStandGastronomico ??= await dbContext.Areas.FirstAsync(a => a.OrganizationId == sagraOrg.Id && a.Name == "Stand Gastronomico");

                var categories = new Dictionary<string, List<(string Name, decimal Price, string Description)>>
                {
                    { "Primi", new List<(string, decimal, string)> {
                        ("Bigoli al Ragù d'Anatra", 8.50m, "Pasta fresca con sugo d'anatra"),
                        ("Gnocchi al Pomodoro", 7.00m, "Gnocchi di patate fatti in casa"),
                        ("Risotto ai Funghi Porcini", 9.00m, "Risotto cremoso con funghi porcini freschi")
                    }},
                    { "Griglie", new List<(string, decimal, string)> {
                        ("Costine di Maiale", 10.00m, "Costine marinate e grigliate"),
                        ("Salsiccia alla Griglia", 6.50m, "Salsiccia nostrana"),
                        ("Pollo alla Diavola", 9.50m, "Mezzo pollo piccante"),
                        ("Tagliata di Manzo", 15.00m, "Manzo selezionato con rucola e grana")
                    }},
                    { "Senza Glutine", new List<(string, decimal, string)> {
                        ("Pasta al Pesto (SG)", 8.00m, "Pasta senza glutine con pesto genovese"),
                        ("Grigliata Mista (SG)", 14.00m, "Selezione di carni alla griglia")
                    }},
                    { "Bar", new List<(string, decimal, string)> {
                        ("Acqua Naturale 0.5L", 1.00m, ""),
                        ("Acqua Frizzante 0.5L", 1.00m, ""),
                        ("Coca Cola", 2.50m, "Lattina 33cl"),
                        ("Birra Bionda Media", 4.00m, "Spina 0.4L"),
                        ("Vino Rosso (Calice)", 3.00m, "Cabernet locale"),
                        ("Caffè", 1.20m, "Espresso")
                    }}
                };

                if (areaStandGastronomico != null)
                {
                    foreach (var kvp in categories)
                    {
                        string categoryName = kvp.Key;
                        var category = await dbContext.MenuCategories.FirstOrDefaultAsync(mc => mc.AreaId == areaStandGastronomico.Id && mc.Name == categoryName);
                        if (category == null)
                        {
                            category = new MenuCategory { Name = categoryName, AreaId = areaStandGastronomico.Id };
                            dbContext.MenuCategories.Add(category);
                            await dbContext.SaveChangesAsync(); // Save category to get ID
                            _logger.LogInformation("Category '{CategoryName}' created for Area ID {AreaId}.", categoryName, areaStandGastronomico.Id);
                        }
                        else
                        {
                            _logger.LogInformation("Category '{CategoryName}' already exists for Area ID {AreaId}.", categoryName, areaStandGastronomico.Id);
                        }
                        category ??= await dbContext.MenuCategories.FirstAsync(mc => mc.AreaId == areaStandGastronomico.Id && mc.Name == categoryName);

                        foreach (var (itemName, itemPrice, itemDescription) in kvp.Value)
                        {
                            var menuItem = await dbContext.MenuItems.FirstOrDefaultAsync(mi => mi.MenuCategoryId == category.Id && mi.Name == itemName);
                            if (menuItem == null)
                            {
                                menuItem = new MenuItem { Name = itemName, Description = itemDescription, Price = itemPrice, MenuCategoryId = category.Id };
                                dbContext.MenuItems.Add(menuItem);
                                _logger.LogInformation("MenuItem '{ItemName}' created for Category ID {CategoryId}.", itemName, category.Id);
                            }
                            else
                            {
                                _logger.LogInformation("MenuItem '{ItemName}' already exists for Category ID {CategoryId}.", itemName, category.Id);
                            }
                        }
                    }
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("Area 'Stand Gastronomico' not found or not created. Skipping menu seeding for it.");
                }
                _logger.LogInformation("Sagra Facile data seeding completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred seeding the Sagra Facile data.");
            }
        }

        private static string GenerateSlug(string phrase, int maxLength = 50)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return string.Empty;
            string str = phrase.ToLowerInvariant();
            // invalid chars, make into spaces
            str = Regex.Replace(str, @"[^a-z0-9\s-]", " ");
            // convert multiple spaces/hyphens into one space       
            str = Regex.Replace(str, @"[\s-]+", " ").Trim();
            // cut and trim it
            str = str.Substring(0, str.Length <= maxLength ? str.Length : maxLength).Trim();
            // hyphens
            str = Regex.Replace(str, @"\s", "-");
            return str;
        }

        private async Task SeedInitialOrganizationAndAdminAsync(ApplicationDbContext dbContext, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _logger.LogInformation("Checking for initial organization and admin user configuration based on environment variables.");
            string initialOrgName = _configuration["INITIAL_ORGANIZATION_NAME"];
            string initialAdminEmail = _configuration["INITIAL_ADMIN_EMAIL"];
            string initialAdminPassword = _configuration["INITIAL_ADMIN_PASSWORD"];

            if (string.IsNullOrWhiteSpace(initialOrgName) ||
                string.IsNullOrWhiteSpace(initialAdminEmail) ||
                string.IsNullOrWhiteSpace(initialAdminPassword))
            {
                _logger.LogInformation("Initial organization/admin configuration (INITIAL_ORGANIZATION_NAME, INITIAL_ADMIN_EMAIL, INITIAL_ADMIN_PASSWORD) not fully provided. Skipping this setup.");
                return;
            }

            // Check if any organizations exist besides "System" and "Sagra Facile" (if demo was not seeded)
            var existingUserOrgsCount = await dbContext.Organizations.CountAsync(o => o.Name != "System" && o.Name != "Sagra Facile");
            if (existingUserOrgsCount > 0)
            {
                _logger.LogInformation("User-defined organizations (other than 'System' or 'Sagra Facile') already exist. Skipping initial organization and admin setup from environment variables.");
                return;
            }

            _logger.LogInformation("Attempting to create initial organization: '{OrgName}' and admin: '{AdminEmail}' from environment variables.", initialOrgName, initialAdminEmail);

            var initialOrg = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Name == initialOrgName);
            if (initialOrg == null)
            {
                _logger.LogInformation("Creating initial organization '{OrgName}'.", initialOrgName);
                string orgSlug = GenerateSlug(initialOrgName);
                initialOrg = new Organization { Name = initialOrgName, Slug = orgSlug };
                dbContext.Organizations.Add(initialOrg);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Initial organization '{OrgName}' created successfully with ID {OrgId}.", initialOrgName, initialOrg.Id);
            }
            else
            {
                _logger.LogInformation("Initial organization '{OrgName}' already exists with ID {OrgId}.", initialOrgName, initialOrg.Id);
            }

            var adminUser = await userManager.FindByEmailAsync(initialAdminEmail);
            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = initialAdminEmail,
                    Email = initialAdminEmail,
                    FirstName = "Admin",
                    LastName = initialOrgName,
                    EmailConfirmed = true,
                    OrganizationId = initialOrg.Id
                };
                var createUserResult = await userManager.CreateAsync(adminUser, initialAdminPassword);
                if (createUserResult.Succeeded)
                {
                    _logger.LogInformation("Initial admin user '{AdminEmail}' created successfully for organization '{OrgName}'.", initialAdminEmail, initialOrgName);
                    if (await roleManager.RoleExistsAsync("Admin"))
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        _logger.LogInformation("Assigned 'Admin' role to initial admin user '{AdminEmail}'.", initialAdminEmail);
                    }
                    else
                    {
                        _logger.LogWarning("'Admin' role does not exist. Cannot assign to initial admin user '{AdminEmail}'.", initialAdminEmail);
                    }
                }
                else
                {
                    foreach (var error in createUserResult.Errors)
                    {
                        _logger.LogError("Error creating initial admin user '{AdminEmail}': {ErrorDescription}", initialAdminEmail, error.Description);
                    }
                }
            }
            else
            {
                _logger.LogInformation("Initial admin user '{AdminEmail}' already exists.", initialAdminEmail);
                if (adminUser.OrganizationId != initialOrg.Id) // Check if org ID is correct
                {
                    adminUser.OrganizationId = initialOrg.Id;
                    await userManager.UpdateAsync(adminUser);
                    _logger.LogInformation("Corrected Organization ID for existing initial admin user '{AdminEmail}'.", initialAdminEmail);
                }
            }
        }
    }

    public static class InitialDataSeederExtensions
    {
        public static async Task SeedDatabaseAsync(this IApplicationBuilder app)
        {
            var hostEnvironment = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
            if (!hostEnvironment.IsEnvironment("Testing")) // Ensure we don't run this during integration tests
            {
                using (var scope = app.ApplicationServices.CreateScope())
                {
                    var services = scope.ServiceProvider;
                    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger("SagraFacile.NET.API.Data.InitialDataSeederExtensions");
                    logger.LogInformation("Attempting to seed database via InitialDataSeederExtensions.");

                    var seeder = services.GetRequiredService<IInitialDataSeeder>();
                    await seeder.SeedAsync();
                }
            }
            else
            {
                var loggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("SagraFacile.NET.API.Data.InitialDataSeederExtensions");
                logger.LogInformation("Skipping database seeding because environment is 'Testing'.");
            }
        }
    }
}
