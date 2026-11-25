namespace Inventory.Domain.Entities
{
    /// <summary>
    /// Version 3.0, stage L (Hiten, 25 Nov 2026): one named label set-up – sheet, label size, margins, and what sits inside a
    /// label – kept in the database (so it is in every backup) instead of in the program. Hiten makes a new one for another
    /// sheet or printer and switches between them; exactly one is in use (<see cref="IsActive"/>). Changes are in the activity log.
    /// The rules for the numbers are in Service/Labels.cs (LabelSheetLayout.Check).
    /// </summary>
    public class LabelLayoutEntity : IStamped
    {
        // Version 3.0, batch 1c-1: who and when (filled in by Domain/RecordStamps.cs only).
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public double PageWidthMm { get; set; }
        public double PageHeightMm { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
        public double LabelWidthMm { get; set; }
        public double LabelHeightMm { get; set; }
        public double GapAcrossMm { get; set; }
        public double GapDownMm { get; set; }
        public double TopMm { get; set; }
        public double LeftMm { get; set; }

        public double ContentDownMm { get; set; }
        public double ContentLeftMm { get; set; }
        public double QrMm { get; set; }
        public double NameFontPt { get; set; }
        public double VariantFontPt { get; set; }
        public double IdFontPt { get; set; }
    }
}
