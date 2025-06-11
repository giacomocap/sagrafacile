using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore; // Ensure this line is present
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums; // Added for PrinterType

namespace SagraFacile.NET.API.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole, string> // Specify User and IdentityRole
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet for your custom entities
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Area> Areas { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<KdsStation> KdsStations { get; set; }
        public DbSet<KdsCategoryAssignment> KdsCategoryAssignments { get; set; }
        public DbSet<OrderKdsStationStatus> OrderKdsStationStatuses { get; set; } // Added DbSet for new entity
        public DbSet<Day> Days { get; set; } // Added for Operational Day
        public DbSet<SyncConfiguration> SyncConfigurations { get; set; } // Added for SagraPreOrdine integration
        public DbSet<Printer> Printers { get; set; } // Added for Printer
        public DbSet<PrinterCategoryAssignment> PrinterCategoryAssignments { get; set; } // Added for Printer-Category mapping
        public DbSet<CashierStation> CashierStations { get; set; }
        public DbSet<AreaQueueState> AreaQueueStates { get; set; }
        public DbSet<AreaDayOrderSequence> AreaDayOrderSequences { get; set; } // Added for DisplayOrderNumber
        public DbSet<AdMediaItem> AdMediaItems { get; set; }
        public DbSet<AdAreaAssignment> AdAreaAssignments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure decimal precision for currency fields
            builder.Entity<MenuItem>()
                .Property(mi => mi.Price)
                .HasColumnType("decimal(18, 2)");

            builder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasColumnType("decimal(18, 2)");

            builder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasColumnType("decimal(18, 2)");

            builder.Entity<Order>()
                .Property(o => o.AmountPaid)
                .HasColumnType("decimal(18, 2)");

            // Configure relationships (many are handled by convention, but explicit is clearer)

            // Organization relationships
            builder.Entity<Organization>()
                .HasMany(o => o.Areas)
                .WithOne(a => a.Organization)
                .HasForeignKey(a => a.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Organization if Areas exist

            builder.Entity<Organization>()
                .HasMany(o => o.Users)
                .WithOne(u => u.Organization)
                .HasForeignKey(u => u.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Organization if Users exist

            builder.Entity<Organization>()
                .HasMany(o => o.Orders)
                .WithOne(or => or.Organization)
                .HasForeignKey(or => or.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Organization if Orders exist

            // Configure Organization Slug
            builder.Entity<Organization>()
                .Property(o => o.Slug)
                .IsRequired();
            builder.Entity<Organization>()
                .HasIndex(o => o.Slug)
                .IsUnique();

            // Area relationships
            builder.Entity<Area>()
                .HasMany(a => a.MenuCategories)
                .WithOne(mc => mc.Area)
                .HasForeignKey(mc => mc.AreaId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Area deletes its Categories

            builder.Entity<Area>()
                .HasMany(a => a.Orders)
                .WithOne(o => o.Area)
                .HasForeignKey(o => o.AreaId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Area if Orders exist

            // Configure Area Slug (unique within an Organization)
            builder.Entity<Area>()
                .Property(a => a.Slug)
                .IsRequired();
            builder.Entity<Area>()
                .HasIndex(a => new { a.OrganizationId, a.Slug }) // Slug must be unique within the Organization
                .IsUnique();

            // MenuCategory relationships
            builder.Entity<MenuCategory>()
                .HasMany(mc => mc.MenuItems)
                .WithOne(mi => mi.MenuCategory)
                .HasForeignKey(mi => mi.MenuCategoryId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a Category deletes its Items

            // MenuItem relationships
            builder.Entity<MenuItem>()
                .HasMany(mi => mi.OrderItems)
                .WithOne(oi => oi.MenuItem)
                .HasForeignKey(oi => oi.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting MenuItem if used in Orders

            // Order relationships
            builder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Order deletes its Items

            builder.Entity<Order>()
                .HasOne(o => o.Cashier)
                .WithMany(u => u.HandledOrders) // Use the navigation property defined in User
                .HasForeignKey(o => o.CashierId)
                .IsRequired(false) // Make the relationship optional as CashierId is nullable
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting User if they handled Orders

            // Removed indexes on the old OrderNumber property.
            // The primary key 'Id' will have an index by default.

            // KDS Station relationships
            builder.Entity<KdsStation>()
                .HasOne(ks => ks.Area)
                .WithMany() // Assuming Area doesn't need a direct collection of KdsStations
                .HasForeignKey(ks => ks.AreaId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Area deletes its KDS Stations

            builder.Entity<KdsStation>()
                .HasOne(ks => ks.Organization)
                .WithMany() // Assuming Organization doesn't need a direct collection of KdsStations
                .HasForeignKey(ks => ks.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Organization if KDS Stations exist

            // KDS Category Assignment (Many-to-Many Join Table)
            builder.Entity<KdsCategoryAssignment>()
                .HasKey(kca => new { kca.KdsStationId, kca.MenuCategoryId }); // Composite primary key

            builder.Entity<KdsCategoryAssignment>()
                .HasOne(kca => kca.KdsStation)
                .WithMany(ks => ks.KdsCategoryAssignments)
                .HasForeignKey(kca => kca.KdsStationId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a KDS Station removes its assignments

            builder.Entity<KdsCategoryAssignment>()
                .HasOne(kca => kca.MenuCategory)
                .WithMany() // Assuming MenuCategory doesn't need a direct collection of assignments
                .HasForeignKey(kca => kca.MenuCategoryId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a Menu Category removes its assignments

            // Order KDS Station Status (New Entity)
            builder.Entity<OrderKdsStationStatus>()
                .HasKey(okss => new { okss.OrderId, okss.KdsStationId }); // Composite primary key

            builder.Entity<OrderKdsStationStatus>()
                .HasOne(okss => okss.Order)
                .WithMany() // Assuming Order doesn't need a direct collection of these statuses
                .HasForeignKey(okss => okss.OrderId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Order removes its KDS station statuses

            builder.Entity<OrderKdsStationStatus>()
                .HasOne(okss => okss.KdsStation)
                .WithMany() // Assuming KdsStation doesn't need a direct collection of these statuses
                .HasForeignKey(okss => okss.KdsStationId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting a KDS Station removes its statuses

            // Day (Operational Day) relationships
            builder.Entity<Day>()
                .HasOne(d => d.Organization)
                .WithMany() // Assuming Organization doesn't need a direct collection of Days
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Organization if Days exist

            builder.Entity<Day>()
                .HasOne(d => d.OpenedByUser)
                .WithMany() // Assuming User doesn't need a direct collection of Days they opened
                .HasForeignKey(d => d.OpenedByUserId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting User if they opened Days

            builder.Entity<Day>()
                .HasOne(d => d.ClosedByUser)
                .WithMany() // Assuming User doesn't need a direct collection of Days they closed
                .HasForeignKey(d => d.ClosedByUserId)
                .IsRequired(false) // ClosedByUserId is nullable
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting User if they closed Days

            builder.Entity<Day>()
                .HasMany(d => d.Orders)
                .WithOne(o => o.Day)
                .HasForeignKey(o => o.DayId)
                .IsRequired(false) // DayId is nullable on Order
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Day if Orders are associated

            builder.Entity<Day>()
                .Property(d => d.TotalSales)
                .HasColumnType("decimal(18, 2)");

            // Update Order relationship to include Day
            builder.Entity<Order>()
                .HasOne(o => o.Day)
                .WithMany(d => d.Orders)
                .HasForeignKey(o => o.DayId)
                .IsRequired(false); // Explicitly state DayId is optional on Order side too

            // SyncConfiguration relationship with Organization (one-to-one)
            builder.Entity<SyncConfiguration>()
                .HasOne(sc => sc.Organization)
                .WithOne(o => o.SyncConfiguration)
                .HasForeignKey<SyncConfiguration>(sc => sc.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Organization deletes its SyncConfiguration

            // Ensure OrganizationId is unique in SyncConfiguration
            builder.Entity<SyncConfiguration>()
                .HasIndex(sc => sc.OrganizationId)
                .IsUnique();

            // -- Customer Queue System Configuration -- START
            builder.Entity<AreaQueueState>()
                .HasOne(aqs => aqs.Area)
                .WithOne() // No navigation property back from Area to AreaQueueState needed by EF Core for one-to-one
                .HasForeignKey<AreaQueueState>(aqs => aqs.AreaId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Area deletes its QueueState

            // Ensure AreaId is unique in AreaQueueState to enforce one-to-one
            builder.Entity<AreaQueueState>()
                .HasIndex(aqs => aqs.AreaId)
                .IsUnique();

            // Configure the relationship with CashierStation (if CashierStationId is Guid)
            // Assuming CashierStation.Id is Guid. If it's int, this part needs adjustment.
            builder.Entity<AreaQueueState>()
                .HasOne(aqs => aqs.LastCalledCashierStation)
                .WithMany() // Assuming CashierStation doesn't need a collection of AreaQueueStates referencing it
                .HasForeignKey(aqs => aqs.LastCalledCashierStationId)
                .IsRequired(false) // LastCalledCashierStationId is nullable
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting CashierStation if referenced
            // -- Customer Queue System Configuration -- END

            // -- Printer Configuration --

            // Printer relationships
            builder.Entity<Printer>()
                .HasOne(p => p.Organization)
                .WithMany() // Assuming Organization doesn't need a direct collection of Printers
                .HasForeignKey(p => p.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Organization if Printers exist

            // Area relationship with ReceiptPrinter
            builder.Entity<Area>()
                .HasOne(a => a.ReceiptPrinter)
                .WithMany() // Assuming Printer doesn't need a direct collection of Areas using it for receipts
                .HasForeignKey(a => a.ReceiptPrinterId)
                .IsRequired(false) // ReceiptPrinterId is nullable
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting Printer if used as ReceiptPrinter

            // Printer Category Assignment (Many-to-Many Join Table)
            builder.Entity<PrinterCategoryAssignment>()
                .HasKey(pca => new { pca.PrinterId, pca.MenuCategoryId });

            builder.Entity<PrinterCategoryAssignment>()
                .HasOne(pca => pca.Printer)
                .WithMany(p => p.CategoryAssignments) // Correct: Assumes Printer model has this collection
                .HasForeignKey(pca => pca.PrinterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PrinterCategoryAssignment>()
                .HasOne(pca => pca.MenuCategory)
                // Correctly specify the inverse navigation property from MenuCategory
                .WithMany(mc => mc.PrinterAssignments)
                .HasForeignKey(pca => pca.MenuCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure WindowsPrinterName is provided if Type is WindowsUsb (handled via validation logic, not DB constraint)

            // Convert PrinterType enum to string in the database for readability
            builder.Entity<Printer>()
                .Property(p => p.Type)
                .HasConversion<string>();

            // CashierStation relationships
            builder.Entity<CashierStation>()
                .HasOne(cs => cs.Organization)
                .WithMany()
                .HasForeignKey(cs => cs.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict); // Or Cascade if appropriate

            builder.Entity<CashierStation>()
                .HasOne(cs => cs.Area)
                .WithMany()
                .HasForeignKey(cs => cs.AreaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CashierStation>()
                .HasOne(cs => cs.ReceiptPrinter)
                .WithMany()
                .HasForeignKey(cs => cs.ReceiptPrinterId)
                .OnDelete(DeleteBehavior.Restrict);

            // You might want to add more specific configurations or constraints here
            // based on your application's rules.

            // -- Display Order Number Configuration -- START
            builder.Entity<AreaDayOrderSequence>()
                .HasOne(ads => ads.Area)
                .WithMany() // Area does not have a navigation property back to AreaDayOrderSequence
                .HasForeignKey(ads => ads.AreaId)
                .OnDelete(DeleteBehavior.Cascade); // If an Area is deleted, its sequences are deleted

            builder.Entity<AreaDayOrderSequence>()
                .HasOne(ads => ads.Day)
                .WithMany() // Day does not have a navigation property back to AreaDayOrderSequence
                .HasForeignKey(ads => ads.DayId)
                .OnDelete(DeleteBehavior.Cascade); // If a Day is deleted, its sequences are deleted

            // Composite unique index on (AreaId, DayId)
            builder.Entity<AreaDayOrderSequence>()
                .HasIndex(ads => new { ads.AreaId, ads.DayId })
                .IsUnique();
            // -- Display Order Number Configuration -- END

            // -- Ad Carousel Configuration -- START
            // AdMediaItem is now linked to Organization
            builder.Entity<AdMediaItem>()
                .HasOne(ad => ad.Organization)
                .WithMany() // Organization does not have a direct navigation property back
                .HasForeignKey(ad => ad.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Organization deletes its media items

            builder.Entity<AdMediaItem>()
                .Property(ad => ad.MediaType)
                .HasConversion<string>();

            // AdAreaAssignment (Many-to-Many Join Table with payload)
            builder.Entity<AdAreaAssignment>()
                .HasKey(aaa => aaa.Id); // Use a surrogate key

            builder.Entity<AdAreaAssignment>()
                .HasIndex(aaa => new { aaa.AdMediaItemId, aaa.AreaId })
                .IsUnique(); // Prevent assigning the same ad to the same area more than once

            builder.Entity<AdAreaAssignment>()
                .HasOne(aaa => aaa.AdMediaItem)
                .WithMany(ad => ad.Assignments)
                .HasForeignKey(aaa => aaa.AdMediaItemId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting the media item deletes the assignment

            builder.Entity<AdAreaAssignment>()
                .HasOne(aaa => aaa.Area)
                .WithMany() // Area does not have a direct navigation property back
                .HasForeignKey(aaa => aaa.AreaId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting an Area deletes its assignments
            // -- Ad Carousel Configuration -- END
        }
    }
}
