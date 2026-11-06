# Synthetic SDK tests

`upstream_extract_xip.py` is the unchanged MIT-licensed extractor baseline from zenvyzenshiro/NXNativeWindows commit `269d61ff974b2e8e073d7357466ae130c3f6812f`. Its license is in LICENSE.NXNativeWindows.txt.

SHA-256: `61214843147d6ff2ce675b575ab493a675b3a4baa56c44c1157f0cc1b92126c6`

The production installer delegates to the freshly downloaded repository extractor. This baseline is used only to make regression tests reproducible without network access. XAR/PBZX/CPIO test files and all header contents are generated from synthetic bytes; no Apple SDK or Xcode binaries are included.
