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

> 練習 2（修 3 個 bug）的心得記在 `documents/PROCESS.md`（練習心得模板）。
