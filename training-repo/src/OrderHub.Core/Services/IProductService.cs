using OrderHub.Core.Common;
using OrderHub.Core.Domain;

namespace OrderHub.Core.Services;

public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<IReadOnlyList<Product>> GetActiveAsync();

    /// <summary>
    /// 取得庫存低於 threshold 的販售中商品（依庫存升冪），含近 30 天售出數量。
    /// </summary>
    Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold);
}
