# 貢獻指南

感謝你對 mini螢幕筆記（MiniScreenNotes）有興趣。本專案採用 MIT License，歡迎提出問題、修正錯誤或新增功能。

## 開發環境

- Windows 10 1809 以上
- .NET 10 SDK
- Git
- 建議使用 Visual Studio 2022 或更新版本，或 Visual Studio Code

## 建置與測試

```powershell
dotnet restore MiniScreenNotes.sln
dotnet build MiniScreenNotes.sln
dotnet test MiniScreenNotes.sln
```

格式化檢查：

```powershell
dotnet format MiniScreenNotes.sln --verify-no-changes
```

- 開發與測試一律使用 `Debug`。
- 專案啟用 `Nullable` 與 `TreatWarningsAsErrors`，請勿關閉警告。
- 套件版本集中於 `Directory.Packages.props`，不要在 `.csproj` 寫死版本。

## 程式風格

- C# 使用 file-scoped namespace、nullable reference types。
- 4 空格縮排。
- 類別／方法使用 PascalCase；區域變數／參數使用 camelCase；私有欄位使用 `_camelCase`。
- 非同步方法以 `Async` 結尾；函式庫層使用 `ConfigureAwait(false)`。
- 不在 UI 執行緒進行 I/O。
- 使用者可見文字放 `Resources/Strings`，預設繁體中文。
- 不要留下 `TODO`、測試假資料或除錯輸出。
- 不要在程式碼或版本控制中加入 API Key、密碼或憑證。

## 提交內容

1. 先開 Issue 描述問題或需求（小型修正可直接開 PR）。
2. 從 `main` 建立分支。
3. 完成變更並更新相關文件：
   - `CHANGELOG.md`
   - `RELEASE_NOTES.md`（若為使用者可見變更）
   - `docs/DEV_LOG.md`
4. 確認建置與測試通過。
5. 開 Pull Request，說明變更內容、測試方式與影響範圍。

### Commit 訊息

請使用簡短、描述性的訊息，例如：

```text
修正面板吸附後未啟動自動隱藏
新增海洋青主題的深色版本
更新使用指南與建置文件
```

## Pull Request 檢查清單

- [ ] 可在本機以 `dotnet build` 建置
- [ ] `dotnet test` 全數通過
- [ ] 已更新文件與開發日誌
- [ ] 未提交 `bin/`、`obj/`、`.vs/`、發行輸出或憑證
- [ ] 未變更無關檔案
- [ ] 使用者可見文字為繁體中文

## 授權

提交貢獻即表示同意以本專案的 MIT License 釋出。
