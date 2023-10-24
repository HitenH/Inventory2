using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace Inventory.Domain
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options):base(options) { }

        public DbSet<UserEntity> Users { get; set; }
        public DbSet<CustomerEntity> Customers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>().HasData(new List<UserEntity>()
            {
                new UserEntity(){ Id = 1, Name = "Hiten", Password = "Hiten", Role = "User"},
                new UserEntity(){ Id = 2, Name = "Admin", Password = "admin", Role = "Admin"}
            });
        }
    }
}
