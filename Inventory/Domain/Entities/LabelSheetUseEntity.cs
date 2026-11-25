namespace Inventory.Domain.Entities
{
    /// <summary>
    /// Version 3.0, stage L (Hiten, 25 Nov 2026): how many labels are already used on the current sheet of one label set-up,
    /// so the next print starts above them. Kept in the database (not the browser) so it survives restarts and backups.
    /// Not stamped and not in the activity log as a change (the prints themselves are logged).
    /// </summary>
    public class LabelSheetUseEntity
    {
        public int LabelLayoutId { get; set; }
        public int Used { get; set; }
    }
}
