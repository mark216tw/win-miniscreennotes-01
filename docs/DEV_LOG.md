# 開發日誌 (DEV_LOG)

## 2026-11-23 - v1.0.24（修正放大鏡模式 Esc 無法回游標模式）

### 目標
修正放大鏡模式（及其他非游標工具）在未點擊覆蓋層、鍵盤焦點仍留在其他程式時，按 `Esc` 無法回到游標模式的間歇性問題。

### 變更
- `Helpers/Win32Interop.cs`：新增 `WH_KEYBOARD_LL`、鍵盤訊息與 `VK_ESCAPE` 常數、`KeyboardHookStruct`、`LowLevelKeyboardProc` 與鍵盤版 `SetWindowsHookEx` P/Invoke。
- `Services/GlobalKeyboardHookService.cs`（新增）：以全域低階鍵盤掛鉤攔截 `Esc`，`ShouldHandleEscape` 謂詞決定是否處理，處理時吞掉 keydown／keyup 並觸發 `EscapePressed`。
- `Views/OverlayWindow.xaml.cs`、`Services/OverlayManager.cs`、`Views/ToolbarWindow.xaml.cs`：新增 `IsEditingText`／`IsInlineTextEditing`／`IsAnyPopupOpen` 狀態查詢。
- `App.xaml.cs`：註冊並啟動 `GlobalKeyboardHookService`，`Esc` 於工具列顯示、非游標模式、無文字輸入／選單／對話框時回到游標模式；離開時釋放。
- `tests/MiniScreenNotes.Tests/GlobalKeyboardHookServiceTests.cs`（新增）：驗證 `ShouldHandleEscape` 各阻擋條件。
- 文件與版本：`README.md`、`docs/USER_GUIDE.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`Directory.Build.props`。

### 問題與解法
- `Esc` 原本僅由 `OverlayWindow`／`ToolbarWindow` 的 `PreviewKeyDown` 處理，需視窗為前景且有鍵盤焦點。以中鍵或全域快捷鍵由游標模式進入放大鏡時，覆蓋層以 `ShowActivated=false` 顯示、放大鏡為 `WS_EX_NOACTIVATE` 且點擊穿透，使用者只移動滑鼠未點擊時焦點仍留在其他程式，`Esc` 因此送不到本程式。
- 改以 `WH_KEYBOARD_LL` 全域掛鉤處理，不依賴焦點；文字輸入、顏色／線寬選單、設定／關於對話框開啟時仍交由原視窗處理，避免破壞 `Esc` 取消語意。

### 待辦
- 尚未於實體多螢幕／混合 DPI 環境實測全域掛鉤與輸入法／遠端桌面情境下的 `Esc` 行為。

---

## 2026-11-23 - v1.0.23（修正放大鏡進場陰影閃現）

### 目標
修正以滑鼠中鍵由游標模式切換進入放大鏡時，鏡頭陰影會先在預設位置閃現一幀後消失的問題。

### 變更
- `Views/MagnifierWindow.xaml.cs`：`Start()` 於視窗原本隱藏時先設 `Opacity = 0` 再 `Show()`，待 `UpdateLens()` 完成擷取與 `SetWindowPos` 定位後才透過 `RevealIfReady()` 恢復為不透明；`OnCompositionTargetRendering` 亦在定位成功後才揭露；`Stop()` 清除待揭露狀態。
- 版本與文件：`Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`。

### 問題與解法
- 放大鏡每次進入皆重建視窗，`Show()` 當下尚未定位，會先以 WPF／OS 預設位置顯示一幀，帶 `DropShadowEffect` 的鏡頭陰影因此閃現後才被 `SetWindowPos` 移走 → 沿用 `ToolbarWindow.ShowAndRestorePosition()` 的「先透明顯示、定位完成再顯示」模式，以 `_pendingReveal` 旗標在首次成功定位（`_lastPhysLeft` 已設定）後才恢復 `Opacity`。

### 待辦
- 尚未於實體多螢幕／混合 DPI 環境實測中鍵來回切換與放大鏡進場動畫。

---

## 2026-11-23 - v1.0.22（Esc 改為回到游標模式）

### 目標
修正點擊浮動面板工具後按 `Esc` 會收起面板的問題；改為回到游標模式，並取消 `Esc` 收起面板的行為。

### 變更
- `Views/ToolbarWindow.xaml.cs`：`ToolbarWindow_PreviewKeyDown` 的 `Esc` 分支改為設定 `_session.IsCursorMode = true`（透過 `StateChanged` 自動同步按鈕狀態），保留先關閉顏色／線寬選單。
- `Views/AboutWindow.xaml.cs`：移除 `Esc` 收起面板的快捷鍵說明列。
- `Resources/Strings/Strings.resx`、`Strings.Designer.cs`：移除不再使用的 `About_Shortcut_ClosePanel`。
- 文件：`README.md`、`docs/USER_GUIDE.md`、`CHANGELOG.md`、`RELEASE_NOTES.md` 同步新行為。
- 版本與文件：`Directory.Build.props`。

### 問題與解法
- 點擊面板工具後鍵盤焦點留在面板，`Esc` 由 `ToolbarWindow` 的 `PreviewKeyDown` 處理並呼叫 `HideAndRememberPosition()`；覆蓋層的 `Esc`（已是回到游標模式）收不到按鍵 → 面板端的 `Esc` 改為同樣切回游標模式。

### 待辦
- 尚未實機驗證多螢幕與混合 DPI 下的操作流程。

---

## 2026-11-20 - v1.0.21（啟動速度與常駐記憶體最佳化）

### 目標
提高應用程式冷／溫啟動速度，並大幅降低常駐系統匣及閒置時的電腦記憶體使用量。

### 變更
- `App.xaml.cs`：改用 `Host.CreateEmptyApplicationBuilder`（跳過設定檔監聽、環境變數掃描與預設日誌提供者）；啟動啟用 `ProfileOptimization`（Multicore JIT）；`--minimized` 模式延後建立 `ToolbarWindow`；一般啟動先顯示工具列再以 `DispatcherPriority.Loaded` 初始化系統匣、全域快捷鍵與滑鼠掛鉤；單一實例喚醒改用 `ThreadPool.RegisterWaitForSingleObject` 避免常駐占用執行緒；面板隱藏／背景常駐時排程執行工作集修剪。
- `Models/AppConfigJsonContext.cs`、`Services/ConfigService.cs`：改用 `System.Text.Json` 原始碼產生器序列化與反序列化 `AppConfig`，免除啟動期反射中繼資料建置。
- `Services/OverlayManager.cs`、`Views/OverlayWindow.xaml.cs`、`Views/MagnifierWindow.xaml.cs`、`Views/Controls/AnnotationCanvas.cs`：覆蓋層視窗改為按需建立（僅在面板顯示且使用標註／簡報工具、白黑板模式或存在可見標註時才建立），隱藏或閒置時關閉釋放全螢幕分層視窗並保留聚光燈半徑與放大鏡倍率／直徑；`AnnotationCanvas` 新增 `Detach()` 解除事件訂閱避免視窗外洩。
- `Services/GlobalMouseHookService.cs`：改用 `GetModuleHandle(null)` 取代 `Process.GetCurrentProcess().MainModule`；新增 `Stop()`／`SetEnabled()`，面板隱藏或功能關閉時立即卸載 `WH_MOUSE_LL`。
- `Services/ThemeService.cs`：記錄預設已載入的 `YellowEyeCare.Light`，避免啟動重複移除與重載同一主題字典。
- `Services/TrayService.cs`：系統匣選單改於 `Opening` 事件才更新組態清單與開機自啟狀態，並確實釋放 `Stream`、`Icon` 與 `ContextMenuStrip`。
- `Views/Controls/LaserOverlayLayer.cs`、`Views/Controls/SpotlightOverlayLayer.cs`：筆刷與畫筆改為靜態共用凍結物件，避免每螢幕重複建置與每幀配置。
- `Helpers/Win32Interop.cs`、`Helpers/MemoryTrimHelper.cs`：新增 `SetProcessWorkingSetSize` 與去抖動閒置記憶體壓縮／工作集修剪工具。
- `tests/MiniScreenNotes.Tests/OverlayManagerTests.cs`：新增覆蓋層按需建立／釋放條件單元測試。
- 版本與文件：`Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`。

### 問題與解法
- 啟動與常駐記憶體主要消耗在每個螢幕各建立一個全螢幕 `AllowsTransparency="True"` 分層視窗（即使處於游標模式、無任何標註或已隱藏至系統匣）→ 改為按需建立、隱藏／閒置即關閉釋放，並於 `AnnotationCanvas.Detach()` 解除 Singleton 事件訂閱防止記憶體外洩。
- 常駐系統匣時 `WH_MOUSE_LL` 仍持續攔截全系統滑鼠事件，使行程頁面無法維持冷狀態 → 改為僅於面板可見且啟用中鍵切換時掛載掛鉤。

### 待辦
- 尚未於實體多螢幕／混合 DPI 環境以工作管理員量測實機冷啟動耗時與常駐工作集數值。

---

## 2026-11-20 - v1.0.20（直向面板寬度與排列修正）

### 目標
修正直向面板首次啟動寬度過寬，以及工具超過每欄上限時平均分散到各欄的排列問題。

### 變更
- `Views/ToolbarWindow.xaml`：工具格線由 `UniformGrid` 改為 `Grid`；`ToolsGrid` 加上 `HorizontalAlignment="Center"`／`VerticalAlignment="Center"`，讓工具圖示區塊在可用區域內置中。
- `Views/ToolbarWindow.xaml.cs`：`RebuildToolButtons` 建立列／欄定義並以 `Grid.SetRow`／`Grid.SetColumn` 明確配置；`RestorePositionForProfile` 在 `UpdateLayout` 前加入 `InvalidateMeasure`，修正首次顯示被視窗最小寬度撐寬的問題。
- `Helpers/DockSnapHelper.cs`：新增 `GetGridCell`，依面板方向計算列優先／欄優先的位置。
- `tests/MiniScreenNotes.Tests/DockSnapHelperTests.cs`：新增直向欄優先與橫向列優先的格線位置測試。
- 版本與文件：`Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`。

### 問題與解法
- 首次 `Show()` 時 WPF 會先套用 OS 最小追蹤寬度，直向窄面板被撐到約 136 DIP；切換方向觸發重新量測才縮回。加入 `InvalidateMeasure()` 後可於首次顯示即縮回正確寬度。此問題已用 WPF 量測實驗重現並驗證修正。
- `UniformGrid` 固定以列優先填滿，直向 12 項、上限 9 時會排成每列 2 項的 6/6，而非第一欄 9 項、第二欄 3 項。改用 `Grid` 明確指定欄優先位置。
- 改用 `Grid` 後，`Auto` 列／欄只佔內容尺寸，被 `DockPanel` 拉伸時內容固定靠左上；當控制條在小尺寸面板比工具列更寬／更高、或視窗最小寬度造成多餘空間時，圖示就會偏移。已用 WPF 量測重現（slot 32×384 時按鈕落在 `(1,1)`），將 `ToolsGrid` 設為置中後回到正確位置。單一圖示本身在按鈕內原本即為置中，無須調整 `BuildIconContent`。

### 待辦
- 尚未於實體多螢幕／混合 DPI 環境手動確認直向面板啟動寬度。

---

## 2026-11-20 - v1.0.19（關於視窗資訊擴充）

### 目標
在設定視窗提供「關於」入口，並讓關於視窗除了版本與授權外，也能查看系統環境與完整快捷鍵說明。

### 變更
- `Views/SettingsWindow.xaml(.cs)`：底部新增「關於」按鈕與 `OpenAboutRequested` 事件。
- `App.xaml.cs`：設定視窗開啟關於視窗時沿用既有 `OpenAboutWindow()` 流程。
- `Views/AboutWindow.xaml(.cs)`：改為可調整大小與可捲動版面，新增系統資訊與快捷鍵清單；注入 `ConfigService` 取得目前全域快捷鍵。
- `Helpers/HotkeyDisplayHelper.cs`：集中處理修飾鍵與虛擬鍵名稱格式化。
- `ViewModels/SettingsViewModel.cs`：改用 `HotkeyDisplayHelper.GetKeyName`。
- `Models/ShortcutInfo.cs`：關於視窗快捷鍵清單資料項目。
- `Resources/Strings/Strings.resx`、`Strings.Designer.cs`：新增關於視窗與設定視窗使用字串。
- `tests/MiniScreenNotes.Tests/HotkeyDisplayHelperTests.cs`：新增快捷鍵格式化測試。
- 版本與文件：`Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`、`docs/USER_GUIDE.md`、`docs/BUILD_AND_RELEASE.md`。

### 問題與解法
- 原本關於視窗固定高度且不可捲動，內容增加後會超出畫面 → 改為可調整大小並以 `ScrollViewer` 呈現。
- 快捷鍵名稱格式在設定頁與關於視窗需要一致 → 抽出 `HotkeyDisplayHelper` 共用。

### 待辦
- 尚未於實體多螢幕／混合 DPI 環境手動確認關於視窗的顯示器資訊與版面。

---

## 2026-11-20 - v1.0.18（操作手感與回饋優化）

### 目標
以低風險的小改動改善日常操作：讓中鍵攔截可關閉、快捷鍵設定失敗不影響現有熱鍵、補齊面板鍵盤操作與狀態提示，並改善 Toast 與快速儲存的容錯。

### 變更
- `Models/AppConfig.cs`：新增 `MiddleClickToggleEnabled`（預設開啟）。
- `App.xaml.cs`：中鍵 hook 僅在設定開啟且面板可見時攔截。
- `ViewModels/SettingsViewModel.cs`、`Views/SettingsWindow.xaml`：新增中鍵切換開關；快捷鍵訊息改由字串資源提供。
- `Services/GlobalHotkeyService.cs`：註冊新快捷鍵失敗時自動回復原本已生效的組合，新增 `IsRegistered`。
- `Views/ToolbarWindow.xaml.cs`：新增 `Esc`／`Ctrl+Z`／`Ctrl+Y`／`Ctrl+C`／`Ctrl+S`；標註顯示狀態 ToolTip、步驟編號右鍵提示、色票勾選、色碼 Enter 套用與錯誤外框。
- `Views/ToolbarWindow.xaml`、`Resources/Styles.xaml`：色票按鈕加入 ContentPresenter 以顯示勾選圖示。
- `Views/ToastWindow.xaml(.cs)`：依游標所在螢幕定位、滑鼠移入暫停關閉、開啟資料夾提示獨立顯示。
- `Services/ScreenCaptureService.cs`：快速儲存失敗時改走另存新檔對話框。
- `Resources/Strings/Strings.resx`、`Strings.Designer.cs`：新增本次使用的字串。
- `tests/MiniScreenNotes.Tests/ConfigServiceTests.cs`：新增中鍵設定預設值與持久化測試。
- 版本與文件：`Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`。

### 問題與解法
- 全域快捷鍵舊版先 `Unregister` 再註冊，失敗時會失去目前熱鍵 → 先保留舊組合，失敗後自動重新註冊。
- 中鍵 hook 原本只要面板可見就攔截，面板收合為小手把時仍會影響所有程式 → 改為可設定關閉。
- 面板沒有鍵盤處理，點過工具按鈕後 `Ctrl+Z` 等快捷鍵無效 → 於面板加入 `PreviewKeyDown` 路由。
- 快速儲存資料夾失效時只顯示失敗 → 改為自動退回另存新檔。

### 待辦
- 依使用者決定，系統匣「清空所有標註」維持不跳二次確認。
- 尚未於實體多螢幕 / 混合 DPI 環境手動驗證 Toast 定位與面板快捷鍵焦點行為。

---

## 2026-11-20 - v1.0.17（線寬預設值調整）

### 目標
依需求調整線寬預設按鈕：「細」改為 4px、「中」改為 6px。

### 變更
- `Views/ToolbarWindow.xaml`：線寬預設按鈕 Tag 由 2／4 改為 4／6；「粗」維持 8px、「特粗」維持 14px。

### 問題與解法
- 無。

### 待辦
- 無。

---

## 2026-11-20 - v1.0.16（互動與輸出優化）

### 目標
降低橡皮擦與放大鏡的每幀成本，修正複製／儲存目標螢幕與失敗回饋，並改善組態讀取、主題跟隨與通知體驗。

### 變更
- `Views/Controls/AnnotationCanvas.cs`：橡皮擦拖曳期間只累積批次，於放開滑鼠時一次移除；undo 改為單一動作，拖曳過程不再反覆通知所有覆蓋層。
- `Views/MagnifierWindow.xaml.cs`：`CompositionTarget.Rendering` 限制約 60 FPS；游標與鏡頭設定未變時直接跳過擷取與定位。
- `Views/ToolbarWindow.xaml.cs`、`Helpers/DisplayMonitorHelper.cs`：複製／儲存改以游標所在實體螢幕為目標。
- `Services/ScreenCaptureService.cs`：擷取或儲存失敗時顯示提示；剪貼簿寫入重試 3 次；快速儲存檔名加入毫秒避免同秒覆蓋。
- `Services/ConfigService.cs`：快取作用中面板組態，避免每次讀取都重新驗證工具清單。
- `Views/OverlayWindow.xaml.cs`：快取視窗控制代碼與點擊穿透狀態，僅在實際變更時呼叫 Win32。
- `Services/ThemeService.cs`：跟隨系統模式監聽 `SystemEvents.UserPreferenceChanged`，即時切換淺／深色。
- `App.xaml.cs`、`Views/ToastWindow.xaml.cs`：啟動時快捷鍵註冊失敗會提示；Toast 改為單一視窗重用並延長顯示。
- `Resources/Strings/Strings.resx`：新增複製失敗、儲存失敗與快捷鍵失敗提示文字。

### 問題與解法
- 橡皮擦每個 `MouseMove` 都呼叫 `RemoveElements`，造成 undo 堆疊爆量、步驟編號重算與所有覆蓋層更新 → 改為拖曳批次、放開才提交。
- 放大鏡在高 DPI 下每幀擷取近 1000×1000 像素並寫入 `WriteableBitmap` → 限制更新頻率並在游標靜止時跳過。
- 複製／儲存原本以面板所在螢幕為準，與文件描述的游標所在螢幕不一致 → 改以 `GetCursorPos` 對應 `PhysicalBounds`。
- 剪貼簿可能被其他程式短暫占用 → 加入最多 3 次重試，失敗才顯示提示。

### 待辦
- 尚未於實體多螢幕混合 DPI 與高更新率螢幕實測放大鏡節流後的手感。

---

## 2026-11-20 - v1.0.15（修正叫出面板誤觸發請勿打擾）

### 目標
修正叫出浮動面板時，Windows 11 自動開啟「請勿打擾」的問題。

### 變更
- `Helpers/Win32Interop.cs`：新增 `SwpFrameChanged`、`SetWindowRectPhysical()` 與 `RefreshWindowFrame()`。
- `Views/OverlayWindow.xaml.cs`：標註覆蓋層不再剛好滿版，於右／下保留 1 實體像素；`SourceInitialized`／`Loaded` 與顯示後以 `SetWindowRectPhysical()` 精確套用實體像素矩形，並重套 tool window frame。
- `Services/OverlayManager.cs`：覆蓋層顯示後呼叫 `RefreshMonitorBounds()`。

### 問題與解法
- 覆蓋整個螢幕、透明且最上層的 `OverlayWindow` 被 Windows 全螢幕啟發式誤判為全螢幕 App，因而自動開啟請勿打擾。
- WPF DIP 尺寸換算可能因捨入再次滿版 → 改以 `SetWindowPos` 設定實體像素矩形校正。
- 已知取捨：最右／最下 1 實體像素不支援標註。

### 待辦
- 需於 Windows 11 實機以通知中心確認叫出／收起面板不再自動開啟請勿打擾；若 1 像素內縮仍無效，評估改用工作區或僅在繪圖時顯示覆蓋層。

---

## 2026-11-20 - v1.0.14（效能與底層優化）

### 目標
在不改變既有功能行為的前提下，降低 UI 執行緒 I/O、每幀記憶體配置與不必要的系統呼叫，改善拖曳、手繪、雷射筆與放大鏡流暢度，並減少常駐耗電。

### 變更
- `Services/ConfigService.cs`：新增 `RequestSave()`／`FlushPendingSave()` 與 500ms 去抖動計時器，`Save()` 仍保持同步寫入（相容測試）；實作 `IDisposable`。
- `Views/ToolbarWindow.xaml.cs`：選色、線寬滑桿與拖曳存位置改用 `RequestSave()`。
- `ViewModels/SettingsViewModel.cs`：自動隱藏延遲改為僅去抖動存檔，不再觸發全量 `ConfigChanged` 重建。
- `Services/ThemeService.cs`：記錄已套用的主題與明暗模式，相同時直接返回。
- `App.xaml.cs`：單一實例喚醒改以 `WaitHandle.WaitAny` 阻塞等待取消；離開前 `FlushPendingSave()`。
- `Views/Controls/AnnotationCanvas.cs`：背景筆刷改為靜態快取，已完成標註改以 `DrawingGroup` 快取，僅在元素變更時重建。
- `Models/Annotations/*`：`FreehandStroke`、`ShapeAnnotation`、`TextAnnotation`、`StepNumberAnnotation` 於建構時快取筆刷／畫筆／`FormattedText`。
- `Views/Controls/LaserOverlayLayer.cs`：預建 32 段透明度筆刷調色盤，移除每幀配置。
- `Views/MagnifierWindow.xaml.cs`：游標位置與縮放未變時跳過 `BitBlt` 與 `WritePixels`。
- `Services/OverlayManager.cs`：僅在工具切換或覆蓋層重新顯示時重設 TopMost；顯示設定變更時主動清除監視器快取。
- `Helpers/DisplayMonitorHelper.cs`：新增執行緒安全監視器快取與 `InvalidateCache()`。

### 問題與解法
- 線寬滑桿每個 tick 都同步寫檔並觸發主題重載與工具按鈕重建，造成拖曳卡頓 → 以去抖動寫入與主題 early-return 消除。
- 手繪與雷射筆每幀重新建立筆刷、畫筆與文字物件 → 改為建構時快取，並以 `DrawingGroup` 避免重繪所有既有標註。
- 放大鏡即使游標靜止仍每 VSync 擷取畫面 → 加入來源座標與尺寸比對後跳過。

### 待辦
- 尚未於實體多螢幕混合 DPI 與低階 GPU 環境實測手繪與放大鏡流暢度。

---

## 2026-11-20 - v1.0.13（文件、授權與發行）

### 目標
補齊繁體中文專案文件、採用 MIT License，並建立公開 GitHub 儲存庫與 x64／ARM64 非 Self-Contained 發行檔。

### 變更
- `README.md`：改寫為完整專案說明，加入功能、下載、快速開始、系統需求、建置、專案結構、文件索引、隱私與授權。
- `LICENSE`：新增 MIT License（Copyright (c) 2026 mark216tw）。
- `RELEASE_NOTES.md`：更新為 v1.0.13 使用者發行說明與下載資訊。
- `CHANGELOG.md`：於 1.0.13 補上文件、授權與發行流程說明。
- `CONTRIBUTING.md`、`SECURITY.md`：新增貢獻流程與安全性政策。
- `docs/USER_GUIDE.md`：新增安裝、面板、工具、設定、快捷鍵、資料位置與疑難排解。
- `docs/ARCHITECTURE.md`：新增技術基線、啟動流程、視窗策略、DPI、標註模型、設定、主題、平台整合與限制。
- `docs/SYSTEM_DESIGN.md`：新增需求、使用案例、狀態模型、資料設計、互動流程、效能、測試與部署設計。
- `docs/BUILD_AND_RELEASE.md`：新增建置、測試、版本管理、x64／ARM64 發行與 GitHub Release 流程。
- `docs/FAQ.md`：新增安裝、操作、儲存、多螢幕、隱私與授權常見問題。

### 問題與解法
- 專案原先不是 Git 儲存庫，需先初始化並設定本機提交身分為 `mark216tw <mark216tw@gmail.com>`。
- 非 Self-Contained 發行需在文件中明確標示 .NET 10 Desktop Runtime 需求，避免使用者誤以為可直接執行。
- ARM64 發行以 `win-arm64` Runtime Identifier 建置，維持與 x64 相同的非 Self-Contained 形式。

### 待辦
- 尚未於實體 ARM64 裝置與混合 DPI 多螢幕環境進行完整手動驗證。

---

## 2026-11-20 - v1.0.13

### 目標
關閉浮動面板後，螢幕上的標註（含白板／黑板背景）一併隱藏；重新顯示面板時恢復原標註狀態。

### 變更
- `src/MiniScreenNotes/Services/OverlayManager.cs`：新增 `_overlaysVisible` 狀態、`Initialize(bool initialVisible = true)` 與 `SetOverlaysVisible(bool visible)`，隱藏時呼叫各 `OverlayWindow.Hide()` 並停止放大鏡，顯示器設定變更重建時亦遵守可見狀態。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml.cs`：新增 `PanelVisibilityChanged` 事件，於 `HideAndRememberPosition()` 發出 `false`、於 `ShowAndRestorePosition()`（含取消隱藏動畫中途）發出 `true`；邊緣自動收合小手把不影響覆蓋層顯示。
- `src/MiniScreenNotes/Views/OverlayWindow.xaml`：設定 `ShowActivated="False"`，避免重新顯示覆蓋層時搶奪視窗焦點。
- `src/MiniScreenNotes/App.xaml.cs`：串接 `ToolbarWindow.PanelVisibilityChanged` 至 `OverlayManager.SetOverlaysVisible`，並將 `!startMinimized` 傳入 `OverlayManager.Initialize`。
- `Directory.Build.props` & `CHANGELOG.md`：更新版本至 `1.0.13`。

### 問題與解法
- 透過直接控制 `OverlayWindow` 顯示／隱藏而非修改 `AnnotationSession.AnnotationsVisible`，避免覆寫使用者手動切換的「隱藏標註」狀態，同時能一併隱藏白板／黑板背景。

### 待辦
- 無。

---

## 2026-11-20 - v1.0.12

### 目標
修正啟動應用程式時，已吸附於螢幕邊緣的浮動面板不會自動隱藏的問題。

### 變更
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml.cs`：
  - 於 `StartExpandedSlideIn()` 吸附滑入動畫完成後，若滑鼠未停留在面板上（`!IsMouseOver`），自動呼叫 `ScheduleAutoHideIfDocked()` 啟動倒數收合。
  - 補齊 `ShowAndRestorePosition()`、`ApplyProfile()`（含切換組態與即時開關 `AutoHideWhenDocked`）及 `ExpandedContainer_MouseLeftButtonUp()` 的自動隱藏排程處理。
- `Directory.Build.props` & `CHANGELOG.md`：更新版本至 `1.0.12`。

### 問題與解法
- 原本僅於 `Window_MouseLeave` 與 `OptionPopup_Closed` 呼叫 `ScheduleAutoHideIfDocked()`，啟動後若滑鼠未曾經過面板則永遠不會觸發倒數 → 於展開滑入完成與相關狀態變更點補齊排程。

### 待辦
- 無。

---

## 2026-11-20 - v1.0.11

### 目標
取消滑鼠移入小手把後需停留 1 秒才展開的延遲機制，恢復即時展開。

### 變更
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml`：移除 `CollapsedHandle` 的 `MouseLeave`／`MouseLeftButtonUp` 綁定並還原 ToolTip。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml.cs`：移除 `_handleHoverExpandTimer` 與延遲展開邏輯，滑鼠移入小手把直接呼叫 `ExpandPanel()`。
- `Directory.Build.props` & `CHANGELOG.md`：更新版本至 `1.0.11`。

### 問題與解法
- 無。

### 待辦
- 無。

---

## 2026-11-20 - v1.0.10

### 目標
避免滑鼠經過螢幕邊緣誤觸展開，將小手把自動展開改為滑鼠停留 1 秒後觸發。

### 變更
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml`：為 `CollapsedHandle` 加入 `MouseLeave` 與 `MouseLeftButtonUp` 事件並更新 ToolTip 提示。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml.cs`：新增 1 秒延遲展開計時器 `_handleHoverExpandTimer`，移開小手把立即取消；點擊小手把仍可立即展開。
- `Directory.Build.props` & `CHANGELOG.md`：更新版本至 `1.0.10`。

### 問題與解法
- 無。

### 待辦
- 無。

---

## 2026-11-20 - v1.0.9

### 目標
為浮動面板的顯示與隱藏（含自動隱藏收合／展開）加入平滑的移入移出過渡動畫。

### 變更
- `src/MiniScreenNotes/Helpers/DockSnapHelper.cs`：新增 `GetSlideOffset()`，依吸附邊緣（完整移出該螢幕邊界）或浮動位置與方向（朝較近側滑動 24 DIP）計算位移向量。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml`：於根 `Grid` 啟用 `ClipToBounds="True"`，並為 `ExpandedContainer` 與 `CollapsedHandle` 加入具名 `TranslateTransform`。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml.cs`：
  - 為 `ShowAndRestorePosition()`、`HideAndRememberPosition()`、`CollapseToHandle()` 與 `ExpandPanel()` 加入移入（`220ms` `CubicEase.EaseOut`）／移出（`170ms` `CubicEase.EaseIn`）與小手把（`140ms`）滑動＋淡入淡出動畫。
  - 吸附邊緣時以 `TranslateTransform` 搭配 `ClipToBounds` 裁切，避免在多螢幕交界穿透至相鄰螢幕；浮動狀態以視窗座標微幅滑動並搭配透明度過渡。
  - 以版本號與狀態機支援動畫中途即時反向切換、拖曳中斷與正確座標持久化（不寫入動畫中途座標）。
- `tests/MiniScreenNotes.Tests/DockSnapHelperTests.cs`：新增 `GetSlideOffset` 吸附邊緣與浮動面板單元測試。
- `Directory.Build.props` & `CHANGELOG.md`：更新版本至 `1.0.9`。

### 問題與解法
- WPF `BeginAnimation` 預設 `HoldEnd` 會鎖定 `Left`／`Top`／`Opacity` 導致後續拖曳或定位無效 → 統一透過 `CancelActiveTransitions()` 先讀取當下值、解除動畫再寫回本地屬性。

### 待辦
- 無。

---

## 2026-11-20 - v1.0.8

### 目標
將主題顏色由原本 5 種擴充為 10 種，新增 5 種支援淺色／深色雙模式的色彩主題。

### 變更
- `src/MiniScreenNotes/Models/ThemeName.cs`：新增 `SunsetOrange`、`OceanTeal`、`SakuraPink`、`MochaBrown`、`SlateGray` 枚舉值。
- `src/MiniScreenNotes/Resources/Themes/`：新增 10 個 XAML 主題資源字典（5 種新主題各含 `.Light.xaml` 與 `.Dark.xaml`）。
- `src/MiniScreenNotes/Views/SettingsWindow.xaml`：於主題顏色 `ComboBox` 新增「暮光橘」、「海洋青」、「櫻花粉」、「摩卡棕」、「石墨灰」選項。
- `tests/MiniScreenNotes.Tests/ConfigServiceTests.cs`：新增 `AllThemes_ShouldPersistAndReloadCorrectly` 單元測試。
- `Directory.Build.props` & `CHANGELOG.md`：更新版本至 `1.0.8`。

### 問題與解法
- 無。

### 待辦
- 無。

---

## 2026-11-20 - v1.0.7

### 目標
將清除工具改為掃把圖示、簡化應用程式圖示設計，並在設定頁面組態列新增「重新命名」按鈕。

### 變更
- `src/MiniScreenNotes/Resources/Icons.xaml`：將 `Icon_Clear` 由垃圾桶改為 24x24 掃把向量圖示。
- `src/MiniScreenNotes/Resources/AppIcon.ico`：重繪極簡風格多解析度（16/24/32/48/64/128/256 px）應用程式圖示（琥珀暖色圓角底＋白色斜筆與單一底線）。
- `src/MiniScreenNotes/Views/SettingsWindow.xaml` & `SettingsWindow.xaml.cs` & `ViewModels/SettingsViewModel.cs`：
  - 於組態操作列「刪除」旁新增「重新命名」按鈕。
  - 點擊後開啟跟隨目前主題的重新命名對話框，確認後透過 `RenameSelectedProfile()` 即時刷新下拉選單與系統匣並存檔。
- `tests/MiniScreenNotes.Tests/ConfigServiceTests.cs`：新增 `RenameSelectedProfile_ShouldUpdateNameAndPersist` 單元測試。

### 問題與解法
- `PanelProfile` 為純資料模型未實作 `INotifyPropertyChanged`，直接修改 `Name` 不會觸發 `ComboBox` 顯示文字更新 → 於 `RenameSelectedProfile()` 中重新插入 `Profiles` 對應索引並觸發 `OnPropertyChanged(nameof(SelectedProfile))`，兼顧即時刷新與純模型序列化。

### 待辦
- 無。

---

## 2026-11-19 - v1.0.6

### 目標
修正設定頁面下拉選單文字垂直對齊、新增「特小」面板尺寸、簡化每列／每行工具數量上限為單一數值，並移除組態下拉選單右側的組態名稱輸入框。

### 變更
- `src/MiniScreenNotes/Views/SettingsWindow.xaml`：
  - 為「面板尺寸與方向」及其他下拉選單補齊 `Height="28"`、`VerticalAlignment="Center"` 與 `VerticalContentAlignment="Center"`，修正選項文字貼齊上緣問題。
  - 面板大小下拉選單新增「特小 (24px)」選項。
  - 工具數量上限改為單一輸入框（綁定 `MaxItemsPerLine`）。
  - 移除面板組態下拉選單右側的組態名稱 `TextBox`。
- `src/MiniScreenNotes/Models/PanelSize.cs` & `Models/PanelProfile.cs` & `Views/ToolbarWindow.xaml.cs`：
  - 新增 `PanelSize.ExtraSmall`（按鈕 `24px`、圖示 `12px`、精簡控制按鈕 `16px`／圖示 `10px`）。
  - 將 `SmallMaxItemsPerLine`／`MediumMaxItemsPerLine`／`LargeMaxItemsPerLine` 合併為單一 `MaxItemsPerLine`（預設 `9`）。
- `src/MiniScreenNotes/ViewModels/SettingsViewModel.cs`：移除 `ProfileName` 與分尺寸上限屬性，改為單一 `MaxItemsPerLine`（範圍 `1–26`）。
- `tests/MiniScreenNotes.Tests/ConfigServiceTests.cs`：新增 `ExtraSmall` 尺寸與單一 `MaxItemsPerLine` 持久化測試。

### 問題與解法
- `SettingsWindow.xaml` 的 4 個 `ComboBox` 位於 `Height="36"` 的 `Grid` 列中且未設 `VerticalAlignment` 與 `VerticalContentAlignment`，導致下拉選單撐滿整列且文字預設靠上對齊 → 統一加上 `Height="28"`、`VerticalAlignment="Center"`、`VerticalContentAlignment="Center"`。

### 待辦
- 無。

---

## 2026-11-19 - v1.0.5

### 目標
將游標模式改為獨立開關並支援滑鼠中鍵全域切換、修正標註工具重複點擊取消選取問題、移除拖曳圖示改為面板空白處拖曳、聚光燈與放大鏡靠近面板自動顯示滑鼠，並修正線寬設定按鈕超出視窗右緣問題。

### 變更
- `src/MiniScreenNotes/Services/AnnotationSession.cs`：將 `IsCursorMode` 與 `SelectedAnnotationTool` 拆分為獨立狀態，新增 `ToggleCursorMode()` 與 `SelectAnnotationTool()`；關閉游標模式時自動恢復原已選標註工具。
- `src/MiniScreenNotes/Helpers/Win32Interop.cs` & `Services/GlobalMouseHookService.cs` & `App.xaml.cs`：新增 `WH_MOUSE_LL` 低階全域滑鼠掛鉤，於面板顯示時按滑鼠中鍵即可在任意視窗即時切換游標模式開關（面板隱藏時完全透傳不干擾）。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml` & `ToolbarWindow.xaml.cs`：
  - 移除 `DragHandle` 拖曳圖示，改由 `ExpandedContainer` 偵測非按鈕區域（空白處）即可直接按住拖曳面板。
  - 游標按鈕獨立反映 `IsCursorMode`，其餘標註工具反映 `SelectedAnnotationTool`，點擊已啟用的標註工具第二下仍維持 ON 狀態。
  - 修正線寬設定 Popup 寬度（`200` → `220`）與預設按鈕內距（`Padding="4,5"`），解決「特粗」按鈕超出右緣問題。
- `src/MiniScreenNotes/Views/OverlayWindow.xaml.cs` & `Services/OverlayManager.cs`：聚光燈與放大鏡模式於滑鼠進入面板（含自動隱藏手把）周圍 `48` DIP 範圍內時自動切換顯示箭頭游標。
- `tests/MiniScreenNotes.Tests/UndoRedoTests.cs` & `ConfigServiceTests.cs`：新增游標模式獨立切換與 `GlobalMouseHookService` DI 解析測試。

### 問題與解法
- 游標模式開啟時 `OverlayWindow` 為點擊穿透（`WS_EX_TRANSPARENT`），無法接收滑鼠中鍵事件 → 透過 `WH_MOUSE_LL` 掛鉤搭配 `Dispatcher.BeginInvoke` 處理中鍵切換，且僅在面板可見時攔截。
- WPF `ToggleButton` 重複點擊同一工具會自動將 `IsChecked` 反轉為 `false`，而 `AnnotationSession` 因工具未變動未觸發 `StateChanged` → 於 `HandleToolClick` 後一律呼叫 `SyncToolButtonStates()` 強制保持 ON 狀態。
- 線寬 Popup `UniformGrid(Columns=4)` 內四個按鈕繼承 `SecondaryButtonStyle` 的 `Padding="12,6"`，導致寬度超出 `200px` 容器 → 縮小內距並微調容器寬度至 `220px`。

### 待辦
- 待實體環境驗證不同滑鼠驅動下中鍵切換游標模式與聚光燈／放大鏡靠近邊緣手把的游標切換流暢度。

---

## 2026-11-19 - v1.0.4

### 目標
微調放大鏡視野範圍、修正顏色選擇器懸停變藍與工具圖示偏左上對齊問題、將直向面板控制按鈕移至最下方、讓每個面板組態獨立記憶位置，並重繪應用程式圖示。

### 變更
- `src/MiniScreenNotes/Views/MagnifierWindow.xaml` & `MagnifierWindow.xaml.cs`：放大鏡預設直徑由 `360` DIP 縮小至 `300` DIP（滾輪調整範圍 `200–500` DIP）。
- `src/MiniScreenNotes/Resources/Styles.xaml` & `Views/ToolbarWindow.xaml.cs`：新增 `ColorSwatchButtonStyle` 自訂模板，避免 WPF 預設 Aero2 按鈕樣式在滑鼠懸停時將畫筆色票覆蓋為藍色；改以主題強調色邊框提示懸停。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml.cs`：
  - 移除 `BuildIconContent` 中 `Viewbox > Canvas > Path(Stretch=Uniform)` 的多餘 `Canvas` 包裝，改由 `Viewbox` 直接置中縮放 `Path`，修正工具圖示偏移至左上角的問題。
  - 面板為直向模式（單欄與多欄）時，將拖曳移動把手、設定與關閉按鈕統一配置於面板最下方（`Dock.Bottom`）。
  - 切換組態時自動保存舊組態並還原新組態各自的面板座標與吸附邊緣。
- `src/MiniScreenNotes/Models/PanelProfile.cs` & `Services/ConfigService.cs`：於 `PanelProfile` 新增 `LastPanelLeft`、`LastPanelTop`、`LastDockEdge`，並在載入舊設定檔時自動遷移全域位置至目前組態。
- `src/MiniScreenNotes/Resources/AppIcon.ico` & `Views/{ToolbarWindow,SettingsWindow,AboutWindow}.xaml`：重繪多解析度（16/24/32/48/64/128/256 px）螢幕＋標註畫筆應用程式圖示，並套用至各視窗。
- `tests/MiniScreenNotes.Tests/ConfigServiceTests.cs`：新增多組態獨立記憶面板位置之單元測試。

### 問題與解法
- `Path` 設 `Stretch=Uniform` 放在無對齊能力的 `24x24` `Canvas` 中，會將幾何外框歸零至 `(0,0)` 且維持原尺寸，導致所有工具圖示偏向左上 → 移除 `Canvas`，直接由 `Viewbox(Stretch=Uniform)` 置中縮放 `Path`。
- 畫筆色票按鈕未指定 `Style`，沿用 WPF 預設 Aero2 `ControlTemplate` 觸發 `IsMouseOver` 藍色背景 → 建立 `ColorSwatchButtonStyle` 保留 `TemplateBinding Background`。

### 待辦
- 待實機確認各 DPI 與系統匣 16px 圖示顯示銳利度。

---

## 2026-11-19 - v1.0.3

### 目標
修正放大鏡重疊顯示原線條與移動抖動、加大放大鏡視野範圍、修正繪圖模式無法從自動隱藏手把喚出面板，以及精簡面板固定按鈕空間。

### 變更
- `src/MiniScreenNotes/Helpers/Win32Interop.cs`：新增 `SetWindowPos`、`GetWindowRect`、`CreateDIBSection` 與 `BringToTopMostNoActivate` / `MoveWindowAtomicNoZOrder`。
- `src/MiniScreenNotes/Views/MagnifierWindow.xaml` & `MagnifierWindow.xaml.cs`：
  - 放大鏡預設視野直徑由 `230` DIP 擴大至 `360` DIP，並支援 `Ctrl + 滾輪` 即時調整範圍（240–560 DIP）。
  - 改以 `CompositionTarget.Rendering` + 可重用 `CreateDIBSection` / `WriteableBitmap`（零逐幀配置）+ 奇數物理像素對齊 + 單一原子 `SetWindowPos` 實體像素定位，消除移動抖動並修正高 DPI 縮放比例。
- `src/MiniScreenNotes/Views/OverlayWindow.xaml.cs`、`Services/OverlayManager.cs`、`App.xaml.cs`：於 `OverlayWindow` 啟用/點擊與工具切換時強制維持 `ToolbarWindow > MagnifierWindow > OverlayWindow` 的 Topmost Z-Order，徹底解決放大鏡上方疊出 1x 原線條以及繪圖工具狀態下無法移至手把展開面板選工具的問題。
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml` & `ToolbarWindow.xaml.cs`：將拖曳把手、設定與關閉按鈕整合為自適應精簡控制條（橫向多列靠右垂直窄條、直向多欄靠頂水平窄條、單列/單欄分離不撐高/不撐寬），大幅減少固定按鈕占用空間。

### 問題與解法
- `OverlayWindow` 為 `Topmost=True` 且非點擊穿透，繪圖或滾輪操作時會取得焦點並躍升至 `MagnifierWindow` 與 `ToolbarWindow` 之上，導致放大鏡內出現 1x 原線條重疊，且收合手把被畫布遮擋 → 透過 `OverlayActivated` 與 `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)` 維護嚴格三層 Z-Order。
- 放大鏡原以 `DispatcherTimer(16ms)` 搭配分開設定 `Left`/`Top` 與逐幀建立 HBITMAP，造成 VSync 節拍差與分段位移抖動 → 改為 VSync 同步渲染、重用 DIB 緩衝與單一原子 `SetWindowPos`。

### 待辦
- 待實體多螢幕混合 DPI 環境實測放大鏡跨螢幕移動與高倍縮放流暢度。

---

## 2026-11-19 - v1.0.2

### 目標
修正 `ToolbarWindow.xaml` 啟動時 XAML 解析例外。

### 變更
- `src/MiniScreenNotes/Views/ToolbarWindow.xaml`：將線寬預設按鈕列的無效 `HorizontalAlignment="SpaceBetween"` 改為 `<UniformGrid Columns="4">`。

### 問題與解法
- 透過 `%AppData%\MiniScreenNotes\error.log` 定位到 `XamlParseException: SpaceBetween is not a valid value for HorizontalAlignment` → 改用 WPF 原生 `UniformGrid` 均分排列。

---

## 2026-11-19 - v1.0.1

### 目標
修正應用程式啟動失敗（無畫面且殘留背景進程）問題。

### 變更
- `src/MiniScreenNotes/Services/ConfigService.cs`：補上 `[ActivatorUtilitiesConstructor]` 單參數 DI 建構子，避免 `Microsoft.Extensions.DependencyInjection` 解析 `string? customConfigPath` 失敗。
- `src/MiniScreenNotes/App.xaml.cs`：將 `DispatcherUnhandledException` 移至啟動完成後註冊，啟動期例外改以 `try-catch` 寫入 `%AppData%\MiniScreenNotes\error.log` 並提示後正常退出，避免形成無畫面的背景殘留進程。
- `tests/MiniScreenNotes.Tests/ConfigServiceTests.cs`：新增 DI 容器解析核心服務單元測試。

### 問題與解法
- 啟動時 DI 解析 `ConfigService` 拋出例外，卻被提前註冊的 `DispatcherUnhandledException` (`args.Handled = true`) 吞掉，加上 `ShutdownMode="OnExplicitShutdown"` 導致程式無畫面卡在背景並佔住單一實例 Mutex → 補上單參數 DI 建構子並強化啟動期例外處理。

---

## 2026-11-19 - v1.0.0

### 目標
完成 Windows 螢幕標註工具「mini螢幕筆記」(MiniScreenNotes) 第一版架構設計、完整功能實作與單元測試。

### 變更
- `Directory.Build.props` / `Directory.Packages.props` / `MiniScreenNotes.sln`：建立 .NET 10 (`net10.0-windows`) 專案與 Central Package Management，啟用 `Nullable` 與 `TreatWarningsAsErrors`。
- `src/MiniScreenNotes/Models/`：建立工具列舉、多組態 `PanelProfile`、`AppConfig` 與向量標註模型（平滑畫筆、不疊色螢光筆、幾何圖形、打勾/打叉圖章、文字、步驟編號與 Undo/Redo 命令）。
- `src/MiniScreenNotes/Helpers/`：封裝 Win32 P/Invoke（點擊穿透、排除截圖 `WDA_EXCLUDEFROMCAPTURE`、DWM 深色標題列、全域熱鍵、多螢幕 DPI 列舉、BitBlt 擷取）與 `DockSnapHelper` 吸附邏輯。
- `src/MiniScreenNotes/Services/`：實作 `ConfigService`、`ThemeService`、`StartupService`（開機自動啟動）、`AnnotationSession`、`GlobalHotkeyService`、`ScreenCaptureService`（含一鍵快速儲存）、`OverlayManager` 與 `TrayService`。
- `src/MiniScreenNotes/Views/` & `ViewModels/`：實作多螢幕 `OverlayWindow`、`MagnifierWindow`（1x~10x 無遞迴放大鏡）、`ToolbarWindow`（可拖曳吸附、自動隱藏小手把、多列折行）、`SettingsWindow`、`AboutWindow` 與 `ToastWindow`。
- `src/MiniScreenNotes/Resources/`：建立多解析度 `AppIcon.ico`、`Strings.resx` 繁體中文資源、`Icons.xaml`、`Styles.xaml` 與 5 組雙模式主題（含黃色護眼）。
- `tests/MiniScreenNotes.Tests/`：新增 `UndoRedoTests`、`ConfigServiceTests`、`DockSnapHelperTests`。

### 問題與解法
- `SupportedOSPlatformVersion 10.0.17763.0` 高於 `net10.0-windows` 預設 `TargetPlatformVersion 7.0` 導致 `NETSDK1135` → 移除 `.csproj` 中的 `SupportedOSPlatformVersion`，維持 `net10.0-windows`。
- 啟用 `UseWindowsForms` + `ImplicitUsings` 導致 `Point`/`Size` 與 WPF 衝突（`CS0104`） → 於 `.csproj` 加入 `<Using Remove="System.Drawing" />` 與 `<Using Remove="System.Windows.Forms" />`。
- `Strings.Designer.cs` 觸發 `CS8669` → 於檔頭加入 `#nullable enable`。

### 待辦
- 尚未於實體多螢幕混合 DPI（如 150% + 100%）與 Windows 10 1809 環境進行 UI 手動實機驗證。
