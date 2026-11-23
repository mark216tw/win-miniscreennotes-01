# mini螢幕筆記 (MiniScreenNotes)

輕量、低延遲的 Windows 螢幕標註與簡報工具。使用 .NET 10 與 WPF 開發，常駐系統匣，按下全域快捷鍵即可在任何畫面上畫重點、畫箭頭、逐步編號、即時放大，適合教學、簡報、遠端會議與日常溝通。

- 多螢幕 Per-Monitor DPI v2 透明覆蓋標註
- 16 種標註與簡報工具，另附獨立游標模式
- 1x～10x 無遞迴即時放大鏡、聚光燈與尾巴雷射筆
- 可吸附螢幕邊緣、自動隱藏為小點的浮動工具面板
- 多面板組態、10 種色彩主題，支援淺色與深色模式
- 一鍵快速儲存、複製到剪貼簿、開機自動啟動
- 完全離線，不需網路，也不上傳任何畫面或使用資料

## 下載

請至 [GitHub Releases](https://github.com/mark216tw/win-miniscreennotes-01/releases/latest) 下載最新版本。

| 平台 | 檔案 | 說明 |
| --- | --- | --- |
| x64 | `MiniScreenNotes-v1.0.20-win-x64.zip` | 一般 Intel／AMD Windows 電腦 |
| ARM64 | `MiniScreenNotes-v1.0.20-win-arm64.zip` | Windows on ARM 裝置（如 Snapdragon X 系列） |

本專案提供 **非 Self-Contained** 壓縮檔，不提供安裝程式。解壓縮後直接執行 `MiniScreenNotes.exe` 即可；執行前請先安裝 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)（x64 或 ARM64 對應版本）。

> 目前尚未進行程式碼簽章，Windows SmartScreen 可能會顯示「未識別的應用程式」提示。若確認檔案來自本專案 Releases，可選擇「仍要執行」。

## 功能特色

### 標註與簡報工具

| 工具 | 操作方式 | 備註 |
| --- | --- | --- |
| 游標模式 | 點選工具列或按滑鼠中鍵 | 滑鼠事件穿透至底層視窗；切回游標模式即可恢復標註工具 |
| 畫筆 | 按住左鍵拖曳 | 以二次貝茲曲線平滑筆跡，適合手寫與圈選 |
| 螢光筆 | 按住左鍵拖曳 | 半透明高亮筆觸，寬度為一般線寬的 3.5 倍 |
| 直線／箭頭 | 拖曳繪製 | 按住 `Shift` 可鎖定 0°、45°、90° 等角度 |
| 空心／實心矩形 | 拖曳繪製 | 按住 `Shift` 可繪製正方形 |
| 空心／實心橢圓 | 拖曳繪製 | 按住 `Shift` 可繪製正圓 |
| 綠色打勾／紅色叉叉 | 點擊放置；滾輪可調整大小 | 以綠色／紅色強調結果 |
| 文字標註 | 點擊輸入文字 | `Ctrl+Enter` 確認、`Esc` 取消 |
| 步驟編號 | 點擊放置 | 依序遞增；可於畫布按右鍵重設為 1 |
| 橡皮擦 | 拖曳 | 批次移除碰觸到的標註，可復原 |
| 聚光燈 | 移動滑鼠 | 四周變暗、中央保持清晰，滾輪調整直徑 |
| 放大鏡 | 移動滑鼠 | 滾輪調整 1x～10x；`Ctrl+滾輪` 調整鏡頭大小 |
| 雷射筆 | 移動滑鼠 | 顯示約 1.2 秒的彗星尾巴，不留下永久標註 |

### 浮動面板與個人化

- 面板尺寸：特小 24px、小 30px、中 38px、大 46px，並跟隨顯示器 DPI 縮放
- 橫向面板吸附螢幕上／下緣；直向面板吸附左／右緣
- 閒置後可設定 0.5～10 秒自動隱藏為小點，滑鼠移入立即展開
- 可勾選顯示工具、調整順序，超出時自動換行或分頁
- 多面板組態，各自保存位置、吸附邊緣、尺寸、方向與主題
- 顯示模式：跟隨系統、淺色、深色
- 10 種主題：黃色護眼、冰河藍、森林綠、靛紫、玫瑰紅、暮橙、海洋青、櫻花粉、摩卡棕、石墨灰

### 輸出與系統整合

- `Ctrl+Z` 復原、`Ctrl+Y` 重做；面板聚焦時 `Ctrl+C`／`Ctrl+S` 可直接複製與儲存
- 面板聚焦時按 `Esc` 回到游標模式（會先關閉顏色／線寬選單），面板維持顯示
- 複製標註結果到剪貼簿
- 儲存 PNG／JPEG，支援一鍵快速儲存與自訂資料夾
- 全域快捷鍵可自訂（預設 `Ctrl+Alt+A`）
- 支援多螢幕、混合 DPI、顯示器熱插拔與工作列位置變更
- 可設定開機自動啟動，並以 `--minimized` 常駐系統匣
- 系統匣圖示提供顯示面板、面板組態、設定與結束
- 浮動面板可被截圖與放大鏡排除（Windows 10 2004 以上支援 `WDA_EXCLUDEFROMCAPTURE`）

## 快速開始

1. 下載並解壓縮對應架構的 zip 檔。
2. 執行 `MiniScreenNotes.exe`。
3. 浮動工具面板會出現在螢幕上，可拖曳並吸附螢幕邊緣。
4. 按 `Ctrl+Alt+A` 開始標註目前畫面；選擇工具後直接在畫面上繪製。
5. 從系統匣圖示可開啟設定、切換面板組態或結束程式；設定會自動保存並於下次啟動還原。

## 系統需求

- Windows 10 1809（Build 17763）以上
- .NET 10 Desktop Runtime（非 Self-Contained 版本必須）
- x64 或 ARM64 架構
- 建議 Windows 10 2004（Build 19041）以上，以獲得 `WDA_EXCLUDEFROMCAPTURE` 完整支援

## 從原始碼建置

需要 .NET 10 SDK。

```powershell
git clone https://github.com/mark216tw/win-miniscreennotes-01.git
cd win-miniscreennotes-01
dotnet restore MiniScreenNotes.sln
dotnet build MiniScreenNotes.sln
dotnet test MiniScreenNotes.sln
```

建置輸出位於 `src/MiniScreenNotes/bin/Debug/net10.0-windows/`。

發佈非 Self-Contained 版本：

```powershell
dotnet publish src/MiniScreenNotes/MiniScreenNotes.csproj -c Release -r win-x64 --self-contained false
dotnet publish src/MiniScreenNotes/MiniScreenNotes.csproj -c Release -r win-arm64 --self-contained false
```

詳細建置與發佈流程請參閱 [建置與發佈](docs/BUILD_AND_RELEASE.md)。

## 專案結構

```text
src/MiniScreenNotes/          應用程式（WPF / MVVM）
  Models/                     資料模型與工具設定項目
  Models/Annotations/         標註元素與 Undo／Redo 動作
  Services/                   擷取、熱鍵、滑鼠鉤子、主題、系統匣等服務
  ViewModels/                 ViewModel
  Views/                      視窗、覆蓋層與放大鏡
  Helpers/                    Win32 P/Invoke、DPI、顯示器與共用工具
  Resources/                  主題、樣式、圖示與字串資源
tests/MiniScreenNotes.Tests/  xUnit 單元測試
docs/                         使用指南、架構、設計與開發紀錄
```

## 文件

- [使用指南](docs/USER_GUIDE.md)
- [系統架構與技術說明](docs/ARCHITECTURE.md)
- [系統設計文件](docs/SYSTEM_DESIGN.md)
- [建置與發佈](docs/BUILD_AND_RELEASE.md)
- [常見問題](docs/FAQ.md)
- [開發紀錄](docs/DEV_LOG.md)
- [版本紀錄](CHANGELOG.md)
- [貢獻指南](CONTRIBUTING.md)
- [安全性政策](SECURITY.md)

## 資料與隱私

所有設定與註記僅儲存在本機，不會上傳至任何伺服器。設定檔儲存於 `%AppData%\MiniScreenNotes\config.json`；錯誤記錄寫入 `%AppData%\MiniScreenNotes\error.log`。

## 授權

本專案採用 [MIT License](LICENSE)。Copyright (c) 2026 mark216tw。
