using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class ImageRepositoryEF : SimpleRepositoryEF<Image>, IImageRepository
    {
        public ImageRepositoryEF(IDbContextFactory<AppDbContext> factory) : base(factory, x => x.Id) { }
    }
}
