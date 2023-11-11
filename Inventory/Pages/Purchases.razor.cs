using AutoMapper;
using Inventory.Domain.Entities;
using Inventory.Domain.Repository;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;

namespace Inventory.Pages
{
    public partial class Purchases
    {
        [Inject] private IPurchaseRepository PurchaseRepository { get; set; }
        [Inject] private ILogger<Purchases> Logger { get; set; }

        private List<PurchaseModel> purchases = new();
        private List<PurchaseModel> purchasesAfterSearch = new();
        private bool isSortAscending = false;

        protected async override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    var list = await PurchaseRepository.GetAll();
                    if (list.Count != 0)
                    {
                        purchases = list.Select(c =>
                         new PurchaseModel()
                         {
                             Date = c.Date,
                             Id = c.Id,
                             Remarks = c.Remarks,
                             SupplierId = c.Supplier.SupplierId,
                             SupplierName = c.Supplier.Name,
                             VoucherId = c.VoucherId,
                             TotalAmountProduct = c.TotalAmountProduct
                         }
                        ).ToList();
                        purchasesAfterSearch = purchases;
                    }
                    StateHasChanged();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Purchases error" + ex.Message);
                }
            }
        }
        //protected async override Task OnInitializedAsync()
        //{
        //    try
        //    {
        //        var list = await PurchaseRepository.GetAll();
        //        if (list.Count != 0)
        //        {
        //            purchases = list.Select(c => 
        //             new PurchaseModel()
        //             {
        //                 Date = c.Date,
        //                 Id= c.Id,
        //                 Remarks = c.Remarks,
        //                 SupplierId = c.Supplier.SupplierId,
        //                 SupplierName = c.Supplier.Name,
        //                 VoucherId = c.VoucherId,
        //                 TotalAmountProduct = c.TotalAmountProduct
        //             }
        //            ).ToList();
        //            purchasesAfterSearch = purchases;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogError("Purchases error" + ex.Message);
        //    }
        //}

        public void SearchItem(ChangeEventArgs e)
        {
            var search = e.Value.ToString().ToLower();
            purchasesAfterSearch = purchases.Where(n => n.VoucherId.ToString().Contains(search) || n.SupplierName.ToLower().Contains(search)
                                                        || n.Date.ToString().Contains(search)).ToList();
        }


        public void SortItem(string column)
        {
            if (purchasesAfterSearch.Count != 0)
            {
                if (column == "VoucherId")
                {
                    if (isSortAscending)
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderBy(c => c.VoucherId).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderByDescending(c => c.VoucherId).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "SupplierName")
                {
                    if (isSortAscending)
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderBy(c => c.SupplierName).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderByDescending(c => c.SupplierName).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Date")
                {
                    if (isSortAscending)
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderBy(c => c.Date).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderByDescending(c => c.Date).ToList();
                        isSortAscending = true;
                    }
                }
                else if (column == "Remarks")
                {
                    if (isSortAscending)
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderBy(c => c.Remarks).ToList();
                        isSortAscending = false;
                    }
                    else
                    {
                        purchasesAfterSearch = purchasesAfterSearch.OrderByDescending(c => c.Remarks).ToList();
                        isSortAscending = true;
                    }
                }
            }
        }
    }
}
