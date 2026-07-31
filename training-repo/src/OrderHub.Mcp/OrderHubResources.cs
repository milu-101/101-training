using ModelContextProtocol.Server;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using System.ComponentModel;

// Resource 是「背景知識」而不是「查詢動作」：agent 判讀金額問題時該先知道的規則。
// 內容刻意由 OrderService.GetDiscountRate 動態組出，不寫死字串——
// 否則折扣改版時 resource 和程式碼會變成兩份真相（同 CLAUDE.md「折扣集中在一處」）。
[McpServerResourceType]
public class OrderHubResources(IOrderService orderService)
{
    [McpServerResource(UriTemplate = "orderhub://discount-rules",
        Name = "會員折扣規則", MimeType = "text/markdown")]
    [Description("目前生效的會員折扣規則與計算方式")]
    public string DiscountRules()
    {
        var tiers = Enum.GetValues<CustomerTier>().Select(tier =>
        {
            var rate = orderService.GetDiscountRate(tier);
            return rate == 0m
                ? $"- {tier}：不打折"
                : $"- {tier}：折扣 {rate:P0}（即 {(1 - rate) * 10:0.##} 折）";
        });

        return $"""
            # OrderHub 會員折扣規則

            {string.Join("\n", tiers)}

            ## 計算方式

            - 小計 = Σ(UnitPriceSnapshot × Quantity)
            - 應付總額 = 小計 × (1 - 折扣率)，四捨五入到小數 2 位
            - 折扣在小計上**折抵一次**；單價快照（UnitPriceSnapshot）存的是下單
              當下的**原價**，不含折扣

            > 本內容由 `OrderService.GetDiscountRate` 動態產生，與程式碼同一份真相。
            """;
    }
}
