using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly OrderHubDbContext _db;

    public ProductRepository(OrderHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.OrderBy(p => p.Sku).ToListAsync();

    public async Task<IReadOnlyList<Product>> GetActiveAsync() =>
        await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Sku).ToListAsync();

    public Task<Product?> GetByIdAsync(int id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold, DateTime soldSince)
    {
        // 單一查詢：低庫存商品 + 相關子查詢統計近 30 天售出量（排除 Cancelled），避免 N+1。
        var query =
            from p in _db.Products
            where p.IsActive && p.StockQuantity < threshold
            orderby p.StockQuantity
            select new LowStockItem(
                p.Sku,
                p.Name,
                p.StockQuantity,
                (from oi in _db.OrderItems
                 join o in _db.Orders on oi.OrderId equals o.Id
                 where oi.ProductId == p.Id
                       && o.Status != OrderStatus.Cancelled
                       && o.CreatedAt >= soldSince
                 select (int?)oi.Quantity).Sum() ?? 0);

        return await query.ToListAsync();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
