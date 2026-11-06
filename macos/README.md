# NXNative Easy Builder for Mac

Created by iSolution. Original NXNative by seedds.

This Apple Silicon app uses the native NXNative build script for iPhone and iPad IPAs and macOS game apps. It checks the upstream `main` branch and downloads the archive for the selected commit. **Install Everything** prepares the dependencies and activates the new checkout after its shader toolchain passes validation. **Check Updates** only looks up the current commit.

## Requirements

- Apple Silicon and macOS 26.6 or later. The upstream revision checked for this release expects Xcode 27.2 and its Metal Toolchain. The app reads the Xcode requirement from each downloaded checkout.
- Install Xcode from Apple and open it once to complete its first-launch setup and license prompts. The builder selects the matching Xcode for its own subprocesses. The iOS platform is required for an IPA.
- Apple Silicon Homebrew in `/opt/homebrew`. **Set Up Homebrew** starts the official installer in Terminal for its interactive prompts. The app uses Homebrew to install missing tools and manages a Python environment for NXNative.
- A writable game folder, enough space for the build cache and output, and your game dump. For NSP, XCI, NSZ and XCZ, select your `prod.keys` file. The builder stores its path in preferences and passes the file to the local pipeline.

## Build a game

1. Click **Install Everything** and follow the Xcode or Homebrew steps shown if required.
2. Select the game folder, or choose a `.nup` file, then select `prod.keys` if needed.
3. Choose **Build IPA** or **Build Mac App**. **Output → Change** sets a separate output folder. **Reveal Output** selects the finished file in Finder.

Keep the same game folder to reuse the `<title ID>_build` cache when its inputs still match. The optional GPU pipeline prebuild starts unchecked. The game can build those pipelines on first launch. Drag the divider above **Activity Log** upward for a larger log. **Open Logs** opens the builder's session logs. NXNative also keeps each build stage's output in the game's build folder.

Builds, keys and game dumps are local. The bundle contains the builder and its .NET runtime. Xcode, SDKs and Metal components come from Apple. Python packages and Homebrew tools are installed by their respective package managers.

## Build the builder from source

Use .NET SDK 10.0.403 or a later compatible .NET 10 SDK and Python 3. On macOS, the packaging script uses the local `codesign` for an ad-hoc signature.

```bash
bash pack.sh
```

On Linux, provide `rcodesign` in `PATH` or via `NXMAC_RCODESIGN`. This produces a macOS arm64 app and verifies the main Mach-O signature. See `Assets/ThirdPartyNotices.txt` for the runtime and UI component licenses.

The command below runs the workflow fixtures and headless GUI checks.

```bash
dotnet run --project tests/NXNativeEasyBuilder.Tests.csproj -c Release -- --ui ./ui-evidence
```

Add `--live` before `./ui-evidence` to test the GitHub lookup and archive extraction with the current upstream revision. Set `NXMAC_TEST_PYTHON` to the path of Python 3 if `/usr/bin/python3` is unavailable.

## Upstream references

- [NXNative and its setup instructions](https://github.com/seedds/nxnative)
- [NXNative front-end protocol](https://github.com/seedds/nxnative/blob/main/docs/frontends.md)
- [Xcode system requirements](https://developer.apple.com/xcode/system-requirements/)
- [Xcode optional components](https://developer.apple.com/documentation/xcode/downloading-and-installing-additional-xcode-components)
- [Apple's instructions for opening apps](https://support.apple.com/en-us/102445)
