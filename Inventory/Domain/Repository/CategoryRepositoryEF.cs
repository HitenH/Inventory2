using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class CategoryRepositoryEF : SimpleRepositoryEF<CategoryEntity>, ICategoryRepository
    {
        public CategoryRepositoryEF(IDbContextFactory<AppDbContext> factory) : base(factory, x => x.Id) { }
    }
}
