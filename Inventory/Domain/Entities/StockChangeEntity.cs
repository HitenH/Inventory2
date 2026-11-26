using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    /// <summary>
    /// Version 3.0, stage 2 (Hiten, 25 Nov 2026, "way B"): where pieces of stock are.
    /// <see cref="Outside"/> means outside the shop's stock: came in from outside (a return, opening stock, found more)
    /// or went out of it (written off, sent back to the supplier, found fewer).
    /// Stored as text, so never rename a value once it has shipped.
    /// </summary>
    public enum StockState
    {
        Outside = 0,
        Sellable = 1,
        Damaged = 2,
        /// <summary>Stage 3, batch 3-5: sent from one place, not yet received at the other ("on the way"); not sellable.</summary>
        InTransit = 3,
    }

    /// <summary>Stage 2: the kinds of stock change. Stored as text, so never rename a value once it has shipped.</summary>
    public enum StockChangeKind
    {
        /// <summary>Sellable → Damaged (found on a purchase, a sales return or in the shop).</summary>
        Damage = 0,
        /// <summary>Damaged → Sellable.</summary>
        Repaired = 1,
        /// <summary>Sellable or Damaged → Outside (thrown away or lost).</summary>
        WriteOff = 2,
        /// <summary>Outside → Sellable (found more) or Sellable → Outside (found fewer).</summary>
        CountCorrection = 3,
        /// <summary>Outside → Sellable (or Damaged).</summary>
        OpeningStock = 4,
        /// <summary>A customer brings goods back: Outside → Sellable or Damaged.</summary>
        SalesReturn = 5,
        /// <summary>Goods sent back to a supplier: Damaged or Sellable → Outside.</summary>
        PurchaseReturn = 6,
        /// <summary>
        /// Stage 3, batch 3-5: goods sent from one place to another in two steps (the transit switch on): Sellable in the
        /// sending place → InTransit, then, when received, InTransit → Sellable in the receiving place.
        /// </summary>
        Move = 7,
        /// <summary>Stage 3, batch 3-5: goods moved in one step (the transit switch off): Sellable here → Sellable there.</summary>
        MoveAtOnce = 8,
        /// <summary>
        /// Stage 3, batch 3-5 (Hiten, 26 Nov 2026): all stock started in the shop; what the first count finds in the godown is
        /// moved there in one step, rack by rack.
        /// </summary>
        StartingCount = 9,
    }

    /// <summary>
    /// Stage 2: only an <see cref="Approved"/> stock change moves stock. With the approval switch off (Hiten, 25 Nov 2026)
    /// every stock change is saved as Approved at once; with it on, it waits.
    /// </summary>
    public enum StockChangeStatus
    {
        Approved = 0,
        Waiting = 1,
        Rejected = 2,
    }

    /// <summary>
    /// Version 3.0, stage 2: one stock change entry (a "voucher" of its kind: a damage record, a sales return,
    /// a purchase return, a count correction…). Purchases and sales are never converted or changed by it:
    /// stock = purchased − sold ± the lines of approved stock changes (<see cref="StockQuery"/>).
    /// </summary>
    public class StockChangeEntity : IStamped
    {
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }

        [Key]
        public Guid Id { get; set; }
        public StockChangeKind Kind { get; set; }
        /// <summary>Numbered per kind and date, like the purchase and sale vouchers.</summary>
        public int VoucherId { get; set; }
        public DateOnly Date { get; set; }
        public StockChangeStatus Status { get; set; }

        /// <summary>A sales return's customer.</summary>
        public Guid? CustomerEntityId { get; set; }
        public CustomerEntity? Customer { get; set; }
        /// <summary>A purchase return's supplier.</summary>
        public Guid? SupplierEntityId { get; set; }
        public SupplierEntity? Supplier { get; set; }
        /// <summary>The purchase this damage was recorded on ("Record damage" on a purchase).</summary>
        public Guid? PurchaseEntityId { get; set; }
        /// <summary>The stock change (a sales return) this damage was recorded on.</summary>
        public Guid? SourceStockChangeId { get; set; }

        public string? Remarks { get; set; }
        /// <summary>
        /// Stage 3, batch 3-4: the place this entry's sellable pieces are in (a purchase's damage: the purchase's place; a sales
        /// return: where sales leave from; a repair: where sales leave from; the Stock changes screen: the place chosen).
        /// Its lines take it when they have none of their own (<see cref="Inventory.Domain.StockPlaces"/>). Blank on entries made
        /// before stage 3 (their lines already say the shop).
        /// </summary>
        public int? PlaceId { get; set; }
        /// <summary>Stage 3, batch 3-5: a move's receiving place (<see cref="PlaceId"/> is the sending place).</summary>
        public int? ToPlaceId { get; set; }
        /// <summary>Stage 3, batch 3-6: when and by whom a two-step move was first confirmed as received (pieces not received then are missing).</summary>
        public DateTime? ReceivedAt { get; set; }
        public string? ReceivedBy { get; set; }
        /// <summary>Stage 2, batch 2-7: the reason of an entry made on the Stock changes screen (copied onto each of its lines).</summary>
        public string? Reason { get; set; }
        /// <summary>Who approved or rejected it, and when (blank when it was approved at once because the switch is off).</summary>
        public string? DecidedBy { get; set; }
        public DateTime? DecidedAt { get; set; }

        public List<StockChangeLineEntity> Lines { get; set; } = new();
    }

    /// <summary>
    /// Stage 2: one line of a stock change – so many pieces of one variant moved from one state to another.
    /// The quantity is always above zero; the direction is given by <see cref="From"/> and <see cref="To"/>.
    /// A line with <see cref="To"/> = Damaged is a damaged lot: it stays on the damage report until later lines
    /// (repaired, written off, sent back) that point to it through <see cref="SourceLineId"/> have used it up.
    /// </summary>
    public class StockChangeLineEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid StockChangeEntityId { get; set; }
        public StockChangeEntity? StockChange { get; set; }
        public int SerialNumber { get; set; }
        public Guid ProductEntityId { get; set; }
        public ProductEntity? Product { get; set; }
        public Guid VariantEntityId { get; set; }
        public VariantEntity? ProductVariant { get; set; }
        public int Quantity { get; set; }
        public StockState From { get; set; }
        public StockState To { get; set; }
        /// <summary>Stage 3: when <see cref="From"/> is Sellable, the place the pieces leave (stock is counted per place; damaged and outside have no place).</summary>
        public int? FromPlaceId { get; set; }
        /// <summary>Stage 3: when <see cref="To"/> is Sellable, the place the pieces go into.</summary>
        public int? ToPlaceId { get; set; }
        /// <summary>The reason as chosen at the time (a copy of the text, so changing the reasons list never changes history).</summary>
        public string? Reason { get; set; }
        public string? Note { get; set; }

        /// <summary>The purchase line the damage was found on.</summary>
        public Guid? PurchaseVariantId { get; set; }
        /// <summary>The line this one takes its pieces from: the damaged lot that is repaired, written off or sent back, or the sales return line the damage was found on.</summary>
        public Guid? SourceLineId { get; set; }

        /// <summary>Damaged lot only: the supplier it is to go back to (assigned on the damage report).</summary>
        public Guid? ReturnSupplierEntityId { get; set; }
        /// <summary>Damaged lot only: marked "Ready to send" on the damage report.</summary>
        public bool ReadyToSend { get; set; }
    }

    /// <summary>Stage 2: the list of reasons offered when stock changes (the Admin keeps it). Lines keep their own copy of the text.</summary>
    public class StockChangeReasonEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int SortOrder { get; set; }
        /// <summary>A reason taken off the list stays in the table, so nothing that used it changes.</summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Stage 2, batch 2-8: a setting kept in the database (so it survives restarts, travels with backups and is the same for
    /// everyone), e.g. whether stock changes need approval. Changed only through the app.
    /// </summary>
    public class AppSettingEntity
    {
        [Key]
        public string Key { get; set; } = "";
        public string? Value { get; set; }
    }
}
