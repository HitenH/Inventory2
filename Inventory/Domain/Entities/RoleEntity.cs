namespace Inventory.Domain.Entities
{
    /// <summary>A named set of permissions. An Admin role has every permission, including ones added in future.</summary>
    public class RoleEntity : IStamped
    {
        // Version 3.0, batch 1c-1: who and when (filled in by Domain/RecordStamps.cs only).
        public DateTime? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public ICollection<RolePermissionEntity> Permissions { get; set; } = new List<RolePermissionEntity>();
    }

    /// <summary>One permission key (see Service/Permissions.cs) given to a role.</summary>
    public class RolePermissionEntity
    {
        public int RoleId { get; set; }
        public string Permission { get; set; } = string.Empty;
    }

    /// <summary>A role held by a user. ExpiresAt null means permanent; a date means temporary access.</summary>
    public class UserRoleEntity
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>The same person may not do both permissions on the same entry. PermissionA is always smaller (ordinal) than PermissionB.</summary>
    public class PairRuleEntity
    {
        public int Id { get; set; }
        public string PermissionA { get; set; } = string.Empty;
        public string PermissionB { get; set; } = string.Empty;
    }
}
