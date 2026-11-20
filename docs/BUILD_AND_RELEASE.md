# å»ºç½®?‡ç™¼è¡?

?¬æ?ä»¶èªª?æœ¬æ©Ÿé??¼ã€æ¸¬è©¦ã€ç”¢??x64ï¼ARM64 ?¼è?æª”è?å»ºç? GitHub Release ?„æ?ç¨‹ã€?

## 1. ?ç½®?€æ±?

| ?…ç›® | ?ˆæœ¬ |
| --- | --- |
| .NET SDK | 10.0 |
| Windows | Windows 10 1809 ä»¥ä? |
| Git | ä»»æ?è¿‘æ??ˆæœ¬ |
| GitHub CLIï¼ˆç™¼è¡Œç”¨ï¼?| 2.xï¼Œé???`gh auth login` |

ç¢ºè??°å?ï¼?

```powershell
dotnet --version
git --version
gh auth status
```

## 2. ?„å??å»ºç½®è?æ¸¬è©¦

ä¸€å¾‹å?ä½¿ç”¨ Debug è¨­å??‹ç™¼?‡é?è­‰ï?

```powershell
dotnet restore MiniScreenNotes.sln
dotnet build MiniScreenNotes.sln
dotnet test MiniScreenNotes.sln
```

?¼å??–æª¢?¥ï?

```powershell
dotnet format MiniScreenNotes.sln --verify-no-changes
```

- å°ˆæ??Ÿç”¨ `TreatWarningsAsErrors`ï¼Œè­¦?Šæ??´æ¥è®“å»ºç½®å¤±?—ã€?
- æ¸¬è©¦ä½¿ç”¨ xUnitï¼Œä???`tests/MiniScreenNotes.Tests`??
- Debug è¼¸å‡ºä½æ–¼ `src/MiniScreenNotes/bin/Debug/net10.0-windows/`??

## 3. ?ˆæœ¬ç®¡ç?

?ˆæœ¬?†ä¸­??`Directory.Build.props`ï¼?

```xml
<Version>1.0.19</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.19.0</FileVersion>
```

è¦å?ï¼?

- `Version` ??`ä¸?æ¬?ä¿®`??
- æ¯æ¬¡?Ÿèƒ½è®Šæ›´?å? `Version` ??`FileVersion`??
- `AssemblyVersion` ?…åœ¨ä¸»æ¬¡?ˆè??´æ?èª¿æ•´??
- ?Œæ­¥?´æ–° `CHANGELOG.md`?`RELEASE_NOTES.md` ??`docs/DEV_LOG.md`??

## 4. ?¢ç???Self-Contained ?¼è?æª?

?¬å?æ¡ˆåª?ä???Self-Contained zipï¼?*ä¸æ?ä¾›å?è£ç?å¼?*??

### 4.1 x64

```powershell
$version = "1.0.19"
dotnet publish src/MiniScreenNotes/MiniScreenNotes.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o "artifacts/publish/MiniScreenNotes-v$version-win-x64"
```

### 4.2 ARM64

```powershell
$version = "1.0.19"
dotnet publish src/MiniScreenNotes/MiniScreenNotes.csproj `
  -c Release `
  -r win-arm64 `
  --self-contained false `
  -o "artifacts/publish/MiniScreenNotes-v$version-win-arm64"
```

### 4.3 ?¼è??§å®¹

è¼¸å‡ºè³‡æ?å¤¾æ??…å«ï¼?

```text
MiniScreenNotes.exe
MiniScreenNotes.dll
MiniScreenNotes.deps.json
MiniScreenNotes.runtimeconfig.json
Microsoft.Extensions.*.dll
?¦å…¶ä»–ç›¸ä¾ç?ä»?
```

ä½¿ç”¨??Self-Contained æ¨¡å??‚ï?ä¸æ??…å« .NET ?·è??°å??‚ä½¿?¨è€…é?å®‰è?å°æ??¶æ???.NET 10 Desktop Runtime??

`artifacts/` ??`publish/` å·²åœ¨ `.gitignore` ä¸­å¿½?¥ï?ä¸æ??äº¤?°å„²å­˜åº«??

## 5. ?“å? zip

å»ºè­°??staging ?®é?å»ºç??‚å±¤è³‡æ?å¤¾ï?è®“å?ç¸®æ?è§??å¾Œæ?æ¸…æ??„ç›®?„å?ç¨±ï?

```powershell
$version = "1.0.19"
$staging = "artifacts/staging"

New-Item -ItemType Directory -Force -Path "$staging/MiniScreenNotes-v$version-win-x64" | Out-Null
Copy-Item "artifacts/publish/MiniScreenNotes-v$version-win-x64/*" `
  "$staging/MiniScreenNotes-v$version-win-x64" -Recurse

Compress-Archive `
  -Path "$staging/MiniScreenNotes-v$version-win-x64" `
  -DestinationPath "artifacts/MiniScreenNotes-v$version-win-x64.zip" `
  -Force
```

ARM64 ä»¥ç›¸?Œæ–¹å¼å? `win-x64` ?¹ç‚º `win-arm64`??

?¥è??¨å?ç¸®æ??§é?ä¸ŠåŸ·è¡Œèªª?ï??¯åœ¨ staging è³‡æ?å¤¾å???`ä½¿ç”¨èªªæ?.txt`ï¼Œå…§å®¹æ??’ä½¿?¨è€…å?è£?.NET 10 Desktop Runtime??

## 6. å»ºç? GitHub Release

ç¢ºè?å·²æ?äº¤ä¸¦?¨é€æ??‰è??´ï?ä¸”ç??¬æ?ç±¤è? `Directory.Build.props` ä¸€?´ï?

```powershell
$version = "1.0.19"
git tag "v$version"
git push origin "v$version"

gh release create "v$version" `
  "artifacts/MiniScreenNotes-v$version-win-x64.zip" `
  "artifacts/MiniScreenNotes-v$version-win-arm64.zip" `
  --title "mini?¢å?ç­†è? v$version" `
  --notes-file RELEASE_NOTES.md
```

æ³¨æ?ï¼?

- ?ªä???x64 ??ARM64 ?„é? Self-Contained zip??
- ä¸å»ºç«?MSIX?MSI?EXE å®‰è?æª”ã€?
- `RELEASE_NOTES.md` ?ºçµ¦ä½¿ç”¨?…ç??„èªª?ï??§å®¹?€?…å«ç³»çµ±?€æ±‚è?ä¸‹è??¹å???

## 7. ?¼è??æª¢?¥æ???

- [ ] `dotnet build MiniScreenNotes.sln` ?å?
- [ ] `dotnet test MiniScreenNotes.sln` ?¨æ•¸?šé?
- [ ] `Directory.Build.props` ?ˆæœ¬æ­?¢º
- [ ] `CHANGELOG.md` å·²æ›´??
- [ ] `RELEASE_NOTES.md` å·²æ›´??
- [ ] `docs/DEV_LOG.md` å·²æ›´??
- [ ] x64 ??ARM64 ?¼è?æª”ç??¯å???
- [ ] zip æª”å??‡ç??¬ä???
- [ ] Release ?ªé?? å?è£æ?
- [ ] ?²å?åº«ç‚º?¬é?ä¸”å???MIT License

## 8. ?‘é›£?’è§£

| ?é? | ?•ç??¹å? |
| --- | --- |
| `NETSDK1135` | ç¢ºè??ªåœ¨ csproj è¨­å?é«˜æ–¼?®æ?æ¡†æ¶??`SupportedOSPlatformVersion` |
| WPF ??WinForms ?‹åˆ¥è¡ç? | csproj å·²ç§»??`System.Drawing` ??`System.Windows.Forms` ??implicit using |
| ARM64 ?¼è?å¤±æ? | ç¢ºè? SDK ??Runtime Pack ?¯æ´ `win-arm64`ï¼Œå?è¦æ??·è? `dotnet workload restore` |
| ä½¿ç”¨?…ç„¡æ³•å???| ç¢ºè?å·²å?è£?.NET 10 Desktop Runtimeï¼Œä??¶æ???zip ?¸ç¬¦ |
| SmartScreen è­¦å? | ?®å??ªç°½ç« ï?å±¬é??Ÿè??ºï??‡ä»¶å·²èªª??|
