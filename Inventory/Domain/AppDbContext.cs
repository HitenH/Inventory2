using Inventory.Domain.Entities;
using Inventory.Service;
using Microsoft.EntityFrameworkCore;
using System;


namespace Inventory.Domain
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<UserEntity> Users { get; set; }
        public DbSet<RoleEntity> Roles { get; set; }
        public DbSet<RolePermissionEntity> RolePermissions { get; set; }
        public DbSet<UserRoleEntity> UserRoles { get; set; }
        public DbSet<PairRuleEntity> PairRules { get; set; }
        public DbSet<CustomerEntity> Customers { get; set; }
        public DbSet<SupplierEntity> Suppliers { get; set; }
        public DbSet<ProductEntity> Products { get; set; }
        public DbSet<VariantEntity> Variants { get; set; }
        public DbSet<CategoryEntity> Categories { get; set; }
        public DbSet<Mobile> Mobiles { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<PurchaseOrderEntity> PurchaseOrders { get; set; }
        public DbSet<PurchaseEntity> Purchases { get; set; }
        public DbSet<PurchaseVariant> PurchaseVariant { get; set; }
        public DbSet<SalesOrderEntity> SalesOrders { get; set; }
        public DbSet<SalesOrderVariantEntity> SalesOrderVariants { get; set; }
        public DbSet<SalesEntity> Sales { get; set; }
        public DbSet<SalesVariantEntity> SalesVariants { get; set; }
        public DbSet<SalesSummaryEntity> SalesSummary { get; set; }
        public DbSet<ActivityEntry> ActivityLog { get; set; }
        public DbSet<LabelLayoutEntity> LabelLayouts { get; set; }
        public DbSet<LabelSheetUseEntity> LabelSheetUse { get; set; }
        public DbSet<StockChangeEntity> StockChanges { get; set; }
        public DbSet<StockChangeLineEntity> StockChangeLines { get; set; }
        public DbSet<StockChangeReasonEntity> StockChangeReasons { get; set; }

        /// <summary>Batch 1c-3: when set (by an import), the next save writes this one activity-log line instead of one per record.</summary>
        public string? ActivityNote { get; set; }

        /// <summary>
        /// If a save fails (for example a duplicate ID refused by the database), undo the pending changes in memory.
        /// A context may be used again after a failed save, so without this the failed change
        /// would stay queued and make every later save on the same context fail too.
        /// </summary>
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
            catch (DbUpdateException)
            {
                RejectPendingChanges();
                throw;
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            try
            {
                return base.SaveChanges(acceptAllChangesOnSuccess);
            }
            catch (DbUpdateException)
            {
                RejectPendingChanges();
                throw;
            }
        }

        private void RejectPendingChanges()
        {
            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                    case EntityState.Modified:
                    case EntityState.Deleted:
                        entry.CurrentValues.SetValues(entry.OriginalValues);
                        entry.State = EntityState.Unchanged;
                        break;
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Users are no longer seeded here. Passwords are stored hashed; see Authentication/UserBootstrap.cs
            // for the one-time upgrade of existing rows and creation of the first admin on an empty database.

            // Business IDs must be unique (batch 5). Empty (NULL) IDs are allowed more than once by SQLite,
            // so the few old blank rows do not block these rules.
            // Voucher numbers per day are NOT made unique yet: old data has duplicates (decision pinned, see README.md and ROADMAP.md).
            modelBuilder.Entity<ProductEntity>()
                        .HasIndex(p => p.ProductId)
                        .IsUnique();

            modelBuilder.Entity<CustomerEntity>()
                        .HasIndex(c => c.CustomerId)
                        .IsUnique();

            modelBuilder.Entity<SupplierEntity>()
                        .HasIndex(s => s.SupplierId)
                        .IsUnique();

            modelBuilder.Entity<VariantEntity>()
                        .HasIndex(v => new { v.ProductEntityId, v.VariantId })
                        .IsUnique();

            // Role names are unique ignoring case; the migration recreates this index with COLLATE NOCASE (same pattern as the IDs above).
            modelBuilder.Entity<RoleEntity>()
                        .HasIndex(r => r.Name)
                        .IsUnique();

            modelBuilder.Entity<RolePermissionEntity>()
                        .HasKey(p => new { p.RoleId, p.Permission });

            modelBuilder.Entity<RoleEntity>()
                        .HasMany(r => r.Permissions)
                        .WithOne()
                        .HasForeignKey(p => p.RoleId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserRoleEntity>()
                        .HasKey(u => new { u.UserId, u.RoleId });

            modelBuilder.Entity<UserRoleEntity>()
                        .HasOne<UserEntity>().WithMany()
                        .HasForeignKey(u => u.UserId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserRoleEntity>()
                        .HasOne<RoleEntity>().WithMany()
                        .HasForeignKey(u => u.RoleId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PairRuleEntity>()
                        .HasIndex(p => new { p.PermissionA, p.PermissionB })
                        .IsUnique();

            // Stage L: named label set-ups, names unique ignoring capitals; the first one, "A4 65 Label", comes with the upgrade.
            modelBuilder.Entity<LabelLayoutEntity>().Property(l => l.Name).UseCollation("NOCASE");
            modelBuilder.Entity<LabelLayoutEntity>().HasIndex(l => l.Name).IsUnique();
            // Batch L-2: labels already used on the current sheet, one row per label set-up (goes when the set-up is deleted).
            modelBuilder.Entity<LabelSheetUseEntity>().ToTable("LabelSheetUse").HasKey(u => u.LabelLayoutId);
            modelBuilder.Entity<LabelSheetUseEntity>().HasOne<LabelLayoutEntity>().WithOne().HasForeignKey<LabelSheetUseEntity>(u => u.LabelLayoutId).OnDelete(DeleteBehavior.Cascade);
            var a4 = LabelSheetLayout.A4Sheet65;
            modelBuilder.Entity<LabelLayoutEntity>().HasData(new LabelLayoutEntity
            {
                Id = 1, Name = a4.Name, IsActive = true, PageWidthMm = a4.PageWidthMm, PageHeightMm = a4.PageHeightMm,
                Columns = a4.Columns, Rows = a4.Rows, LabelWidthMm = a4.LabelWidthMm, LabelHeightMm = a4.LabelHeightMm,
                GapAcrossMm = a4.GapAcrossMm, GapDownMm = a4.GapDownMm, TopMm = a4.TopMm, LeftMm = a4.LeftMm,
                ContentDownMm = a4.ContentDownMm, ContentLeftMm = a4.ContentLeftMm, QrMm = a4.QrMm,
                NameFontPt = a4.NameFontPt, VariantFontPt = a4.VariantFontPt, IdFontPt = a4.IdFontPt,
            });

            // Batch 1c-3: the activity log is read per record (the panel) and by date (the log screen).
            modelBuilder.Entity<ActivityEntry>().ToTable("ActivityLog");
            modelBuilder.Entity<ActivityEntry>().HasIndex(a => new { a.RecordType, a.RecordId });
            modelBuilder.Entity<ActivityEntry>().HasIndex(a => a.At);

            // Stage 2, batch 2-1: stock is added up per variant from these two columns; an index holding both
            // lets the database add up without reading every line (about 35 times faster on a copy of real data).
            // It takes the place of the plain variant index.
            modelBuilder.Entity<PurchaseVariant>().HasIndex(p => new { p.VariantEntityId, p.Quantity });
            modelBuilder.Entity<SalesVariantEntity>().HasIndex(s => new { s.VariantEntityId, s.Quantity });

            // Stage 2, batch 2-1: stock changes (damage, repairs, returns, corrections) beside purchases and sales.
            modelBuilder.Entity<StockChangeEntity>().Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
            modelBuilder.Entity<StockChangeEntity>().Property(s => s.Status).HasConversion<string>().HasMaxLength(10);
            modelBuilder.Entity<StockChangeEntity>().Property(s => s.Date).HasConversion<DateOnlyConverter>();
            modelBuilder.Entity<StockChangeEntity>().HasIndex(s => new { s.Kind, s.Date });
            modelBuilder.Entity<StockChangeEntity>().HasOne(s => s.Customer).WithMany().HasForeignKey(s => s.CustomerEntityId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockChangeEntity>().HasOne(s => s.Supplier).WithMany().HasForeignKey(s => s.SupplierEntityId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockChangeEntity>().HasIndex(s => s.PurchaseEntityId);
            modelBuilder.Entity<StockChangeEntity>()
                        .HasMany(s => s.Lines)
                        .WithOne(l => l.StockChange)
                        .HasForeignKey(l => l.StockChangeEntityId)
                        .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StockChangeLineEntity>().Property(l => l.From).HasConversion<string>().HasMaxLength(10);
            modelBuilder.Entity<StockChangeLineEntity>().Property(l => l.To).HasConversion<string>().HasMaxLength(10);
            modelBuilder.Entity<StockChangeLineEntity>().HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductEntityId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockChangeLineEntity>().HasOne(l => l.ProductVariant).WithMany().HasForeignKey(l => l.VariantEntityId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StockChangeLineEntity>().HasIndex(l => l.SourceLineId);
            modelBuilder.Entity<StockChangeLineEntity>().HasIndex(l => l.PurchaseVariantId);
            modelBuilder.Entity<StockChangeReasonEntity>().Property(r => r.Name).UseCollation("NOCASE");
            modelBuilder.Entity<StockChangeReasonEntity>().HasIndex(r => r.Name).IsUnique();
            // A starting list (Hiten keeps it on the reasons list in Settings, batch 2-7).
            modelBuilder.Entity<StockChangeReasonEntity>().HasData(StockReasons.Starting.Select((name, i) =>
                new StockChangeReasonEntity { Id = i + 1, Name = name, SortOrder = i + 1, IsActive = true }));

            modelBuilder.Entity<PurchaseOrderEntity>()
                        .Property(p => p.Date)
                        .HasConversion<DateOnlyConverter>();

            modelBuilder.Entity<PurchaseOrderEntity>()
                        .Property(p => p.DueDate)
                        .HasConversion<DateOnlyConverter>();

            modelBuilder.Entity<PurchaseEntity>()
                       .Property(p => p.Date)
                       .HasConversion<DateOnlyConverter>();

            modelBuilder.Entity<SalesOrderEntity>()
                        .Property(p => p.DueDate)
                        .HasConversion<DateOnlyConverter>();

            modelBuilder.Entity<SalesOrderEntity>()
                        .Property(p => p.Date)
                        .HasConversion<DateOnlyConverter>();

            modelBuilder.Entity<SalesEntity>()
                        .Property(p => p.Date)
                        .HasConversion<DateOnlyConverter>();

            modelBuilder.Entity<CategoryEntity>()
                        .HasMany(p => p.Products)
                        .WithOne(p => p.Category);

            modelBuilder.Entity<ProductEntity>()
                        .HasMany(p => p.Variants)
                        .WithOne(v => v.Product)
                        .HasForeignKey(k => k.ProductEntityId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VariantEntity>()
                        .HasOne(v => v.Image)
                        .WithOne(i => i.Variant)
                        .HasForeignKey<Image>(k => k.VariantEntityId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerEntity>()
                        .HasMany(c => c.Mobiles)
                        .WithOne(m => m.Customer)
                        .HasForeignKey(k => k.CustomerEntityId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SupplierEntity>()
                        .HasMany(s => s.Purchases)
                        .WithOne(p => p.Supplier)
                        .HasForeignKey(k => k.SupplierEntityId)
                        .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplierEntity>()
                        .HasMany(c => c.Mobiles)
                        .WithOne(m => m.Supplier)
                        .HasForeignKey(k => k.SupplierEntityId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductEntity>()
                      .HasMany(p => p.PurchaseVariants)
                      .WithOne(v => v.Product)
                      .HasForeignKey(k => k.ProductEntityId)
                      .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<VariantEntity>()
                      .HasMany(v => v.PurchaseVariants)
                      .WithOne(p => p.ProductVariant)
                      .HasForeignKey(k => k.VariantEntityId)
                      .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PurchaseEntity>()
                        .HasMany(p => p.PurchaseVariants)
                        .WithOne(v => v.Purchase)
                        .HasForeignKey(k => k.PurchaseEntityId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerEntity>()
                        .HasMany(c => c.SalesOrders)
                        .WithOne(s => s.Customer)
                        .HasForeignKey(k => k.CustomerEntityId)
                        .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SalesOrderEntity>()
                        .HasMany(s => s.SalesOrderVariants)
                        .WithOne(v => v.SalesOrder)
                        .HasForeignKey(k => k.SalesOrderEntityId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductEntity>()
                        .HasMany(p => p.SalesOrderVariants)
                        .WithOne(v => v.Product)
                        .HasForeignKey(k => k.ProductEntityId)
                        .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<VariantEntity>()
                        .HasMany(v => v.SalesOrderVariants)
                        .WithOne(p => p.ProductVariant)
                        .HasForeignKey(k => k.VariantEntityId)
                        .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SalesEntity>()
                      .HasMany(s => s.SalesVariants)
                      .WithOne(v => v.Sale)
                      .HasForeignKey(k => k.SalesEntityId)
                      .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalesEntity>()
                    .HasMany(s => s.SalesSummaries)
                    .WithOne(v => v.Sale)
                    .HasForeignKey(k => k.SalesEntityId)
                    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductEntity>()
                       .HasMany(p => p.SalesVariants)
                       .WithOne(v => v.Product)
                       .HasForeignKey(k => k.ProductEntityId)
                       .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<VariantEntity>()
                        .HasMany(v => v.SalesVariants)
                        .WithOne(p => p.ProductVariant)
                        .HasForeignKey(k => k.VariantEntityId)
                        .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<CustomerEntity>()
                        .HasMany(c => c.Sales)
                        .WithOne(s => s.Customer)
                        .HasForeignKey(k => k.CustomerEntityId)
                        .OnDelete(DeleteBehavior.NoAction);

        }
    }
}
