using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Migrations
{
    /// <summary>
    /// Removes the seeded user rows from the EF model only. The rows already in the database are kept:
    /// their plain-text passwords are converted to hashes at startup by Authentication/UserBootstrap.
    /// Up and Down are intentionally empty so no user data is deleted or re-inserted.
    /// </summary>
    public partial class RemoveSeededUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: existing users are kept and upgraded in place at startup.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: never re-insert plain-text credentials.
        }
    }
}
