using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private const int LowStockLookbackDays = 30;

    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold)
    {
        var soldSince = DateTime.UtcNow.AddDays(-LowStockLookbackDays);
        return _productRepository.GetLowStockAsync(threshold, soldSince);
    }
}
