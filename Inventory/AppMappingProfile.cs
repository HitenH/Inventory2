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
        }
    }
}
