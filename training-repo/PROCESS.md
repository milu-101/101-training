# PROCESS — OrderHub AI Agent 練習紀錄

## 練習 1 — 讓 agent 讀懂專案、agent 初始設置

**日期**：2026-07-24

### 建立的設定檔

依 `agent-configuration.md`（Claude Code 版）完成五個區塊：

| 檔案 | 用途 | 進 git |
| --- | --- | --- |
| `CLAUDE.md` | 專案記憶：分層慣例、技術棧、指令、危險檔案、Don'ts | ✅ |
| `.claude/settings.json` | 權限規則（deny/ask/allow）+ hooks（PreToolUse/PostToolUse） | ✅ |
| `.claude/hooks/block-destructive-sql.ps1` | 攔截含 `DROP TABLE` / `TRUNCATE` 的 Bash 指令 | ✅ |
| `.claude/hooks/log-edits.ps1` | 每次 Edit/Write 記錄到 `edit-log.txt` | ✅ |
| `.claude/agents/code-reviewer.md` | 唯讀 reviewer 子代理 | ✅ |
| `.claude/agents/test-runner.md` | 隔離測試輸出的子代理 | ✅ |
| `.claude/skills/fix-bug/SKILL.md` | `/fix-bug` 斜線指令：標準修 bug 流程 | ✅ |

> `.claude/settings.local.json`（個人設定）與 `edit-log.txt` 不進 git；
> repo 根目錄的 `.gitignore` 已有 `*.local.json` 規則涵蓋 local 設定。

### 事實核對（不照抄範本，先驗證再寫進 CLAUDE.md）

- TargetFramework：`net8.0` ✅（三個 .csproj 一致）
- 測試框架：xUnit 2.5.3 ✅
- 網站埠：`http://localhost:5150` ✅（launchSettings.json）
- 參考檔：`ProductsController.cs`、`ProductService.cs` 皆存在 ✅

### 驗證方式（開新 session 後執行）

CLAUDE.md：

- [ ] 問「這個專案的分層慣例是什麼？」——不用讀檔就答得出三層架構、Controller 薄、只有 repository 碰 DbContext
- [ ] 問「金額用什麼型別？折扣在哪算？」——答 `decimal`、`OrderService.CalculateTotal`
- [ ] 請它「裝一個新 NuGet 套件」——應先問而不是逕自安裝

permissions：

- [ ] `git push --force` → 直接被 deny（不跳詢問）
- [ ] `dotnet test` → 直接放行（allow）
- [ ] `dotnet ef database drop` → 先跳確認（ask）

hooks：

- [ ] 請它「用 sqlcmd TRUNCATE OrderItems」→ 被 PreToolUse hook 擋下並回報原因
- [ ] 請它建立 `sample.txt` → `.claude/hooks/edit-log.txt` 出現一行紀錄

subagents / skills：

- [ ] 修完 bug 說「用 code-reviewer 審查變更」→ 委派唯讀 reviewer
- [ ] 用 test-runner 跑測試 → 只回摘要
- [ ] `/fix-bug <症狀>` → 啟動標準流程

---

## 練習 2 — 排查並修復 3 個 bug

**日期**：2026-07-24

### 三個 bug 與根因

| # | 症狀 | 根因 | 修法 | 分支 |
| --- | --- | --- | --- | --- |
| 1 | 新訂單在列表第一頁找不到、最後一頁空白 | `OrderRepository.GetPagedAsync` 用 `Skip(page * pageSize)`，但 page 是 1-based，第一頁就多跳一整頁（off-by-one） | 改成 `Skip((page - 1) * pageSize)` | `fix/order-list-pagination` |
| 2 | Gold 會員應付總額比手算少一截，Silver 正常 | 建單時只有 Gold 把折扣先打進 `UnitPriceSnapshot`，`CalculateTotal` 又再打一次 → Gold 折兩次 (0.81) | 移除建單的預打折，快照一律存原價，折扣統一由 `CalculateTotal` 打一次 | `fix/gold-double-discount` |
| 3 | 取消訂單後庫存不回補、越退越少 | 先把 `order.Status` 設成 Cancelled，才用它判斷是否回補 → 條件恆為 false | 改狀態前先用 `wasActive` 記住原狀態，再據以回補 | `fix/cancel-restock` |

> 每個 bug 一個獨立分支、一個 commit（含回歸測試），commit message 一律「症狀 → 根因 → 修法」。

### 心得（第一次用 agent 修 bug 的觀察）

- **三個 bug 都是「差一點」的經典錯誤**：off-by-one、重複套用同一段邏輯、狀態判斷的先後順序。程式碼看起來都很順、也會編譯過，根因都只在一兩行——但頁面上的症狀差很多。這說明「會動」不等於「對」。
- **先重現、再定位、最後才動手**，比一看到就改快得多。三個根因其實都藏在既有測試「差一步」沒測到的地方：舊測試只驗了分頁的**總數**、取消後的**狀態**，卻沒驗「哪一筆在第一頁」「庫存有沒有加回來」。缺的斷言正好就是 bug 的藏身處。
- **TDD 的紅燈很有價值**：每個 bug 都先寫一個會失敗的測試，確認它失敗的原因真的對應到客訴症狀，修完再轉綠。這樣才確定測試是真的驗到行為，而不是恆真斷言。
- **既有測試是理解「正確設計」的線索**：bug 2 一開始會猶豫要改建單還是改 `CalculateTotal`，是既有的 `CalculateTotal_AppliesTierDiscountOnSubtotal`（Gold 1000→900）確立了「快照存原價、折扣只打一次」才是對的設計，才沒改錯方向。
- **一 bug 一 commit / 一分支** 讓每個修改都能單獨回溯、單獨 review，message 用「症狀→根因→修法」之後，未來的人不用讀 diff 也知道當初在解什麼。
- **agent 的產出一定要自己驗**：對照程式碼、跑 `dotnet test` 全綠（最終 30/29 綠）才算數，不能因為說明聽起來合理就相信。

### 目前狀態

- 三個修復分別在三個 `fix/*` 分支上、尚未合併回 `main`（保持隔離，待逐一 review / merge）。
- 每個分支上 `dotnet test` 全綠。

---

# 活動 2 — 自建 MCP Server

## 練習 0 — 先當使用者：接一個現成的 MCP（Playwright）

**日期**：2026-07-31

### 做了什麼

接上 Playwright MCP 之後，我請 agent 直接去 `http://localhost:5150` 建一筆新訂單、截圖結果頁。它自己把流程跑完了：

1. 開網站 → 導到訂單列表，點「建立訂單」進 `/Orders/Create`
2. 客戶選「陳志明（金卡會員）」，商品選 SKU-1002 極光 機械鍵盤，數量填 2
3. 送出 → 導到 `/Orders/Details/203`，畫面出現「訂單 #203 建立成功」
4. 截了結果頁：小計 NT$ 4,640、金卡 9 折折掉 NT$ 464、應付總額 NT$ 4,176

順帶一提，這筆的金額剛好驗證了活動 1 bug 2 修好之後的行為——金卡會員只折一次（0.9），沒有再出現折兩次的狀況。

### 和活動 1 練習 2 的對比（這題的重點）

活動 1 修 bug 時，「重現客訴」這一步是我自己在瀏覽器裡一格一格點的：手動開頁面、手動選客戶商品、手動送出，再肉眼比對頁面上的數字對不對。步驟都寫在腦子裡，換一個人重現就要重講一遍，也沒有留下可回放的紀錄。

接了 Playwright MCP 之後，同一件事變成 agent 自己做：它能讀頁面的 accessibility snapshot 知道有哪些欄位、自己決定先選客戶再選商品、送出後自己確認導頁成功，最後把結果截圖回傳。我從「操作的人」退到「看結果的人」。

差別具體在幾點：

- **重現變成可交付的動作**：以前的重現步驟是口述知識，現在是一串 agent 能重跑的操作。要再驗一次，只要再叫它做一遍就好。
- **多了一個「看得到」的證據**：截圖直接把畫面狀態留下來，不用再靠我描述「我看到金額是多少」。
- **邊界還是在人身上**：這題是唯讀性質的建單、風險低所以放手讓它跑；但它終究是照我給的意圖操作，選哪個客戶、填多少數量、結果對不對，還是我在把關——跟活動 1 那句「agent 的產出一定要自己驗」是同一件事。

換句話說，工具讓 agent 能替我做「重現與操作」這種以前只能人工的體力活，但「這樣對不對」的判斷沒有外包出去。

### 目前狀態

- Playwright MCP 已接上，建單 → 截圖整條流程 agent 能獨立完成。
- 結果截圖存為 `order-203-result.png`。

---

## 練習 1 — 自建 OrderHub MCP Server（stdio）

**日期**：2026-07-31
**分支**：`feat/orderhub-mcp`

### 做了什麼

建了一個 C# console 專案 `src/OrderHub.Mcp`，透過 stdio 對外開三個唯讀工具：

| 工具（agent 看到的名字） | 做什麼 | 底層接誰 |
| --- | --- | --- |
| `get_order` | 依訂單編號查明細（客戶、品項、單價快照、折扣、應付總額） | `IOrderService.GetOrderAsync` + 金額計算 |
| `low_stock` | 列出庫存低於門檻、仍在販售的商品，庫存升冪 | `IProductRepository.GetActiveAsync` |
| `customer_orders` | 查某客戶的全部訂單摘要 | `IOrderService.GetCustomerOrdersAsync` |

方法名 `GetOrder` / `LowStock` / `CustomerOrders` 由 SDK 自動轉成 snake_case，煙霧測試時確認過就是這三個名字。

### 動手前先對過的事（延續練習 2 那句「產出要自己驗」）

範本引用了一票 `IOrderService` 方法，我沒照抄就寫，先開介面確認簽章對得上：

- `IOrderService` 確實有 `GetOrderAsync` / `GetCustomerOrdersAsync` / `CalculateSubtotal` / `CalculateTotal` / `GetDiscountRate` ✅
- `IProductRepository.GetActiveAsync()` 回 `Product` 清單，欄位有 `Sku` / `Name` / `StockQuantity` ✅
- `OrderService` 建構子吃三個 repository（Order / Product / Customer），所以 DI 這三個都要註冊，少一個執行期才會炸 ✅
- 連線字串 key 是 `Default`，跟 Web 的 appsettings 一致；MCP 專案沒有 appsettings，靠範本裡的 fallback 撐 ✅

### 踩到 / 注意到的點

- **`dotnet new console` 預設給 net10.0**（機器裝了 SDK 10），手動改回 `net8.0` 跟其他專案一致。
- **少 `using Microsoft.Extensions.Configuration;` 會編不過**：`GetConnectionString` 是那個命名空間的擴充方法，第一次 build 就吃了 CS1501，補上就好。
- **分層照舊**：工具建構子注入的是 service / repository，沒有直接摸 `DbContext`；金額一律叫 `OrderService` 算，工具裡不重寫折扣規則（跟 CLAUDE.md 和練習 2 bug 2 同一條原則——規則只留一份真相）。
- **stdout 是協定通道**：log 全部導到 stderr（`LogToStandardErrorThreshold`），投影成匿名物件避免 Order↔Customer 循環參照在執行期爆掉。

### 驗證

- [x] `dotnet build src/OrderHub.Mcp` 成功，0 warning / 0 error
- [x] stdio 煙霧測試：送 `initialize` + `tools/list`，stdout 乾淨地回三個工具（名稱、description、參數 schema 都對），log 走 stderr，stdin 保持開著避免 server 提早關機
- [x] 一個獨立 commit（訊息列出新增的三個工具）

> MCP Inspector 的手動測試留到練習 2。

---

## 練習 2 — 用 MCP Inspector 除錯

**日期**：2026-07-31
**分支**：`feat/orderhub-mcp`

### 怎麼跑的

Inspector 有兩種用法，我兩種都用了：

- **CLI 模式**（`npx @modelcontextprotocol/inspector --cli dotnet <dll> --method ...`）：非互動、輸出可直接抓回來核對，拿來做三項驗證最乾脆。
- **GUI 模式**（`npx @modelcontextprotocol/inspector dotnet <dll>`）：起在 `http://localhost:6274`，我用 Playwright 開它、連上 server、切到 Tools 分頁截圖，兌現「瀏覽器會開啟 Inspector 介面」那段體驗。截圖存為 `inspector-tools-list.png`。

### 三項驗證的結果

1. **工具列得出來、說明如我所寫**：`tools/list` 回三個工具，snake_case 名稱（`get_order` / `low_stock` / `customer_orders`）、description、每個參數的說明和 `low_stock` 的 `default: 10` 都在。中文沒有亂碼。
2. **`low_stock`（threshold=10）和 `/Products/LowStock` 頁面一致**：工具回 5 筆——SKU-1048(2)、SKU-1005(3)、SKU-1023(3)、SKU-1014(4)、SKU-1032(4)；頁面（門檻同樣填 10）顯示的就是這 5 個 SKU、同樣的庫存量。集合完全對得上。
   - 小觀察：庫存同為 4 的兩筆（SKU-1014 / SKU-1032），工具和頁面的先後不同。因為 `OrderBy(StockQuantity)` 是穩定排序但同鍵沒有第二排序鍵，次序由查詢來源決定——集合一致就算過，但如果哪天要「可重現的排序」，得再加一個 tie-breaker（例如 SKU）。
3. **不存在的 Id 回乾淨訊息**：`get_order(id=999999)` 回「找不到訂單 999999」，不是 exception dump——因為工具在 `GetOrderAsync` 回 null 時就先攔下來了。順手用 `get_order(id=203)` 對練習 0 那筆做健全性檢查，Total 4176.00（Gold 9 折）跟當時截圖一致，等於把整條分層端到端也驗過。

### 心得

- **CLI 模式是這種「要留證據」的驗證最好的朋友**：GUI 適合探索、看得到參數表單，但要把「我驗過、結果是這樣」寫進紀錄，CLI 的純文字輸出直接複製就好，不用截圖再讀一次。
- **先用 Inspector 測、不要急著接 agent**：這一步把「工具能不能跑、回傳長怎樣、錯誤訊息乾不乾淨」全部確認完，之後接進 agent 出問題時，就能排除「是工具本身壞了」這條線。

### 驗證

- [x] 三個工具都列得出來，description、參數說明如我所寫（CLI + GUI 截圖各一份佐證）
- [x] `low_stock(threshold=10)` 回傳商品與 `/Products/LowStock` 頁面一致
- [x] `get_order` 用不存在的 Id 得到清楚錯誤訊息，非 exception dump

---

## 練習 3 — 註冊給 agent，做 before/after 對照

**日期**：2026-07-31
**分支**：`feat/orderhub-mcp`

### 註冊

在 repo 根目錄建了 `.mcp.json`（進 git，全隊共用）：

```json
{
  "mcpServers": {
    "orderhub": {
      "command": "dotnet",
      "args": ["run", "--project", "src/OrderHub.Mcp"]
    }
  }
}
```

> `dotnet run` 首次要編譯、較慢，agent 連線可能逾時。真的遇到就先 `dotnet build` 一次，或把 `args` 改指向發佈後的執行檔。

### 對照實驗：「哪些商品庫存低於 5？」

這次是在**非互動 session** 裡做的，有個誠實的前提要先寫下來：**orderhub 的工具沒有載入這個 session**（server 是這次才建的），我也沒辦法在對話中途重啟自己的 MCP 連線去切 on/off。所以下面「沒工具 / 有工具」兩邊，是我分別走了兩條真實路徑去比，而不是同一個 agent 在同一輪切換。真正的 `/mcp` on/off 對照留給互動式終端（見下方驗證）。

**沒工具那邊（我實際繞的路）**：手上沒有 `low_stock`，要回答只能自己去撈 DB——

1. 先找連線字串（翻 `appsettings.json`：`Server=localhost;Database=OrderHubTraining;Trusted_Connection`）
2. 再搞懂 schema：資料表 `Products`、欄位 `StockQuantity` / `IsActive` / `Sku` / `Name`（得去讀 Domain / DbContext 才知道）
3. 自己補上「仍在販售」的條件（`IsActive = 1`），不然會混進停售品
4. 用 `sqlcmd` 跑 `SELECT ... WHERE StockQuantity < 5 AND IsActive = 1 ORDER BY StockQuantity`
5. 結果還被 sqlcmd 的 codepage 弄成亂碼，SKU 和數字看得懂、中文名整排變豆腐

**有工具那邊**：`low_stock(threshold=5)`，一次呼叫。乾淨 JSON、中文完好、`IsActive` 過濾和排序都已經包在工具裡。

**兩邊結果一致**（都這 5 筆）：

| SKU | 名稱 | 庫存 |
| --- | --- | --- |
| SKU-1048 | 晨光 行動電源 | 2 |
| SKU-1005 | 極光 筆電支架 | 3 |
| SKU-1023 | 雲峰 27吋螢幕 | 3 |
| SKU-1014 | 星河 USB-C 集線器 | 4 |
| SKU-1032 | 曜石 機械鍵盤 | 4 |

### 差異記下來

- **繞的路差很多**：沒工具要「連線字串 → schema 考古 → 自己補業務條件 → 寫 SQL → 還要處理編碼」，每一步都是可能出錯或問錯的地方（例如忘了 `IsActive`，就會把停售品也報成低庫存）。有工具就是一句話、一次呼叫。
- **業務規則的歸屬**：`IsActive` 這種「什麼叫『仍在販售』」的判斷，沒工具時落在**呼叫者**身上（每個要查的人都得自己記得），有工具時它被收進 server 一次、大家共用同一份定義。這跟前面「金額別自己算」是同一堂課。
- **輸出形狀**：DB 給的是要再解析的表格（還可能亂碼），工具給的是 agent 能直接往下用的結構化 JSON。省掉的不只是打字，是「把原始資料整理成能用的形狀」這段。
- **一致性讓人安心**：兩條路殊途同歸回同 5 筆，反過來驗證了工具沒有偷改語意——它只是把我手動繞的那條路，收成一次可靠的呼叫。

### 驗證

- [x] `.mcp.json` 建好、進 git（獨立 commit）
- [x] before/after 對照完成並記錄（同一問題、兩條路、結果一致）
- [ ] **待互動式終端**：重啟 Claude Code 讓它讀到 `.mcp.json` 後，`/mcp` 應看得到 orderhub server 與三個工具；再對同一問題問一次，觀察是否一次工具呼叫就答完（這個 session 無法自我重連，故留給互動環境確認）
