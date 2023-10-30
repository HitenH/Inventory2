using AutoMapper;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Inventory.Service;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class Supplier
    {
        [Parameter] public string SupplierId { get; set; }

        [Inject] private ISupplierRepository SupplierRepository { get; set; }
        [Inject] private IMobileRepository MobileRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private IMobileService MobileService { get; set; }
        [Inject] private AppDbContext context { get; set; }


        private SupplierModel supplierModel = new();
        private SupplierEntity supplierEntity = new();

        protected override async Task OnInitializedAsync()
        {
            if (SupplierId != null)
            {
                try
                {
                    supplierEntity = await SupplierRepository.GetById(Guid.Parse(SupplierId));
                    if (supplierEntity == null)
                        navManager.NavigateTo("/suppliers");

                    supplierModel = Mapper.Map<SupplierModel>(supplierEntity);

                }
                catch (Exception ex)
                {
                    Logger.LogError("GetSupplier error: " + ex.Message);
                }
            }
            if (supplierModel.Mobiles.Count < 3)
            {
                var count = 3 - supplierModel.Mobiles.Count;
                for (int i = 0; i < count; i++)
                {
                    supplierModel.Mobiles.Add(new Mobile() { Phone = "" });
                }
            }
        }

        public async Task AddSupplier()
        {
            if (supplierModel != null)
            {
                try
                {
                    supplierEntity = Mapper.Map<SupplierEntity>(supplierModel);
                    var numbers = MobileService.GetMobiles(supplierEntity.Mobiles);
                    supplierEntity.Mobiles = new();

                    if (numbers != null)
                        supplierEntity.Mobiles.AddRange(numbers);

                    await SupplierRepository.Create(supplierEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Create supplier error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/suppliers");
        }

        public async Task UpdateSupplier()
        {
            if (supplierModel != null)
            {
                try
                {
                    supplierEntity.ContactPerson = supplierModel.ContactPerson;
                    supplierEntity.Name = supplierModel.Name;
                    supplierEntity.SupplierId = supplierModel.SupplierId;
                    supplierEntity.Address = supplierModel.Address;
                    supplierEntity.Area = supplierModel.Area;
                    supplierEntity.Remarks = supplierModel.Remarks;

                    var numbers = MobileService.GetMobiles(supplierModel.Mobiles);
                    supplierEntity.Mobiles = new();
                    supplierEntity.Mobiles.AddRange(numbers);

                    await SupplierRepository.Update(supplierEntity);
                    await MobileService.DeleteEmptyNumbers(context);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Update Supplier error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/suppliers");
        }

        public async void DeleteSupplier()
        {
            if (supplierEntity != null)
            {
                try
                {
                    if (supplierEntity.Mobiles.Count > 0)
                        await MobileRepository.DeleteRange(supplierEntity.Mobiles);

                    await SupplierRepository.Delete(supplierEntity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Delete Supplier error: " + ex.Message);
                }
            }
            navManager.NavigateTo("/suppliers");
        }
    }
}
