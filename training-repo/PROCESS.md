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
