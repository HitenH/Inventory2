namespace Inventory.Domain.Entities
{
    /// <summary>
    /// Version 3.0, batch 1c-3: one line of the activity log. Written only by the app (Domain/ActivityLogWriter.cs
    /// at every save, Service/ActivityLogService.cs for logins, backups and imports); never edited.
    /// Lines written by one save share a <see cref="SaveId"/>, so the panel can show them together.
    /// </summary>
    public class ActivityEntry
    {
        public long Id { get; set; }
        public DateTime At { get; set; }
        /// <summary>The person logged in; null = the app itself (or a refused login).</summary>
        public string? By { get; set; }
        public Guid SaveId { get; set; }
        /// <summary>Created, Changed, Deleted, Login, Login refused, Backup restored, Backup deleted, Backup folder, Import.</summary>
        public string Action { get; set; } = string.Empty;
        /// <summary>The record this line belongs to (a purchase line belongs to its purchase): entity name, e.g. PurchaseEntity.</summary>
        public string? RecordType { get; set; }
        /// <summary>That record's key as text; null for actions not about one record.</summary>
        public string? RecordId { get; set; }
        /// <summary>"Purchase 14 of 25-11-2026" – readable even after the record is deleted.</summary>
        public string? RecordLabel { get; set; }
        public string Text { get; set; } = string.Empty;
        /// <summary>"purchase" or "sale" when the line shows a rate, amount or discount; hidden from people without that switch.</summary>
        public string? RateSide { get; set; }
        /// <summary>Batch 1c-4: a "line added" line (or its rates) that the panel may fold under a summary ("100 lines added (show lines)").</summary>
        public bool IsLineDetail { get; set; }
        /// <summary>The quantity of an added line, so a folded summary can say how many pieces.</summary>
        public int? Pieces { get; set; }
    }
}
