namespace OrderHub.Core.Common;

/// <summary>
/// 低庫存頁面的讀取模型：商品基本欄位 + 近 30 天售出數量（排除 Cancelled 訂單）。
/// </summary>
public record LowStockItem(string Sku, string Name, int StockQuantity, int SoldLast30Days);
