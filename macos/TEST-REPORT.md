# NXNative Easy Builder for Mac 1.0.0

Built on Linux for macOS Apple Silicon using .NET SDK 10.0.403, .NET 10.0.14 runtime and Avalonia 11.3.8. The app bundle uses an ad-hoc signature. Apple notarization and Gatekeeper acceptance have not been tested.

## Results

| Area | Result |
| --- | --- |
| Build | Release publish completed for `osx-arm64`. |
| Repository and repair | Fixture tests passed for reading the latest commit, staging, activation, reuse, hash mismatch, repair and a failed candidate. |
| Live GitHub | Download and extraction passed at commit `6d928fd14471704832fc9f5ae20b39d3bb8d8f9e`. The four locked tool file hashes matched and the SPIRV-Cross executable mode was preserved. The checkout expects Xcode 27.2. |
| Pipeline integration | Child-process fixtures passed for IPA and Mac app arguments, Unicode and spaces in paths, scoped environment, progress, output verification, nonzero exit and cancellation. |
| GUI | Headless Avalonia and Skia rendering passed at 1040×800, 900×670, 640×540 and 1280×900. Dragging the splitter enlarged the log. |
| Bundle | Static checks passed for the ARM64 Mach-O, its ad-hoc code page hashes, its Info.plist and resource seals, all three native libraries and the 200 entries in the .NET payload. |
| Native end-to-end | Pending. Launch from Finder, native file picker, Xcode and Homebrew setup, Metal compilation, and actual game IPA and Mac app builds require an Apple Silicon Mac. |

The final local run passed 27 assertions. Two additional assertions passed against the live GitHub checkout. Build outputs in the child-process tests were synthetic fixtures.

## Reproduce

The source archive includes `pack.sh`, the workflow and GUI tests, and `scripts/verify_bundle.py`. The app obtains Xcode components through the user's own Xcode installation.
