using OrderHub.Core.Common;
using OrderHub.Core.Domain;

namespace OrderHub.Core.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<IReadOnlyList<Product>> GetActiveAsync();
    Task<Product?> GetByIdAsync(int id);

    /// <summary>
    /// 取得 IsActive 且 StockQuantity 低於 threshold 的商品，依庫存升冪排序，
    /// 並附上自 soldSince 起、排除 Cancelled 訂單的售出數量。
    /// </summary>
    Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold, DateTime soldSince);

    Task SaveChangesAsync();
}
