using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>Stage 3: where a move stands.</summary>
public enum MoveState
{
    /// <summary>No lines yet.</summary>
    Empty,
    /// <summary>Two-step move: sent, not yet confirmed as received.</summary>
    OnTheWay,
    /// <summary>Two-step move: received, and pieces sent but not received (and not written off) – a red flag (Hiten).</summary>
    Missing,
    /// <summary>Two-step move: every piece received (or the missing ones written off).</summary>
    Received,
    /// <summary>One-step move or starting count: counted in the new place at once.</summary>
    Moved,
}

/// <summary>Stage 3: one line of the Moves list.</summary>
public sealed record MoveListItem(Guid Id, int VoucherId, DateOnly Date, StockChangeKind Kind, int FromPlaceId, string From, int ToPlaceId, string To,
    int Lines, int Sent, int Received, int Missing, MoveState State, StockChangeStatus Status, string? Remarks);

/// <summary>Stage 3: one line of a two-step move with how much of it came.</summary>
public sealed record MoveLineProgress(Guid LineId, int SerialNumber, Guid ProductEntityId, Guid VariantEntityId, string ProductId, string ProductName,
    string VariantId, int Sent, int Received, int WrittenOff)
{
    /// <summary>Sent but neither received nor written off.</summary>
    public int Outstanding => Sent - Received - WrittenOff;
}

/// <summary>Stage 3: an item found from a scanned label (or picked) for a transfer's list.</summary>
public sealed record TransferItem(Guid ProductEntityId, Guid VariantEntityId, string ProductId, string ProductName, string VariantId, string VariantName);

/// <summary>Stage 3, batch 3-5: a purchase line offered by "Send to shop" (already sent = that item sent on from this purchase so far).</summary>
public sealed record PurchaseSendLine(Guid LineId, int SerialNumber, string ProductId, string ProductName, string VariantId, int CameIn, int AlreadySent);

/// <summary>Stage 3, batch 3-6: a variant whose stock in one place is below zero (sold before the move that brings it was recorded).</summary>
public sealed record BelowZeroItem(Guid ProductEntityId, string ProductId, string ProductName, string VariantId, string VariantName, int PlaceId, string Place, int Qty);

/// <summary>
/// Version 3.0, stage 3, batches 3-5 and 3-6 (Hiten, 26 Nov 2026): moving stock between places, and receiving it.
/// A move is a stock change entry from one place (<see cref="StockChangeEntity.PlaceId"/>) to another
/// (<see cref="StockChangeEntity.ToPlaceId"/>). With the transit switch on (two steps, the default) each line sends pieces
/// "on the way" (Sellable there → InTransit); the receiving place confirms what came (InTransit → Sellable there), and what
/// did not come is a red flag until it is received late or written off. With the switch off – and always for a starting
/// count (all stock started in the shop; the first count of the godown moves it there) – lines move at once.
/// Moves have their own approval switch (off); a waiting move cannot be received until approved.
/// </summary>
public class MoveService(IDbContextFactory<AppDbContext> factory, ActivityLogService activity)
{
    public const string TransitSetting = "MovesInTransit";
    public const string ApprovalSetting = "MoveApproval";
    public const string MissingReason = "Missing on the way";

    private static readonly StockChangeKind[] MoveKinds = [StockChangeKind.Move, StockChangeKind.MoveAtOnce, StockChangeKind.StartingCount];

    public static bool IsMove(StockChangeKind kind) => MoveKinds.Contains(kind);

    // ---------------------------------------------------------------- the two switches (Admin only)

    /// <summary>Two steps (sent, on the way, received): on unless the Admin switched it off (Hiten: "toggle it off and it will not appear anywhere").</summary>
    public static async Task<bool> TransitOnAsync(AppDbContext db) =>
        await db.AppSettings.AsNoTracking().Where(s => s.Key == TransitSetting).Select(s => s.Value).FirstOrDefaultAsync() != "off";

    /// <summary>Moves wait for approval: off unless the Admin switched it on (Hiten: its own switch, off).</summary>
    public static async Task<bool> ApprovalOnAsync(AppDbContext db) =>
        await db.AppSettings.AsNoTracking().Where(s => s.Key == ApprovalSetting).Select(s => s.Value).FirstOrDefaultAsync() == "on";

    public async Task<(bool Transit, bool Approval)> SwitchesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return (await TransitOnAsync(db), await ApprovalOnAsync(db));
    }

    public Task SetTransitAsync(bool on, UserAccess access) =>
        SetSwitchAsync(TransitSetting, on, access, on ? "Stock transfers in two steps (on the way, received): switched on" : "Stock transfers in two steps (on the way, received): switched off");

    public Task SetApprovalAsync(bool on, UserAccess access) =>
        SetSwitchAsync(ApprovalSetting, on, access, on ? "Stock transfers need approval: switched on" : "Stock transfers need approval: switched off");

    private async Task SetSwitchAsync(string key, bool on, UserAccess access, string logText)
    {
        if (!access.HasAdminRole)
        {
            throw new StockChangeRefused("Only the Admin can change this switch.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var setting = await db.AppSettings.FindAsync(key);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSettingEntity { Key = key, Value = on ? "on" : "off" });
        }
        else
        {
            setting.Value = on ? "on" : "off";
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync("Setting", logText, access.Name);
    }

    // ---------------------------------------------------------------- reading

    /// <summary>The Moves list, newest first: only moves from or to the places in <paramref name="places"/> (null = every place).</summary>
    public async Task<List<MoveListItem>> ListAsync(IReadOnlySet<int>? places = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var names = await db.Places.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name);
        var moves = await db.StockChanges.AsNoTracking().Include(s => s.Lines)
            .Where(s => MoveKinds.Contains(s.Kind))
            .ToListAsync();
        var writtenOff = await WrittenOffAsync(db, moves.SelectMany(m => m.Lines).Select(l => l.Id).ToList());
        return moves
            .Where(m => places is null || places.Contains(m.PlaceId ?? 0) || places.Contains(m.ToPlaceId ?? 0))
            .Select(m =>
            {
                var progress = Progress(m, writtenOff, products: null);
                var sent = SentLines(m).Sum(l => l.Quantity);
                var received = m.Kind == StockChangeKind.Move ? progress.Sum(p => p.Received) : sent;
                return new MoveListItem(m.Id, m.VoucherId, m.Date, m.Kind, m.PlaceId ?? 0, names.GetValueOrDefault(m.PlaceId ?? 0, "?"),
                    m.ToPlaceId ?? 0, names.GetValueOrDefault(m.ToPlaceId ?? 0, "?"), SentLines(m).Count(), sent, received,
                    StateOf(m, progress) == MoveState.Missing ? progress.Sum(p => p.Outstanding) : 0, StateOf(m, progress), m.Status, m.Remarks);
            })
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.VoucherId)
            .ToList();
    }

    /// <summary>A move with its lines (and their products), or null. Remembers the version opened (edit clash).</summary>
    public async Task<StockChangeEntity?> GetAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var found = await db.StockChanges
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(s => s.Lines).ThenInclude(l => l.Product)
            .Include(s => s.Lines).ThenInclude(l => l.ProductVariant)
            .FirstOrDefaultAsync(s => s.Id == id && MoveKinds.Contains(s.Kind));
        if (found is not null)
        {
            db.MarkSeen(found);
        }

        return found;
    }

    /// <summary>Each sent line of a move with what was received and written off.</summary>
    public async Task<List<MoveLineProgress>> ProgressAsync(StockChangeEntity move)
    {
        await using var db = await factory.CreateDbContextAsync();
        var writtenOff = await WrittenOffAsync(db, move.Lines.Select(l => l.Id).ToList());
        return Progress(move, writtenOff, products: true);
    }

    public static MoveState StateOf(StockChangeEntity move, IReadOnlyList<MoveLineProgress> progress)
    {
        if (SentLines(move).Count == 0)
        {
            return MoveState.Empty;
        }

        if (move.Kind != StockChangeKind.Move)
        {
            return MoveState.Moved;
        }

        if (move.ReceivedAt is null)
        {
            return MoveState.OnTheWay;
        }

        return progress.Any(p => p.Outstanding > 0) ? MoveState.Missing : MoveState.Received;
    }

    /// <summary>How many moves to the places in <paramref name="places"/> have pieces missing (the red count in the menu).</summary>
    public async Task<int> MissingCountAsync(IReadOnlySet<int>? places = null) =>
        (await ListAsync(places)).Count(m => m.State == MoveState.Missing);

    /// <summary>Two-step moves on the way to the places in <paramref name="places"/> (null = every place).</summary>
    public async Task<int> ArrivingCountAsync(IReadOnlySet<int>? places = null) =>
        (await ListAsync(places)).Count(m => m.State == MoveState.OnTheWay && m.Status == StockChangeStatus.Approved && (places is null || places.Contains(m.ToPlaceId)));

    /// <summary>
    /// Hiten, 26 Nov 2026: selling with 0 in the shop is allowed (goods fetched from the godown in a hurry); the place then shows
    /// below zero until the move is recorded. This lists every such variant and place, in the places the person sees.
    /// </summary>
    public async Task<List<BelowZeroItem>> BelowZeroAsync(IReadOnlySet<int>? places = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var byPlace = await StockQuery.SellableByPlaceAsync(db, null);
        var below = byPlace.SelectMany(v => v.Value.Where(p => p.Value < 0 && (places is null || places.Contains(p.Key))).Select(p => (Variant: v.Key, Place: p.Key, Qty: p.Value))).ToList();
        if (below.Count == 0)
        {
            return [];
        }

        var ids = below.Select(b => b.Variant).Distinct().ToList();
        var variants = await db.Variants.AsNoTracking().Include(v => v.Product).Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        var names = await db.Places.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name);
        return below.Where(b => variants.ContainsKey(b.Variant))
            .Select(b =>
            {
                var v = variants[b.Variant];
                // Old data has a few lines whose item has no product: named as such rather than left blank.
                return new BelowZeroItem(v.ProductEntityId ?? Guid.Empty, string.IsNullOrWhiteSpace(v.Product?.ProductId) ? "(no product ID)" : v.Product!.ProductId!,
                    v.Product?.Name ?? "", v.VariantId ?? "", v.Name ?? "", b.Place, names.GetValueOrDefault(b.Place, "?"), b.Qty);
            })
            .OrderBy(b => b.Qty).ThenBy(b => b.ProductName).ThenBy(b => b.VariantId)
            .ToList();
    }

    // ---------------------------------------------------------------- making a transfer (batch 3-5)

    /// <summary>Saves date and remarks. The places are changed by the Admin only (<see cref="ChangePlacesAsync"/>).</summary>
    public async Task UpdateAsync(Guid id, DateOnly date, string? remarks)
    {
        await using var db = await factory.CreateDbContextAsync();
        var move = await MoveAsync(db, id);
        move.Date = date;
        move.Remarks = Clean(remarks);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Hiten, 26 Nov 2026: only the Admin changes the places of a saved move; its lines move with it. Refused once anything was
    /// received (the receiving place has counted it).
    /// </summary>
    public async Task ChangePlacesAsync(Guid id, int fromPlaceId, int toPlaceId, UserAccess access)
    {
        if (!access.HasAdminRole)
        {
            throw new StockChangeRefused(PlaceService.OnlyAdminChangesPlace);
        }

        await using var db = await factory.CreateDbContextAsync();
        await CheckPlacesAsync(db, fromPlaceId, toPlaceId);
        var move = await MoveAsync(db, id);
        if (move.PlaceId == fromPlaceId && move.ToPlaceId == toPlaceId)
        {
            return;
        }

        if (move.ReceivedAt is not null || ReceivedLines(move).Any())
        {
            throw new StockChangeRefused("Goods of this transfer were already received, so its places cannot change.");
        }

        move.PlaceId = fromPlaceId;
        move.ToPlaceId = toPlaceId;
        foreach (var line in move.Lines)
        {
            line.FromPlaceId = line.From == StockState.Sellable ? fromPlaceId : null;
            line.ToPlaceId = toPlaceId;
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var move = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id && MoveKinds.Contains(s.Kind));
        if (move is null)
        {
            return;
        }

        if (move.ReceivedAt is not null || ReceivedLines(move).Any() || await AnyWrittenOffAsync(db, move.Lines.Select(l => l.Id).ToList()))
        {
            throw new StockChangeRefused("Goods of this transfer were already received or written off, so it cannot be deleted.");
        }

        db.StockChanges.Remove(move);
        await db.SaveChangesAsync();
    }

    /// <summary>A product and variant found from a scanned (or typed) label.</summary>
    public async Task<TransferItem?> FindItemAsync(string? productCode, string? variantCode)
    {
        var product = (productCode ?? "").Trim().ToLower();
        var variant = (variantCode ?? "").Trim().ToLower();
        if (product.Length == 0 || variant.Length == 0)
        {
            return null;
        }

        await using var db = await factory.CreateDbContextAsync();
        return await db.Variants.AsNoTracking()
            .Where(v => v.Product.ProductId != null && v.Product.ProductId.ToLower() == product && v.VariantId != null && v.VariantId.ToLower() == variant)
            .Select(v => new TransferItem(v.ProductEntityId!.Value, v.Id, v.Product.ProductId!, v.Product.Name ?? "", v.VariantId!, v.Name ?? ""))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Hiten, 26 Nov 2026: "after scanning or entering the data, it should confirm by at least having a send button to click to
    /// officially save it". A new transfer is a list on the screen until Send; then the whole transfer – header and every line –
    /// is saved in one go and gets its number. Needs "Send stock transfers". Two steps (on the way) while the transit switch is
    /// on; a starting count always at once; waits for approval when stock transfers need approval.
    /// </summary>
    public async Task<StockChangeEntity> SendAsync(int fromPlaceId, int toPlaceId, DateOnly date, string? remarks, bool startingCount,
        IReadOnlyList<(Guid ProductId, Guid VariantId, int Quantity, string? Note)> lines, UserAccess access, Guid? purchaseId = null)
    {
        if (!access.Allows(Permissions.StockTransferSend))
        {
            throw new StockChangeRefused("You may not send stock transfers (switch \"Send stock transfers\").");
        }

        if (lines.Count == 0)
        {
            throw new StockChangeRefused("Nothing to send yet: scan or add the items first.");
        }

        foreach (var l in lines)
        {
            if (LineProblem(l.ProductId, l.VariantId, l.Quantity) is { } problem)
            {
                throw new StockChangeRefused(problem);
            }
        }

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await CheckPlacesAsync(db, fromPlaceId, toPlaceId);
        var kind = startingCount ? StockChangeKind.StartingCount : await TransitOnAsync(db) ? StockChangeKind.Move : StockChangeKind.MoveAtOnce;
        var move = new StockChangeEntity
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Date = date,
            VoucherId = await NextVoucherAsync(db, date),
            Status = await ApprovalOnAsync(db) ? StockChangeStatus.Waiting : StockChangeStatus.Approved,
            PlaceId = fromPlaceId,
            ToPlaceId = toPlaceId,
            PurchaseEntityId = purchaseId,
            Remarks = Clean(remarks),
        };
        var serial = 1;
        foreach (var l in lines)
        {
            move.Lines.Add(SentLine(move, l.ProductId, l.VariantId, l.Quantity, serial++, l.Note));
        }

        db.StockChanges.Add(move);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return move;
    }

    /// <summary>
    /// "Send to shop" on a purchase (Hiten, 26 Nov 2026): a transfer from the place the purchase arrived in to where sales leave
    /// from, with a line for each purchase line given more than 0 pieces. Linked to the purchase.
    /// </summary>
    public async Task<StockChangeEntity> SendFromPurchaseAsync(Guid purchaseId, IReadOnlyList<(Guid PurchaseLineId, int Quantity)> send, UserAccess access, string? remarks = null)
    {
        var chosen = send.Where(s => s.Quantity != 0).ToList();
        if (chosen.Any(s => s.Quantity < 0))
        {
            throw new StockChangeRefused("The quantity to send cannot be below 0.");
        }

        if (chosen.Count == 0)
        {
            throw new StockChangeRefused("Type how many to send on at least one line.");
        }

        int from, to;
        List<PurchaseVariant> purchaseLines;
        string label;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var purchase = await db.Purchases.AsNoTracking().Include(p => p.PurchaseVariants).FirstOrDefaultAsync(p => p.Id == purchaseId)
                ?? throw new StockChangeRefused("This purchase was not found.");
            from = purchase.PlaceId;
            to = await PlaceSettings.SalesPlaceAsync(db);
            purchaseLines = purchase.PurchaseVariants ?? [];
            label = $"From purchase {purchase.VoucherId} of {purchase.Date:dd-MM-yyyy}";
            foreach (var s in chosen)
            {
                var line = purchaseLines.FirstOrDefault(l => l.Id == s.PurchaseLineId) ?? throw new StockChangeRefused("A line of this purchase is no longer there.");
                if (s.Quantity > (line.Quantity ?? 0))
                {
                    throw new StockChangeRefused($"Only {line.Quantity} came in on line {line.SerialNumber}; more cannot be sent on.");
                }
            }
        }

        return await SendAsync(from, to, DateOnly.FromDateTime(DateTime.Now), remarks ?? label, startingCount: false,
            chosen.Select(s =>
            {
                var line = purchaseLines.First(l => l.Id == s.PurchaseLineId);
                return (line.ProductEntityId, line.VariantEntityId, s.Quantity, (string?)null);
            }).ToList(), access, purchaseId);
    }

    /// <summary>"Send to shop": the lines of a purchase with how many came in and how many of that item were already sent on from it.</summary>
    public async Task<List<PurchaseSendLine>> PurchaseSendLinesAsync(Guid purchaseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var lines = await db.PurchaseVariant.AsNoTracking().Include(l => l.Product).Include(l => l.ProductVariant)
            .Where(l => l.PurchaseEntityId == purchaseId).OrderBy(l => l.SerialNumber).ToListAsync();
        var sent = await db.StockChangeLines.AsNoTracking()
            .Where(l => l.StockChange!.PurchaseEntityId == purchaseId && MoveKinds.Contains(l.StockChange.Kind) && l.From == StockState.Sellable
                        && l.StockChange.Status != StockChangeStatus.Rejected)
            .GroupBy(l => l.VariantEntityId).Select(g => new { g.Key, Qty = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(g => g.Key, g => g.Qty);
        return lines.Select(l => new PurchaseSendLine(l.Id, l.SerialNumber, l.Product?.ProductId ?? "", l.Product?.Name ?? "", l.ProductVariant?.VariantId ?? "",
            l.Quantity ?? 0, sent.GetValueOrDefault(l.VariantEntityId))).ToList();
    }

    // ---------------------------------------------------------------- receiving (batch 3-6)

    /// <summary>
    /// The receiving place confirms what came (Hiten, 26 Nov 2026). Each received piece goes into stock there; pieces sent but
    /// not received are missing – a red flag until they are received later or written off. Can be confirmed again later
    /// for pieces that turn up. A move waiting for approval cannot be received.
    /// </summary>
    public async Task ReceiveAsync(Guid id, IReadOnlyList<(Guid SentLineId, int Quantity)> received, UserAccess access)
    {
        if (!access.Allows(Permissions.StockTransferReceive))
        {
            throw new StockChangeRefused("You may not receive stock transfers (switch \"Receive stock transfers\").");
        }

        var who = access.Name;
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var move = await MoveAsync(db, id);
        if (move.Kind != StockChangeKind.Move)
        {
            throw new StockChangeRefused("This transfer was counted in the new place at once; there is nothing to receive.");
        }

        if (move.Status != StockChangeStatus.Approved)
        {
            throw new StockChangeRefused("This transfer is waiting for approval (or was rejected), so it cannot be received yet.");
        }

        var progress = Progress(move, await WrittenOffAsync(db, move.Lines.Select(l => l.Id).ToList()), products: null);
        var serial = move.Lines.Count == 0 ? 1 : move.Lines.Max(l => l.SerialNumber) + 1;
        foreach (var r in received.Where(r => r.Quantity != 0))
        {
            var line = progress.FirstOrDefault(p => p.LineId == r.SentLineId) ?? throw new StockChangeRefused("A line of this transfer is no longer there.");
            if (r.Quantity < 0)
            {
                throw new StockChangeRefused("A received quantity cannot be below 0.");
            }

            if (r.Quantity > line.Outstanding)
            {
                throw new StockChangeRefused($"Line {line.SerialNumber}: only {line.Outstanding} {(line.Outstanding == 1 ? "piece is" : "pieces are")} still to come, not {r.Quantity}.");
            }

            db.StockChangeLines.Add(new StockChangeLineEntity
            {
                Id = Guid.NewGuid(),
                StockChangeEntityId = move.Id,
                SerialNumber = serial++,
                ProductEntityId = line.ProductEntityId,
                VariantEntityId = line.VariantEntityId,
                Quantity = r.Quantity,
                From = StockState.InTransit,
                To = StockState.Sellable,
                ToPlaceId = move.ToPlaceId,
                SourceLineId = line.LineId,
            });
        }

        if (move.ReceivedAt is null)
        {
            move.ReceivedAt = DateTime.Now;
            move.ReceivedBy = who;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>
    /// The pieces still missing on a move are written off (lost on the way): one write-off entry, linked to the move, that
    /// waits for approval when the stock approval switch is on. Returns it.
    /// </summary>
    public async Task<StockChangeEntity> WriteOffMissingAsync(Guid id, string? note, UserAccess access)
    {
        if (!access.Allows(Permissions.StockTransferWriteOff))
        {
            throw new StockChangeRefused("You may not write off missing pieces (switch \"Write off pieces missing on a stock transfer\", Admin by default).");
        }

        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var move = await MoveAsync(db, id);
        var progress = Progress(move, await WrittenOffAsync(db, move.Lines.Select(l => l.Id).ToList()), products: null);
        if (StateOf(move, progress) != MoveState.Missing)
        {
            throw new StockChangeRefused("Nothing is missing on this transfer.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var reason = $"{MissingReason} ({ActivityLogWriter.StockChangeLabel(move.Kind, move.VoucherId, move.Date)})";
        var writeOff = new StockChangeEntity
        {
            Id = Guid.NewGuid(),
            Kind = StockChangeKind.WriteOff,
            Date = today,
            VoucherId = await StockChangeService.NextVoucherAsync(db, StockChangeKind.WriteOff, today),
            Status = await StockChangeService.NewStatusAsync(db),
            SourceStockChangeId = move.Id,   // keeps it off the Stock changes list; it belongs to the move
            Reason = reason,
            Remarks = Clean(note),
        };
        var serial = 1;
        foreach (var line in progress.Where(p => p.Outstanding > 0))
        {
            writeOff.Lines.Add(new StockChangeLineEntity
            {
                Id = Guid.NewGuid(),
                SerialNumber = serial++,
                ProductEntityId = line.ProductEntityId,
                VariantEntityId = line.VariantEntityId,
                Quantity = line.Outstanding,
                From = StockState.InTransit,
                To = StockState.Outside,
                Reason = reason,
                SourceLineId = line.LineId,
            });
        }

        db.StockChanges.Add(writeOff);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return writeOff;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>The lines that send pieces (all lines of a one-step move; the outgoing lines of a two-step move).</summary>
    public static List<StockChangeLineEntity> SentLines(StockChangeEntity move) =>
        move.Lines.Where(l => l.From == StockState.Sellable).OrderBy(l => l.SerialNumber).ToList();

    private static IEnumerable<StockChangeLineEntity> ReceivedLines(StockChangeEntity move) =>
        move.Lines.Where(l => l.From == StockState.InTransit);

    private static List<MoveLineProgress> Progress(StockChangeEntity move, IReadOnlyDictionary<Guid, int> writtenOff, bool? products)
    {
        var received = ReceivedLines(move).GroupBy(l => l.SourceLineId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.Sum(l => l.Quantity));
        return SentLines(move).Select(l => new MoveLineProgress(l.Id, l.SerialNumber, l.ProductEntityId, l.VariantEntityId,
            l.Product?.ProductId ?? "", l.Product?.Name ?? "", l.ProductVariant?.VariantId ?? "", l.Quantity,
            move.Kind == StockChangeKind.Move ? received.GetValueOrDefault(l.Id) : l.Quantity, writtenOff.GetValueOrDefault(l.Id))).ToList();
    }

    /// <summary>Pieces of these move lines written off as missing (approved or waiting).</summary>
    private static async Task<Dictionary<Guid, int>> WrittenOffAsync(AppDbContext db, IReadOnlyCollection<Guid> lineIds)
    {
        if (lineIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await db.StockChangeLines.AsNoTracking()
            .Where(l => l.SourceLineId != null && lineIds.Contains(l.SourceLineId.Value) && l.From == StockState.InTransit && l.To == StockState.Outside
                        && l.StockChange!.Status != StockChangeStatus.Rejected)
            .GroupBy(l => l.SourceLineId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.Quantity) })
            .ToListAsync();
        return rows.ToDictionary(r => r.Key, r => r.Qty);
    }

    private static async Task<bool> AnyWrittenOffAsync(AppDbContext db, IReadOnlyCollection<Guid> lineIds) =>
        (await WrittenOffAsync(db, lineIds)).Count > 0;

    private static StockChangeLineEntity SentLine(StockChangeEntity move, Guid productId, Guid variantId, int quantity, int serialNumber, string? note) => new()
    {
        Id = Guid.NewGuid(),
        StockChangeEntityId = move.Id,
        SerialNumber = serialNumber,
        ProductEntityId = productId,
        VariantEntityId = variantId,
        Quantity = quantity,
        From = StockState.Sellable,
        FromPlaceId = move.PlaceId,
        To = move.Kind == StockChangeKind.Move ? StockState.InTransit : StockState.Sellable,
        ToPlaceId = move.ToPlaceId,   // two-step: where the pieces are going
        Note = Clean(note),
    };

    private static string? LineProblem(Guid productId, Guid variantId, int? quantity)
    {
        if (productId == Guid.Empty || variantId == Guid.Empty)
        {
            return "Select a product and variant before adding a line.";
        }

        return quantity is null or <= 0 ? "The quantity to transfer must be more than 0." : null;
    }

    private static async Task CheckPlacesAsync(AppDbContext db, int fromPlaceId, int toPlaceId)
    {
        if (fromPlaceId == toPlaceId)
        {
            throw new StockChangeRefused("Choose two different places: where the goods leave from and where they go.");
        }

        if (await db.Places.CountAsync(p => p.Id == fromPlaceId || p.Id == toPlaceId) != 2)
        {
            throw new StockChangeRefused("Choose the places first.");
        }
    }

    private static async Task<StockChangeEntity> MoveAsync(AppDbContext db, Guid id)
    {
        var move = await db.StockChanges.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id && MoveKinds.Contains(s.Kind));
        if (move is null)
        {
            db.ThrowIfGoneWhileOpen<StockChangeEntity>(id);
            throw new StockChangeRefused("This transfer was not found.");
        }

        return move;
    }

    /// <summary>Moves are numbered per day across one-step, two-step and starting counts.</summary>
    private static async Task<int> NextVoucherAsync(AppDbContext db, DateOnly date) =>
        (await db.StockChanges.Where(s => MoveKinds.Contains(s.Kind) && s.Date == date).MaxAsync(s => (int?)s.VoucherId) ?? 0) + 1;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
