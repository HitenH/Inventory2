namespace Inventory.Domain.Entities
{
    /// <summary>
    /// Version 3.0, batch 1c-1: who created a record and who last changed it, and when.
    /// Filled in only by <see cref="Inventory.Domain.RecordStamps"/> at save time, never by a screen.
    /// Null on records made before version 3.0 ("not recorded"). A null "By" with a time means the app itself
    /// (for example the start-up steps), not a person.
    /// </summary>
    public interface IStamped
    {
        DateTime? CreatedAt { get; set; }
        string? CreatedBy { get; set; }
        DateTime? ChangedAt { get; set; }
        string? ChangedBy { get; set; }
    }
}
