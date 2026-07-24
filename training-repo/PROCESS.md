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
