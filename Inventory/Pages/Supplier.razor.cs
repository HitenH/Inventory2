using AutoMapper;
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
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private IMapper Mapper { get; set; }
        [Inject] private INumberService NumberService { get; set; }


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
            if (supplierModel.Numbers.Count < 3)
            {
                var count = 3 - supplierModel.Numbers.Count;
                for (int i = 0; i < count; i++)
                {
                    supplierModel.Numbers.Add(new Number() { Phone = "" });
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
                    var numbers = NumberService.GetNumbers(supplierEntity.Numbers);
                    supplierEntity.Numbers = new();

                    if (numbers != null)
                        supplierEntity.Numbers.AddRange(numbers);

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
                    supplierEntity.Address = supplierModel.Address;
                    supplierEntity.Area = supplierModel.Area;
                    supplierEntity.Numbers = supplierModel.Numbers;
                    supplierEntity.Remarks = supplierModel.Remarks;

                    var numbers = NumberService.GetNumbers(supplierEntity.Numbers);
                    supplierEntity.Numbers = new();

                    if (numbers != null)
                        supplierEntity.Numbers.AddRange(numbers);

                    await SupplierRepository.Update(supplierEntity);
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
