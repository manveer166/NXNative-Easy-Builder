# NXNativeWindows Easy Installer v2.0.2

Easy Installer Created by **iSolution**. Original NXNativeWindows by **zenvyzenshiro**: https://github.com/zenvyzenshiro/NXNativeWindows. Based on NXNative by seedds.

A single-file Windows installer with a new Avalonia/Skia desktop interface. The .NET runtime, UI engine, fonts and native drawing libraries are bundled. No browser or WebView runtime is required.

**Validation:** production UI controls were actually rendered in headless Skia; 22 layout, resize, density, pointer-drag and managed Browse interaction cases passed. The core suite passed 93 tests, including a live GitHub download/extraction. Native Windows startup, live Apple package installation and a full authorized SDK build still require Windows verification. Windows ARM64 toolchain support remains experimental. See `docs/TEST_REPORT.md`.

## Run

Choose `NXNativeWindowsEasyInstaller-v2.0.2-win-x64.exe` for Windows Intel/AMD or `NXNativeWindowsEasyInstaller-v2.0.2-win-arm64.exe` for Windows 11 ARM64. The visible window title and header show v2.0.2.

**Browse** uses a managed in-app folder picker, without loading the native Explorer/COM dialog. Navigate drives/folders, paste a path, or enter a new installation subfolder. Cancel keeps the previous path. The XIP picker selects files: click a completed `.xip`, then **Open**, or double click it. Folders are navigation only. There is no output-folder field. Open remains visible in compact windows; errors clear after a valid selection.

Click **Install Everything**. The app checks GitHub's actual default-branch HEAD, downloads an archive pinned to that commit, extracts it automatically and installs/checks the toolchain. Windows may show UAC for system package installers. Use the same installation folder to retain completed setup work.

## Layout and the large log

- Components and Activity Log are visible together, with independent scrolling.
- The log takes about 60% of the work area by default. Drag the horizontal divider upward to enlarge it further.
- Text wrapping is enabled for the log; disabling Wrap enables horizontal scrolling.
- The actual text layout drives control heights. Component title, description and status have separate layout cells.
- Small windows use a compact header and a scrollable minimum content surface. They do not compress controls into overlapping fixed-height rows.
- Inter fonts are bundled so text metrics do not depend on which optional Windows fonts are installed.

## Apple setup: select only the XIP

1. Open **Apple Setup** and choose your officially obtained Xcode `.xip`. No SDK output-folder picker is required.
2. An existing valid SDK is reused; otherwise the XIP is extracted automatically. A bundled adapter runs the current upstream extractor without modifying repository files. On Windows it uses normalized extended-length paths and tests file I/O before extraction. Relative framework aliases are normalized before copying. SDK files with unresolved hard links/placeholders are rejected.
3. Extraction uses a short staging directory, validates the result, then activates it at `sdk/iPhoneOS.sdk`. Staging from failed/cancelled attempts is removed and never selected as an installed SDK. Legacy `sdk/extract-*` output is treated as unverified and extracted again from the selected XIP; an active SDK is retained until replacement succeeds.
4. A valid SDK must include `SDKSettings.json` with version 26+ and the Foundation/UIKit framework headers. An existing repository folder is not accepted as an SDK. A mistaken repository-root selection is searched for a real SDK under `win/sdk/out` and repaired when possible.
5. Metal tools are detected from their existing configured/default installation or a previously verified managed extraction.
6. If Metal is missing, download **Metal Developer Tools for Windows** from Apple's official portal into Downloads or beside the XIP. The app detects completed EXE/MSI/ZIP packages without a Metal folder picker. Signed EXE/MSI packages open Apple's own installer/license UI. ZIPs require confirmation of their applicable terms, safe extraction and valid Apple signatures on the compiler/linker before activation.

Xcode XIP and Metal for Windows are separate Apple downloads. The installer does not collect Apple login credentials or bypass authenticated download gates. When login/download is required it opens Apple's official page. Native Apple installer, UAC and license screens remain under user control.

The standard Xcode and Apple SDKs agreement does not itself grant SDK use on a Windows host. SDK extraction, verification and builder launch remain gated on the user's explicit attestation of separate applicable authorization. No Apple files or licenses are bundled or granted. XIP signature verification itself is not implemented; only officially obtained XIPs should be selected.

Official Apple URLs:

- https://developer.apple.com/download/all/?q=Xcode
- https://developer.apple.com/download/all/?q=metal%20developer%20tools%20for%20windows
- https://www.apple.com/legal/sla/docs/xcode.pdf

## Dependencies

The unchanged Python environment workflow uses Python 3.13 with a managed virtual environment and lz4, capstone, fonttools, pillow, unicorn and backports.zstd. Core dependencies use the official WinGet source: `Microsoft.VCRedist.2015+.x64`, `Python.Python.3.13`, `LLVM.LLVM`, `Microsoft.DotNet.SDK.10`, `KhronosGroup.VulkanSDK`, and `Meta.Zstandard`.

The toolchain is x64 on both supported hosts. Windows 11 ARM64 runs these tools through x64 emulation and must pass the same execution probes. Windows 10 ARM64 and x86 hosts are blocked. No hash-verification bypass or automatic Windows reboot is used.

## Update, repair and launch

**Check for Updates** compares the installed repository commit with HEAD. **Update Everything** downloads/extracts a new commit, keeps the old checkout, carries forward builder selections/custom configuration and checks dependencies. It checks the upstream repository, not this installer's own EXE.

**Install / Repair** checks SHA-256 hashes of tracked source files. If the current commit is intact, the checkout is reused without downloading or extracting it again. Missing or modified source is restored from the checksum-verified cached ZIP, or a fresh pinned download if needed. Older installations with a cached ZIP are migrated to this integrity check automatically. Builder settings and custom toolchain JSON are preserved. Usable dependencies are reused and supported failures are repaired. Broken toolchain JSON and virtual environments are backed up. Old backups are retained. Explicit tool paths are written to `win/toolchain.json`, user `NXW_*` variables and deduplicated user PATH additions.

The **Launch NXNativeWindows IPA Builder** button appears after required checks pass. Checks include upstream doctor, Python/Tk, .NET extractor build/run, Vulkan conversion, Zstandard round trip, and compilation/linking with the authorized SDK/Metal tools. Installation checks do not test game IPA builds, signing, sideloading or playback.

Repository connection/download status is logged every 5 seconds, including bytes when Content-Length is absent. Extraction shows file/byte counts. Download limits: 60 seconds for response headers, 45 seconds without data, 15 minutes total, at most 3 attempts. Stop cancels the operation, discards partial downloads and retains the previous checkout. The source manifest is a local integrity baseline, not a GitHub-signed authenticity proof.

Session logs are stored under `logs/`; `health-report.json` records the installation probes. The large log has an **Open Logs** button. A desktop shortcut is created after successful setup when possible.

## Build and reproduce the checks

Build scripts require Python 3.13 and .NET 10 SDK; pinned Avalonia 11.3.8 UI dependencies are restored from NuGet. Tmds.DBus.Protocol is pinned to the patched 0.21.3 release. Source includes all third-party notices.

```powershell
./scripts/build.ps1 -Architecture both
```

Core tests, with optional real GitHub download:

```text
dotnet run --project tests/NXInstaller.Tests -c Release -- --live --sdk-fixture . --python python --report artifacts/core-tests.json
```

SDK extraction tests use synthetic XAR/PBZX/CPIO fixtures and the included MIT-licensed upstream extractor baseline. No Apple SDK bytes are included. Python 3.13.17 was tested on Linux; the real Win32 I/O test is skipped there and remains a Windows verification step.

```text
python -m unittest discover -s tests -p test_sdk_extract.py -v
```

Render the actual production UI, check text bounds and overlaps, exercise the splitter with pointer input, and save PNG frames:

```text
dotnet run --project tests/NXInstaller.UiTests -c Release -- artifacts/ui-render-tests
```

The headless renderer uses the real production controls and Skia/fonts, not a parallel HTML mockup. Render densities 1.25, 1.5 and 2 simulate corresponding physical viewport/DIP combinations. This does not constitute testing actual Windows monitor-DPI transitions or native OS dialogs.

The source CI workflow runs these UI checks on Linux and builds both Windows architectures, with a native x64 `--smoke-ui --snapshot PATH` check. CI also sets up Python 3.13 and runs SDK regression tests, including real Win32 long-path I/O on the x64 runner. CI was configured but not run during this delivery. `--verify --root PATH` runs installation verification; `--launch --root PATH` serves the desktop shortcut.

EXEs are unsigned because no Authenticode certificate is available. Do not disable Windows security features. MIT license for this installer; dependency and Apple terms remain their own.
