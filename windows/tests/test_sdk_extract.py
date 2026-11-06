"""Regression fixtures contain synthetic bytes only, no Apple SDK or Xcode data."""
import contextlib
import hashlib
import importlib.util
import io
import json
import lzma
import os
import shutil
import struct
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("sdk_adapter", PROJECT / "src/NXInstaller.Core/Resources/sdk_extract_windows.py")
ADAPTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ADAPTER)
UPSTREAM = PROJECT / "tests/Fixtures/upstream_extract_xip.py"
PREFIX = "Xcode.app/Contents/Developer/Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk/"
LONG_HEADER = "System/Cryptexes/OS/System/Library/Frameworks/AuthenticationServices.framework/Headers/ASAccountAuthenticationModificationReplacePasswordWithSignInWithAppleRequest.h"
HEADER_BYTES = b"/* SYNTHETIC TEST HEADER: NO APPLE SOURCE */\n" + b"test " * 30


def cpio(entries):
    output = bytearray()
    for name, mode, inode, data in list(entries) + [("TRAILER!!!", 0, 0, b"")]:
        name = name.encode("utf-8") + b"\0"
        values = (inode, mode, 0, 0, 1, 0, len(data), 0, 0, 0, 0, len(name), 0)
        header = b"070701" + b"".join(f"{n:08x}".encode() for n in values)
        output += header + name + b"\0" * (-(len(header) + len(name)) % 4)
        output += data + b"\0" * (-len(data) % 4)
    return bytes(output)


def fixture_xip(path, *, missing_carrier=False, unsafe=False, version="27.3"):
    entries = []
    if not missing_carrier:
        # Real bytes precede the SDK's placeholder and live outside the SDK tree.
        entries.append(("Xcode.app/SharedHeaderCarrier", 0o100644, 777, HEADER_BYTES))
    canary = b"NULLcanary" + b"\0" * (len(HEADER_BYTES) - len(b"NULLcanary"))
    entries += [
        (PREFIX + LONG_HEADER, 0o100644, 777, canary),
        (PREFIX + "SDKSettings.json", 0o100644, 1, json.dumps({"Version": version}).encode()),
        (PREFIX + "System/Library/Frameworks/Foundation.framework/Versions/A/Headers/Foundation.h", 0o100644, 2, b"synthetic foundation"),
        (PREFIX + "System/Library/Frameworks/UIKit.framework/Headers/UIKit.h", 0o100644, 3, b"synthetic uikit"),
        (PREFIX + "System/Library/Frameworks/Foundation.framework/Headers", 0o120777, 4, b"Versions/Current/Headers"),
        (PREFIX + "System/Library/Frameworks/Foundation.framework/Versions/Current", 0o120777, 5, b"A"),
        (PREFIX + "System/Library/Frameworks/AuthenticationServices.framework", 0o120777, 6, b"../../Cryptexes/OS/System/Library/Frameworks/AuthenticationServices.framework"),
    ]
    if unsafe:
        entries.append((PREFIX + "../../escape.txt", 0o100644, 999, b"must not escape"))
    raw = cpio(entries)
    chunks = [raw[i:i + 512] for i in range(0, len(raw), 512)]
    body = b"pbzx" + struct.pack(">Q", 512)
    for number, chunk in enumerate(chunks):
        data = lzma.compress(chunk) if number % 2 == 0 else chunk
        body += struct.pack(">QQ", 0, len(data)) + data
    toc = f'<xar><toc><file><name>Content</name><data><offset>0</offset><length>{len(body)}</length><encoding style="application/octet-stream"/></data></file></toc></xar>'.encode()
    compressed = zlib.compress(toc)
    header = b"xar!" + struct.pack(">HHQQI", 28, 1, len(compressed), len(toc), 0)
    path.write_bytes(header + compressed + body)


class SdkRegression(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="nxw-sdk-test-")
        self.root = Path(self.temporary.name)
        self.log = io.StringIO()

    def tearDown(self):
        if os.name == "nt" and self.root.exists():
            shutil.rmtree(ADAPTER.io_path(self.root))
        self.temporary.cleanup()

    def extract(self, **options):
        xip = self.root / "Xcode official synthetic fixture.xip"
        fixture_xip(xip, **options)
        output = self.root / ("setup-" + "x" * 60) / ("work-" + "y" * 35)
        with contextlib.redirect_stdout(self.log):
            sdk = ADAPTER.extract(UPSTREAM, xip, output)
        return sdk, output

    def test_reported_long_header_and_canary_hard_link(self):
        before = hashlib.sha256(UPSTREAM.read_bytes()).hexdigest()
        sdk, output = self.extract()
        file = sdk / LONG_HEADER
        self.assertGreater(len(str(file)), 273)
        self.assertEqual(file.read_bytes(), HEADER_BYTES)
        self.assertTrue((sdk / "System/Library/Frameworks/Foundation.framework/Headers/Foundation.h").is_file())
        self.assertEqual((sdk / "System/Library/Frameworks/AuthenticationServices.framework/Headers" / Path(LONG_HEADER).name).read_bytes(), HEADER_BYTES)
        self.assertIn("1 hard links resolved", self.log.getvalue())
        self.assertIn("SDK path I/O probe passed", self.log.getvalue())
        self.assertFalse((output / "_nxw_long_path_probe").exists())
        self.assertEqual(hashlib.sha256(UPSTREAM.read_bytes()).hexdigest(), before)

    def test_missing_hard_link_carrier_fails(self):
        with self.assertRaisesRegex(OSError, "without data carriers"):
            self.extract(missing_carrier=True)

    def test_sdk_archive_traversal_fails(self):
        with self.assertRaisesRegex(ValueError, "Unsafe SDK archive path"):
            self.extract(unsafe=True)
        self.assertFalse((self.root / "escape.txt").exists())

    def test_old_sdk_is_rejected(self):
        with self.assertRaisesRegex(OSError, "26 or later"):
            self.extract(version="25.4")

    def test_unresolved_canary_is_rejected_after_extraction(self):
        sdk, output = self.extract()
        (sdk / "extra.h").write_bytes(b"NULLcanary" + b"\0" * 90)
        spec = importlib.util.spec_from_file_location("fixture_extract", UPSTREAM)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with self.assertRaisesRegex(OSError, "Unresolved placeholder"):
            ADAPTER.validate_result(output, module, set())

    def test_windows_prefix_normalizes_relative_link_segments(self):
        value = ADAPTER.windows_long_path(r"C:\SDK\iPhoneOS.sdk\System\Library\Frameworks\..\..\Cryptexes\OS")
        self.assertEqual(value, r"\\?\C:\SDK\iPhoneOS.sdk\System\Cryptexes\OS")
        self.assertNotIn("\\..\\", value)

    def test_windows_existing_prefix_is_not_duplicated(self):
        self.assertEqual(ADAPTER.windows_long_path(r"\\?\C:\SDK\A\..\B"), r"\\?\C:\SDK\B")

    def test_windows_unc_prefix_and_slash_normalization(self):
        self.assertEqual(ADAPTER.windows_long_path(r"\\server\share\sdk\a\..\b"), r"\\?\UNC\server\share\sdk\b")
        self.assertEqual(ADAPTER.windows_long_path("C:/SDK/A/../B"), r"\\?\C:\SDK\B")

    def test_windows_device_path_is_rejected(self):
        with self.assertRaises(ValueError):
            ADAPTER.windows_long_path(r"\\.\PhysicalDrive0")

    def test_symlink_cannot_copy_outside_output(self):
        output = self.root / "output"
        output.mkdir()
        (self.root / "outside.txt").write_text("outside")
        ADAPTER.materialize(output, [("iPhoneOS.sdk/link", "../../outside.txt")])
        self.assertFalse((output / "iPhoneOS.sdk/link").exists())

    @unittest.skipUnless(os.name == "nt", "Real Win32 filesystem I/O requires Windows")
    def test_real_windows_extended_length_write_and_cleanup(self):
        output = ADAPTER.io_path(self.root / ("x" * 80))
        output.mkdir(parents=True)
        ADAPTER.probe_long_paths(output)
        self.assertFalse((output / "_nxw_long_path_probe").exists())


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--write-fixture":
        fixture_xip(Path(sys.argv[2]))
    else:
        unittest.main(verbosity=2)
