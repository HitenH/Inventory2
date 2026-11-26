using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Globalization;

namespace Inventory.Domain;

/// <summary>
/// Version 3.0, batch 1c-3: turns the changes of one save into activity-log lines in plain words, with
/// before → after values ("Line AJ0120101/G: Quantity 5 → 3"). Runs inside every save (from
/// <see cref="RecordStampInterceptor"/>, after the stamps and the clash check), and the lines are saved in the
/// same save, so the log and the data can never disagree.
/// A line of a voucher, a variant, a photo, a phone number, a role's switch or a user's role is logged against
/// the record it belongs to. A record created in this save gets one "Created" line (its lines are not listed
/// one by one: the record itself shows them). Lines showing a rate, amount or discount carry the side
/// ("purchase" or "sale") so they can be hidden from people who may not see those rates.
/// </summary>
public static class ActivityLogWriter
{
    public const string PurchaseSide = "purchase";
    public const string SaleSide = "sale";

    private sealed record Field(string Name, string Label, string? Rate = null);

    private static readonly Dictionary<Type, Field[]> RecordFields = new()
    {
        [typeof(CustomerEntity)] = [new("CustomerId", "Customer ID"), new("Name", "Name"), new("ContactPerson", "Contact person"), new("Address", "Address"), new("Area", "Area"), new("Remarks", "Remarks")],
        [typeof(SupplierEntity)] = [new("SupplierId", "Supplier ID"), new("Name", "Name"), new("ContactPerson", "Contact person"), new("Address", "Address"), new("Area", "Area"), new("Remarks", "Remarks")],
        [typeof(ProductEntity)] = [new("ProductId", "Product ID"), new("Name", "Name"), new("Description", "Description"), new("CategoryId", "Category"), new("Rate", "Rate", SaleSide)],
        [typeof(CategoryEntity)] = [new("CategoryId", "Category ID"), new("Name", "Name"), new("Description", "Description")],
        [typeof(PurchaseOrderEntity)] = [new("SupplierId", "Supplier"), new("ProductId", "Product"), new("ProductName", "Product name"), new("VariantId", "Variant"), new("Quantity", "Quantity"), new("Date", "Date"), new("DueDate", "Due date"), new("OrderStatus", "Status"), new("Remarks", "Remarks"), new("ProductRate", "Rate", PurchaseSide)],
        [typeof(PurchaseEntity)] = [new("SupplierEntityId", "Supplier"), new("Date", "Date"), new("VoucherId", "Voucher"), new("Remarks", "Remarks"), new("DiscountAmount", "Discount", PurchaseSide)],
        [typeof(SalesOrderEntity)] = [new("CustomerEntityId", "Customer"), new("Date", "Date"), new("DueDate", "Due date"), new("VoucherId", "Voucher"), new("OrderStatus", "Status"), new("DiscountAmount", "Discount", SaleSide)],
        [typeof(SalesEntity)] = [new("CustomerEntityId", "Customer"), new("Date", "Date"), new("VoucherId", "Voucher"), new("Remarks", "Remarks"), new("DiscountAmount", "Discount", SaleSide)],
        [typeof(RoleEntity)] = [new("Name", "Name")],
        [typeof(UserEntity)] = [new("Name", "Name")],
        [typeof(LabelLayoutEntity)] = [new("Name", "Name"), new("IsActive", "In use"), new("PageWidthMm", "Page width mm"), new("PageHeightMm", "Page height mm"),
            new("Columns", "Labels across"), new("Rows", "Labels down"), new("LabelWidthMm", "Label width mm"), new("LabelHeightMm", "Label height mm"),
            new("GapAcrossMm", "Gap across mm"), new("GapDownMm", "Gap down mm"), new("TopMm", "Top margin mm"), new("LeftMm", "Left margin mm"),
            new("ContentDownMm", "Content down mm"), new("ContentLeftMm", "Content left mm"), new("QrMm", "QR size mm"),
            new("NameFontPt", "Name text pt"), new("VariantFontPt", "Variant text pt"), new("IdFontPt", "ID text pt")],
        // Stage 2: a sales return, a damage record ... (StockChangeEntity.Kind says which).
        [typeof(StockChangeEntity)] = [new("CustomerEntityId", "Customer"), new("SupplierEntityId", "Supplier"), new("Date", "Date"), new("VoucherId", "Voucher"), new("Remarks", "Remarks"), new("Status", "Status")],
    };

    private static readonly Dictionary<Type, Field[]> LineFields = new()
    {
        [typeof(PurchaseVariant)] = [new("Quantity", "Quantity"), new("ProductRate", "Rate", PurchaseSide), new("DiscountPercent", "Discount %", PurchaseSide)],
        [typeof(SalesOrderVariantEntity)] = [new("Quantity", "Quantity"), new("Remarks", "Remarks"), new("ProductRate", "Rate", SaleSide), new("DiscountPercent", "Discount %", SaleSide)],
        [typeof(SalesVariantEntity)] = [new("Quantity", "Quantity")],
        [typeof(SalesSummaryEntity)] = [new("Quantity", "Quantity"), new("ProductRate", "Rate", SaleSide), new("DiscountPercent", "Discount %", SaleSide)],
        [typeof(StockChangeLineEntity)] = [new("Quantity", "Quantity"), new("Reason", "Reason"), new("ReturnSupplierEntityId", "Return supplier"), new("ReadyToSend", "Ready to send")],
    };

    private static readonly Field[] VariantFields = [new("VariantId", "Variant ID"), new("Name", "Name"), new("ReorderLevel", "Reorder level")];

    public static List<ActivityEntry> Describe(DbContext context, string? who, DateTime now, SeenVersions? seen = null)
    {
        var saveId = Guid.NewGuid();
        var result = new List<ActivityEntry>();
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && e.Entity is not ActivityEntry)
            .ToList();
        if (entries.Count == 0)
        {
            return result;
        }

        // An import writes one summary line instead of one per imported record.
        if (context is AppDbContext { ActivityNote: { } note })
        {
            result.Add(new ActivityEntry { At = now, By = who, SaveId = saveId, Action = "Import", Text = note });
            return result;
        }

        var names = new Names(context);
        // Owner, 25 Nov 2026: changes to an existing record in one sitting on its screen share one group
        // (SeenVersions.SittingFor); a record created or deleted in this save keeps this save's own group.
        var sittings = new Dictionary<object, Guid>(ReferenceEqualityComparer.Instance);
        Guid GroupOf(EntityEntry record)
        {
            if (seen is null || record.State is EntityState.Added or EntityState.Deleted)
            {
                return saveId;
            }

            if (!sittings.TryGetValue(record.Entity, out var group))
            {
                group = seen.SittingFor(SeenVersions.KeyOf(record), now);
                sittings[record.Entity] = group;
            }

            return group;
        }

        void Add(EntityEntry record, string action, string text, string? rate = null, bool detail = false, int? pieces = null) => result.Add(new ActivityEntry
        {
            At = now, By = who, SaveId = GroupOf(record), Action = action, Text = text, RateSide = rate, IsLineDetail = detail, Pieces = pieces,
            RecordType = record.Metadata.ClrType.Name,
            RecordId = KeyText(record),
            RecordLabel = Label(record, names),
        });

        var parts = entries.Where(e => e.Entity is not IStamped)
            .Select(e => (Entry: e, Owner: RecordStamps.FindOwner(context, e)))
            .Where(p => p.Owner is not null)
            .Select(p => (p.Entry, Owner: context.Entry(p.Owner!)))
            .ToList();

        foreach (var record in entries.Where(e => e.Entity is IStamped))
        {
            var mine = parts.Where(p => ReferenceEquals(p.Owner.Entity, record.Entity)).Select(p => p.Entry).ToList();
            switch (record.State)
            {
                case EntityState.Added:
                    Add(record, "Created", CreatedText(record, mine, names));
                    if (record.Entity is PurchaseOrderEntity { ProductRate: > 0 } order)
                    {
                        Add(record, "Created", $"Rate {Num(order.ProductRate)}", PurchaseSide);
                    }
                    else if (record.Entity is ProductEntity { Rate: > 0 } product)
                    {
                        Add(record, "Created", $"Rate {Num(product.Rate)}", SaleSide);
                    }
                    else if (record.Entity is StockChangeEntity { Kind: StockChangeKind.Damage } damage && DamageNote(damage, mine, names) is { } damageNote)
                    {
                        // Stage 2: damage recorded on a purchase or a sales return shows in that record's history too.
                        result.Add(new ActivityEntry
                        {
                            At = now, By = who, SaveId = saveId, Action = "Changed", Text = damageNote.Text,
                            RecordType = damageNote.RecordType, RecordId = damageNote.RecordId.ToString(), RecordLabel = damageNote.Label,
                        });
                    }
                    break;
                case EntityState.Deleted:
                    Add(record, "Deleted", DeletedText(record, mine, names));
                    break;
                case EntityState.Modified:
                    if (record.Entity is UserEntity)
                    {
                        if (IsChanged(record, nameof(UserEntity.Password)))
                        {
                            Add(record, "Changed", "Password set");
                        }

                        if (IsChanged(record, nameof(UserEntity.IsDisabled)))
                        {
                            Add(record, "Changed", ((UserEntity)record.Entity).IsDisabled ? "Disabled" : "Enabled");
                        }
                    }

                    foreach (var field in RecordFields.GetValueOrDefault(record.Metadata.ClrType, []))
                    {
                        if (IsChanged(record, field.Name))
                        {
                            Add(record, "Changed", $"{field.Label}: {Show(record.Property(field.Name).OriginalValue, field.Name, names)} → {Show(record.Property(field.Name).CurrentValue, field.Name, names)}", field.Rate);
                        }
                    }
                    break;
            }
        }

        // Parts of records that are not themselves created or deleted in this save.
        foreach (var group in parts.Where(p => p.Owner.State is not (EntityState.Added or EntityState.Deleted)).GroupBy(p => p.Owner.Entity, ReferenceEqualityComparer.Instance))
        {
            var owner = group.First().Owner;
            // Lines added to a record this tab is still creating are part of creating it (owner, 25 Nov 2026).
            var action = seen?.IsCreating(SeenVersions.KeyOf(owner)) == true ? "Created" : "Changed";
            foreach (var line in PartLines(group.Select(p => p.Entry).ToList(), names, owner.Entity))
            {
                Add(owner, action, line.Text, line.Rate, line.Detail, line.Pieces);
            }
        }

        return result;
    }

    /// <summary>What changed among the parts of one record, matching a removed and re-added line as one changed line.</summary>
    private sealed record PartLine(string Text, string? Rate, bool Detail = false, int? Pieces = null);

    private static IEnumerable<PartLine> PartLines(List<EntityEntry> parts, Names names, object owner)
    {
        foreach (var byType in parts.GroupBy(p => p.Metadata.ClrType))
        {
            var type = byType.Key;
            var list = byType.ToList();
            if (LineFields.TryGetValue(type, out var fields))
            {
                foreach (var line in PairUp(list, LineKey))
                {
                    // Stage 2: on a sales return, a line given in exchange (Sellable -> Outside) is named as such.
                    var noun = type == typeof(SalesSummaryEntity) ? "Summary line"
                        : owner is StockChangeEntity { Kind: StockChangeKind.SalesReturn } && (line.New ?? line.Old)!.Entity is StockChangeLineEntity { To: StockState.Outside } ? "Exchange line"
                        : "Line";
                    if (line.Old is null)
                    {
                        var added = line.New!;
                        yield return new PartLine($"{noun} added: {LineCode(added, names)}, quantity {Show(added.Property("Quantity").CurrentValue, "Quantity", names)}", null, true, (int?)added.Property("Quantity").CurrentValue);
                        var rateFields = fields.Where(f => f.Rate is not null).ToList();
                        if (rateFields.Any(f => !IsZero(added.Property(f.Name).CurrentValue)))
                        {
                            yield return new PartLine($"{noun} {LineCode(added, names)}: " + string.Join(", ", rateFields.Select(f => $"{f.Label} {Show(added.Property(f.Name).CurrentValue, f.Name, names)}")), rateFields[0].Rate, true);
                        }
                    }
                    else if (line.New is null)
                    {
                        var removed = line.Old;
                        yield return new PartLine($"{noun} removed: {LineCode(removed, names, original: true)}, quantity {Show(removed.Property("Quantity").OriginalValue, "Quantity", names)}", null);
                    }
                    else
                    {
                        var (before, after) = (line.Old, line.New);
                        var sameEntry = ReferenceEquals(before, after);
                        var code = LineCode(after, names);
                        if (sameEntry && (IsChanged(after, "ProductEntityId") || (after.Metadata.FindProperty("VariantEntityId") is not null && IsChanged(after, "VariantEntityId"))))
                        {
                            yield return new PartLine($"{noun} {LineCode(before, names, original: true)} changed to {code}", null);
                        }

                        foreach (var field in fields)
                        {
                            var old = sameEntry ? before.Property(field.Name).OriginalValue : before.Property(field.Name).OriginalValue;
                            var now = after.Property(field.Name).CurrentValue;
                            if (!Same(old, now, after.Property(field.Name).Metadata.ClrType))
                            {
                                yield return new PartLine($"{noun} {code}: {field.Label} {Show(old, field.Name, names)} → {Show(now, field.Name, names)}", field.Rate);
                            }
                        }
                    }
                }
            }
            else if (type == typeof(VariantEntity))
            {
                foreach (var v in list)
                {
                    var variant = (VariantEntity)v.Entity;
                    if (v.State == EntityState.Added)
                    {
                        yield return new PartLine($"Variant added: {variant.VariantId} ({variant.Name})", null);
                    }
                    else if (v.State == EntityState.Deleted)
                    {
                        yield return new PartLine($"Variant removed: {v.Property("VariantId").OriginalValue} ({v.Property("Name").OriginalValue})", null);
                    }
                    else
                    {
                        foreach (var field in VariantFields.Where(f => IsChanged(v, f.Name)))
                        {
                            yield return new PartLine($"Variant {variant.VariantId}: {field.Label} {Show(v.Property(field.Name).OriginalValue, field.Name, names)} → {Show(v.Property(field.Name).CurrentValue, field.Name, names)}", null);
                        }
                    }
                }
            }
            else if (type == typeof(Image))
            {
                foreach (var i in list)
                {
                    var variantId = (Guid?)(i.State == EntityState.Deleted ? i.Property("VariantEntityId").OriginalValue : i.Property("VariantEntityId").CurrentValue);
                    var of = $"variant {names.VariantCode(variantId)}";
                    if (i.State == EntityState.Added)
                    {
                        yield return new PartLine($"Photo added ({of})", null);
                    }
                    else if (i.State == EntityState.Deleted)
                    {
                        yield return new PartLine($"Photo removed ({of})", null);
                    }
                    else if (IsChanged(i, nameof(Image.ImageData)) || IsChanged(i, nameof(Image.ImageTitle)))
                    {
                        yield return new PartLine($"Photo changed ({of})", null);
                    }
                }
            }
            else if (type == typeof(Mobile))
            {
                foreach (var pair in PairUp(list, m => (string?)(m.State == EntityState.Deleted ? m.Property("Phone").OriginalValue : m.Property("Phone").CurrentValue)))
                {
                    if (pair.Old is null)
                    {
                        yield return new PartLine($"Mobile added: {((Mobile)pair.New!.Entity).Phone}", null);
                    }
                    else if (pair.New is null)
                    {
                        yield return new PartLine($"Mobile removed: {pair.Old.Property("Phone").OriginalValue}", null);
                    }
                    else if (ReferenceEquals(pair.Old, pair.New) && IsChanged(pair.New, "Phone"))
                    {
                        yield return new PartLine($"Mobile: {pair.New.Property("Phone").OriginalValue} → {pair.New.Property("Phone").CurrentValue}", null);
                    }
                }
            }
            else if (type == typeof(UserRoleEntity))
            {
                foreach (var pair in PairUp(list, r => Value(r, "RoleId")?.ToString()))
                {
                    var role = names.RoleName((int?)Value(pair.New ?? pair.Old!, "RoleId"));
                    if (pair.Old is null)
                    {
                        var until = ((UserRoleEntity)pair.New!.Entity).ExpiresAt;
                        yield return new PartLine($"Role {role} given" + (until is { } u ? $" until {u:dd-MM-yyyy}" : ""), null);
                    }
                    else if (pair.New is null)
                    {
                        yield return new PartLine($"Role {role} removed", null);
                    }
                    else
                    {
                        var oldEnd = (DateTime?)pair.Old.Property("ExpiresAt").OriginalValue;
                        var newEnd = (DateTime?)pair.New.Property("ExpiresAt").CurrentValue;
                        if (oldEnd != newEnd)
                        {
                            yield return new PartLine($"Role {role}: end date {EndDate(oldEnd)} → {EndDate(newEnd)}", null);
                        }
                    }
                }
            }
            else if (type == typeof(RolePermissionEntity))
            {
                foreach (var pair in PairUp(list, p => (string?)Value(p, "Permission")))
                {
                    if (pair.Old is null)
                    {
                        yield return new PartLine($"Switch on: {Inventory.Service.Permissions.Describe((string)Value(pair.New!, "Permission")!)}", null);
                    }
                    else if (pair.New is null)
                    {
                        yield return new PartLine($"Switch off: {Inventory.Service.Permissions.Describe((string)Value(pair.Old, "Permission")!)}", null);
                    }
                }
            }
        }
    }

    /// <summary>"Damage recorded (Damage 1 of 25-11-2026): AJ0120103 1 NO HAATICHAP / G 1 (Broken in transit)", filed under its purchase or sales return.</summary>
    private static (string Text, string RecordType, Guid RecordId, string Label)? DamageNote(StockChangeEntity damage, List<EntityEntry> parts, Names names)
    {
        // Hiten, 26 Nov 2026: damage can be lowered too ("not damaged after all"), so each line says which way it went.
        var lines = parts.Where(p => p.Entity is StockChangeLineEntity && p.State == EntityState.Added)
            .Select(p => (StockChangeLineEntity)p.Entity)
            .Select(l => l.To == StockState.Damaged
                ? $"{LineCode(parts.First(p => ReferenceEquals(p.Entity, l)), names)} {l.Quantity} damaged" + (string.IsNullOrEmpty(l.Reason) ? "" : $" ({l.Reason})")
                : $"{LineCode(parts.First(p => ReferenceEquals(p.Entity, l)), names)} {l.Quantity} not damaged after all")
            .ToList();
        var text = $"Damage recorded ({StockChangeLabel(damage.Kind, damage.VoucherId, damage.Date)}): " + string.Join("; ", lines);
        if (damage.PurchaseEntityId is { } purchase)
        {
            return (text, nameof(PurchaseEntity), purchase, names.PurchaseLabel(purchase));
        }

        return damage.SourceStockChangeId is { } source ? (text, nameof(StockChangeEntity), source, names.StockChangeLabel(source)) : null;
    }

    /// <summary>"Sales return", "Damage" ... - what the shop calls each kind of stock change.</summary>
    public static string KindName(StockChangeKind kind) => kind switch
    {
        StockChangeKind.Damage => "Damage",
        StockChangeKind.Repaired => "Repair",
        StockChangeKind.WriteOff => "Write-off",
        StockChangeKind.CountCorrection => "Count correction",
        StockChangeKind.OpeningStock => "Opening stock",
        StockChangeKind.SalesReturn => "Sales return",
        StockChangeKind.PurchaseReturn => "Purchase return",
        _ => kind.ToString(),
    };

    /// <summary>"Sales return 3 of 25-11-2026".</summary>
    public static string StockChangeLabel(StockChangeKind kind, int voucher, DateOnly date) =>
        $"{KindName(kind)} {voucher} of {date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}";

    private static string EndDate(DateTime? end) => end is { } d ? d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) : "none";

    /// <summary>
    /// Matches each removed entry with an added one of the same key (a screen that replaces all lines at once),
    /// so only real differences are logged. A changed entry is paired with itself.
    /// </summary>
    private static List<(EntityEntry? Old, EntityEntry? New)> PairUp(List<EntityEntry> list, Func<EntityEntry, string?> key)
    {
        var result = new List<(EntityEntry?, EntityEntry?)>();
        var removed = list.Where(e => e.State == EntityState.Deleted).ToList();
        foreach (var e in list.Where(e => e.State == EntityState.Modified))
        {
            result.Add((e, e));
        }

        foreach (var added in list.Where(e => e.State == EntityState.Added))
        {
            var match = removed.FirstOrDefault(r => key(r) == key(added));
            if (match is not null)
            {
                removed.Remove(match);
            }

            result.Add((match, added));
        }

        result.AddRange(removed.Select(r => ((EntityEntry?)r, (EntityEntry?)null)));
        return result;
    }

    private static string? LineKey(EntityEntry line) => line.Entity is SalesSummaryEntity
        ? $"{Value(line, "ProductEntityId")}|{Value(line, "SerialNumber")}"
        : $"{Value(line, "ProductEntityId")}|{Value(line, "VariantEntityId")}";

    /// <summary>"AJ0120101 1 NO FRAME (NS-9) / G" – product code and name, and variant code.</summary>
    private static string LineCode(EntityEntry line, Names names, bool original = false)
    {
        object? V(string p) => original || line.State == EntityState.Deleted ? line.Property(p).OriginalValue : line.Property(p).CurrentValue;
        if (line.Entity is SalesSummaryEntity)
        {
            return V("ProductId") is string code ? $"{code} {V("ProductName")}".TrimEnd() : names.ProductLabel((Guid?)V("ProductEntityId"));
        }

        return $"{names.ProductLabel((Guid?)V("ProductEntityId"))} / {names.VariantCode((Guid?)V("VariantEntityId"))}";
    }

    private static string CreatedText(EntityEntry record, List<EntityEntry> parts, Names names)
    {
        string Lines()
        {
            var lines = parts.Where(p => LineFields.ContainsKey(p.Metadata.ClrType) && p.Entity is not SalesSummaryEntity && p.State == EntityState.Added).ToList();
            if (lines.Count == 0)
            {
                return "";
            }

            var pieces = lines.Sum(l => Convert.ToInt64(l.Property("Quantity").CurrentValue ?? 0));
            return $", {lines.Count} line{(lines.Count == 1 ? "" : "s")}, {pieces} piece{(pieces == 1 ? "" : "s")}";
        }

        return record.Entity switch
        {
            CustomerEntity c => $"Created: {c.Name} ({c.CustomerId})",
            SupplierEntity s => $"Created: {s.Name} ({s.SupplierId})",
            ProductEntity p => $"Created: {p.Name} ({p.ProductId})" + (parts.Count(x => x.Entity is VariantEntity) is var n and > 0 ? $", {n} variant{(n == 1 ? "" : "s")}" : ""),
            CategoryEntity c => $"Created: {c.Name}",
            PurchaseOrderEntity o => $"Created: {o.ProductId}/{o.VariantId}, quantity {o.Quantity}, supplier {o.SupplierId}",
            PurchaseEntity p => $"Created: supplier {names.SupplierName(p.SupplierEntityId)}" + Lines(),
            SalesOrderEntity o => $"Created: customer {names.CustomerName(o.CustomerEntityId)}" + Lines(),
            SalesEntity s => $"Created: customer {names.CustomerName(s.CustomerEntityId)}" + Lines(),
            RoleEntity r => "Created" + (r.Permissions.Count > 0 ? ": " + string.Join(", ", r.Permissions.Select(k => Inventory.Service.Permissions.Describe(k.Permission))) : ""),
            StockChangeEntity { Kind: StockChangeKind.SalesReturn } s => $"Created: customer {names.CustomerName(s.CustomerEntityId)}" + Lines(),
            StockChangeEntity { Kind: StockChangeKind.PurchaseReturn } s => $"Created: supplier {names.SupplierName(s.SupplierEntityId)}" + Lines(),
            StockChangeEntity { Kind: StockChangeKind.Damage, PurchaseEntityId: { } p } => $"Created: damage on {names.PurchaseLabel(p)}" + Lines(),
            StockChangeEntity { Kind: StockChangeKind.Damage, SourceStockChangeId: { } r } => $"Created: damage on {names.StockChangeLabel(r)}" + Lines(),
            StockChangeEntity s => $"Created: {KindName(s.Kind).ToLowerInvariant()}" + Lines(),
            _ => "Created",
        };
    }

    private static string DeletedText(EntityEntry record, List<EntityEntry> parts, Names names)
    {
        var lines = parts.Where(p => LineFields.ContainsKey(p.Metadata.ClrType) && p.Entity is not SalesSummaryEntity && p.State == EntityState.Deleted).ToList();
        var count = lines.Count == 0 ? ""
            : $", {lines.Count} line{(lines.Count == 1 ? "" : "s")}, {lines.Sum(l => Convert.ToInt64(l.Property("Quantity").OriginalValue ?? 0))} pieces";
        return record.Entity switch
        {
            PurchaseEntity => $"Deleted: supplier {names.SupplierName((Guid?)record.Property("SupplierEntityId").OriginalValue)}" + count,
            SalesOrderEntity or SalesEntity => $"Deleted: customer {names.CustomerName((Guid?)record.Property("CustomerEntityId").OriginalValue)}" + count,
            PurchaseOrderEntity => $"Deleted: {Value(record, "ProductId")}/{Value(record, "VariantId")}, quantity {Value(record, "Quantity")}, supplier {Value(record, "SupplierId")}",
            StockChangeEntity { Kind: StockChangeKind.SalesReturn } => $"Deleted: customer {names.CustomerName((Guid?)record.Property("CustomerEntityId").OriginalValue)}" + count,
            StockChangeEntity { Kind: StockChangeKind.PurchaseReturn } => $"Deleted: supplier {names.SupplierName((Guid?)record.Property("SupplierEntityId").OriginalValue)}" + count,
            StockChangeEntity => "Deleted" + count,
            _ => "Deleted",
        };
    }

    /// <summary>"Purchase 14 of 25-11-2026" – a record named the way the shop names it.</summary>
    private static string Label(EntityEntry record, Names names)
    {
        object? V(string p) => Value(record, p);
        string Day(string p) => V(p) is DateOnly d ? d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) : "";
        return record.Entity switch
        {
            CustomerEntity => $"Customer {V("Name")} ({V("CustomerId")})",
            SupplierEntity => $"Supplier {V("Name")} ({V("SupplierId")})",
            ProductEntity => $"Product {V("Name")} ({V("ProductId")})",
            CategoryEntity => $"Category {V("Name")}",
            PurchaseOrderEntity => $"Purchase order {V("ProductId")}/{V("VariantId")} ({V("SupplierId")})",
            PurchaseEntity => $"Purchase {V("VoucherId")} of {Day("Date")}",
            SalesOrderEntity => $"Sales order {V("VoucherId")} of {Day("Date")}",
            SalesEntity => $"Sale {V("VoucherId")} of {Day("Date")}",
            UserEntity => $"User {V("Name")}",
            RoleEntity => $"Role {V("Name")}",
            LabelLayoutEntity => $"Label settings {V("Name")}",
            StockChangeEntity s => StockChangeLabel(s.Kind, s.VoucherId, s.Date),
            _ => record.Metadata.ClrType.Name,
        };
    }

    /// <summary>The record's key as text, or null while the database has not given it one yet (a new user or role).</summary>
    private static string? KeyText(EntityEntry record)
    {
        var key = record.Metadata.FindPrimaryKey()!.Properties[0];
        var property = record.Property(key.Name);
        return property.IsTemporary ? null : Convert.ToString(property.CurrentValue, CultureInfo.InvariantCulture);
    }

    private static object? Value(EntityEntry e, string property) =>
        e.State == EntityState.Deleted ? e.Property(property).OriginalValue : e.Property(property).CurrentValue;

    private static bool IsChanged(EntityEntry e, string property)
    {
        var p = e.Property(property);
        return p.IsModified && !Same(p.OriginalValue, p.CurrentValue, p.Metadata.ClrType);
    }

    /// <summary>Blank text and no text are the same; 5 and 5.00 are the same.</summary>
    /// <summary>
    /// Equal for the shop: blank text and no text; 5 and 5.00; and an empty amount (rate, discount) and 0, which the
    /// screens use for the same thing (owner saw "Discount: (blank) → 0" after an Update, 25 Nov 2026).
    /// </summary>
    private static bool Same(object? before, object? after, Type type)
    {
        if (Nullable.GetUnderlyingType(type) == typeof(decimal))
        {
            before ??= 0m;
            after ??= 0m;
        }

        return Equals(Normalise(before), Normalise(after));
    }

    private static object? Normalise(object? value) => value switch
    {
        string s when string.IsNullOrWhiteSpace(s) => null,
        decimal d => d / 1.000000000000000000000000000000000m,
        _ => value,
    };

    private static bool IsZero(object? value) => value is null || Convert.ToDecimal(value, CultureInfo.InvariantCulture) == 0;

    private static string Num(decimal? value) => value is { } d ? d.ToString("0.##", CultureInfo.InvariantCulture) : "(blank)";

    private static string Show(object? value, string field, Names names) => value switch
    {
        null => "(blank)",
        string s when string.IsNullOrWhiteSpace(s) => "(blank)",
        decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        bool b => b ? "yes" : "no",
        DateOnly d => d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
        Guid g when field is "SupplierEntityId" or "ReturnSupplierEntityId" => names.SupplierName(g),
        Guid g when field == "CustomerEntityId" => names.CustomerName(g),
        Guid g when field == "CategoryId" => names.CategoryName(g),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    /// <summary>Codes and names for keys, from this save's own records first, then the database (cached per save).</summary>
    private sealed class Names(DbContext context)
    {
        private readonly Dictionary<string, string> cache = new();

        private string Get<T>(object? id, Func<T, string?> pick, Func<IQueryable<T>, string?> query) where T : class
        {
            if (id is null)
            {
                return "(none)";
            }

            var key = typeof(T).Name + ":" + id;
            if (cache.TryGetValue(key, out var known))
            {
                return known;
            }

            var tracked = context.ChangeTracker.Entries<T>().FirstOrDefault(e => Equals(e.Property("Id").CurrentValue, id))?.Entity;
            var text = (tracked is not null ? pick(tracked) : query(context.Set<T>().AsNoTracking())) ?? "?";
            cache[key] = text;
            return text;
        }

        /// <summary>"AJ0120101 1 NO FRAME (NS-9)" – code and name (owner, 25 Nov 2026), one small lookup per product per save.</summary>
        public string ProductLabel(Guid? id) => Get<ProductEntity>(id, p => $"{p.ProductId} {p.Name}".TrimEnd(), q => q.Where(p => p.Id == id).Select(p => p.ProductId + " " + p.Name).FirstOrDefault());
        public string VariantCode(Guid? id) => Get<VariantEntity>(id, v => v.VariantId, q => q.Where(v => v.Id == id).Select(v => v.VariantId).FirstOrDefault());
        public string SupplierName(Guid? id) => Get<SupplierEntity>(id, s => s.Name, q => q.Where(s => s.Id == id).Select(s => s.Name).FirstOrDefault());
        public string CustomerName(Guid? id) => Get<CustomerEntity>(id, c => c.Name, q => q.Where(c => c.Id == id).Select(c => c.Name).FirstOrDefault());
        public string CategoryName(Guid? id) => Get<CategoryEntity>(id, c => c.Name, q => q.Where(c => c.Id == id).Select(c => c.Name).FirstOrDefault());
        public string RoleName(int? id) => Get<RoleEntity>(id, r => r.Name, q => q.Where(r => r.Id == id).Select(r => r.Name).FirstOrDefault());

        /// <summary>"Purchase 12 of 25-11-2026".</summary>
        public string PurchaseLabel(Guid? id) => Get<PurchaseEntity>(id, PurchaseText,
            q => q.Where(p => p.Id == id).AsEnumerable().Select(PurchaseText).FirstOrDefault());

        /// <summary>"Sales return 3 of 25-11-2026".</summary>
        public string StockChangeLabel(Guid? id) => Get<StockChangeEntity>(id, s => ActivityLogWriter.StockChangeLabel(s.Kind, s.VoucherId, s.Date),
            q => q.Where(s => s.Id == id).AsEnumerable().Select(s => ActivityLogWriter.StockChangeLabel(s.Kind, s.VoucherId, s.Date)).FirstOrDefault());

        private static string PurchaseText(PurchaseEntity p) => $"Purchase {p.VoucherId} of {p.Date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}";
    }
}
