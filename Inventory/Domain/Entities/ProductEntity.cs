﻿using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class ProductEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal? Rate { get; set; }
        public string? Description { get; set; }
        public CategoryEntity? Categoty { get; set; }
        public List<VariantEntity>? Variants { get; set; } = new();
    }
}
