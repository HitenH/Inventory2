using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;

namespace Inventory.Service
{
    public class ProductService
    {
        private readonly IVariantRepository variantRepository;
        public VariantEntity productVariant { get; set; }

        public ProductService(IVariantRepository variantRepository)
        {
            this.variantRepository = variantRepository;
        }

        //public async Task AddProductVariantQuantity(ProductEntity product, Guid productVariantId, int productVariantQuantity)
        //{
        //    if (product != null && product.Variants.Count >0)
        //    {
        //        productVariant = await variantRepository.GetById(productVariantId);
        //        if (productVariant != null)
        //        {
        //            productVariant.StockInHand += productVariantQuantity;
        //            await variantRepository.Update(productVariant);
        //        }    
        //    }
        //}
        //public async Task SubtractionProductVariantQuantity(ProductEntity product, Guid productVariantId, int productVariantQuantity)
        //{
        //    if (product != null && product.Variants.Count > 0)
        //    {
        //        productVariant = await variantRepository.GetById(productVariantId);
        //        if (productVariant != null)
        //        {
        //            productVariant.StockInHand -= productVariantQuantity;
        //            await variantRepository.Update(productVariant);
        //        }      
        //    }
        //}
    }
}
