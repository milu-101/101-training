using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_ReturnsOnlyBelowThreshold_OrderedByStockAsc()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 8, sku: "SKU-S008");
        TestSetup.AddProduct(db, stock: 3, sku: "SKU-S003");
        TestSetup.AddProduct(db, stock: 10, sku: "SKU-S010"); // 剛好等於門檻 → 排除（嚴格 <）
        TestSetup.AddProduct(db, stock: 12, sku: "SKU-S012"); // 高於門檻 → 排除

        var result = await service.GetLowStockAsync(10);

        // 只回庫存 < 10 的兩筆，且依庫存升冪
        Assert.Equal(new[] { "SKU-S003", "SKU-S008" }, result.Select(r => r.Sku).ToArray());
        Assert.Equal(new[] { 3, 8 }, result.Select(r => r.StockQuantity).ToArray());
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 2, sku: "SKU-ACT", isActive: true);
        TestSetup.AddProduct(db, stock: 1, sku: "SKU-INA", isActive: false); // 停售即使庫存最低也不出現

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal("SKU-ACT", result[0].Sku);
    }

    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesCancelledAndOldOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3, sku: "SKU-SOLD");

        db.Orders.AddRange(
            // 近期、未取消 → 計入 5
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 5, UnitPriceSnapshot = 100m } }
            },
            // 近期、但已取消 → 排除 4
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Cancelled,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 4, UnitPriceSnapshot = 100m } }
            },
            // 30 天以前 → 排除 7
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-40),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 7, UnitPriceSnapshot = 100m } }
            });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(5, result[0].SoldLast30Days);
    }
}
