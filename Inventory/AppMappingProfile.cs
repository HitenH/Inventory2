using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Models;

namespace Inventory
{
    public class AppMappingProfile: Profile
    {
        public AppMappingProfile()
        {
            CreateMap<CustomerEntity, CustomerModel>().ReverseMap();
            CreateMap<SupplierEntity, SupplierModel>().ReverseMap();
            CreateMap<ProductEntity, ProductModel>().ReverseMap();
            CreateMap<CategoryEntity, CategoryModel>().ReverseMap();
            CreateMap<VariantEntity, VariantModel>().ReverseMap();
            CreateMap<PurchaseOrderEntity, PurchaseOrderModel>().ReverseMap();
        }
    }
}
