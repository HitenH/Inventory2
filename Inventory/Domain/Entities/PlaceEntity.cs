using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    /// <summary>
    /// Version 3.0, stage 3 (Hiten, 26 Nov 2026): a place where stock is counted – the shop, a godown.
    /// Stock is counted per place only ("way A"); racks and levels inside a place are signposts, not counted.
    /// Every purchase, sale and stock change says which place it happened in; entries made before stage 3
    /// belong to the starting place (<see cref="Places.StartingId"/>, the shop).
    /// </summary>
    public class PlaceEntity
    {
        [Key]
        public int Id { get; set; }
        /// <summary>1–99, the first part of an address ("02" in 02-03-B). Unique.</summary>
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public List<RackEntity> Racks { get; set; } = new();
    }

    /// <summary>Stage 3: a rack in a place, numbered 1–99, with levels A (the bottom, Hiten) upwards.</summary>
    public class RackEntity
    {
        [Key]
        public int Id { get; set; }
        public int PlaceId { get; set; }
        public PlaceEntity? Place { get; set; }
        /// <summary>1–99, the middle part of an address ("03" in 02-03-B). Unique within its place.</summary>
        public int Number { get; set; }
        /// <summary>How many levels it has: 3 = levels A, B and C.</summary>
        public int Levels { get; set; }
    }

    /// <summary>
    /// Stage 3: a signpost – "this variant is kept on this rack level" – for pickers and arrangers (Hiten, 26 Nov 2026).
    /// One variant can be on several racks in several places, and one rack level holds many variants. At most one
    /// is the main rack per place (where to look first, where new stock goes). Not a stock count.
    /// </summary>
    public class VariantRackEntity
    {
        [Key]
        public int Id { get; set; }
        public Guid VariantEntityId { get; set; }
        public VariantEntity? Variant { get; set; }
        public int RackId { get; set; }
        public RackEntity? Rack { get; set; }
        /// <summary>One capital letter, A = the bottom level.</summary>
        public string Level { get; set; } = "A";
        public bool IsMain { get; set; }
    }

    /// <summary>
    /// Stage 3, batch 3-3 (Hiten, 26 Nov 2026): a place a user works in and sees ("a godown person sees only godown stock,
    /// a manager both"). A user with no rows works in every place (also places added later); the Admin role always does.
    /// </summary>
    public class UserPlaceEntity
    {
        public int UserId { get; set; }
        public int PlaceId { get; set; }
    }

    public static class Places
    {
        /// <summary>
        /// The starting place, made by the upgrade as "Shop" (number 01): every entry made before stage 3 belongs to it
        /// (Hiten, 26 Nov 2026: all existing stock starts in the shop). It cannot be deleted.
        /// </summary>
        public const int StartingId = 1;

        /// <summary>The godown made by the upgrade (number 02), as Hiten described the shop.</summary>
        public const int GodownId = 2;

        public const int MaxNumber = 99;
        public const int MaxLevels = 26;
    }
}
