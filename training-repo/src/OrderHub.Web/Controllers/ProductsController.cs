using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> LowStock(int? threshold)
    {
        // 未帶參數 → 預設門檻 10
        var effectiveThreshold = threshold ?? 10;

        var vm = new LowStockViewModel { Threshold = effectiveThreshold };

        // 門檻 <= 0：以 ModelState 顯示表單錯誤，不查詢、不要變成 500
        if (effectiveThreshold <= 0)
        {
            ModelState.AddModelError(nameof(LowStockViewModel.Threshold), "庫存門檻必須大於 0");
            return View(vm);
        }

        var items = await _productService.GetLowStockAsync(effectiveThreshold);
        vm.Items = items.Select(i => new LowStockRowViewModel
        {
            Sku = i.Sku,
            Name = i.Name,
            StockQuantity = i.StockQuantity,
            SoldLast30Days = i.SoldLast30Days
        }).ToList();

        return View(vm);
    }
}

