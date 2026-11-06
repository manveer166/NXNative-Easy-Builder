"""Check the Apple Silicon binary, ad-hoc seals and bundled managed payload."""

import hashlib
import plistlib
import struct
import sys
import zlib
from pathlib import Path

ARM64 = 0x0100000C
DOTNET_BUNDLE_MARKER = bytes.fromhex(
    "8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae"
)


def slices(binary):
    if binary[:4] == b"\xca\xfe\xba\xbe":
        count, = struct.unpack_from(">I", binary, 4)
        for index in range(count):
            _, _, offset, size, _ = struct.unpack_from(">5I", binary, 8 + 20 * index)
            yield binary[offset:offset + size]
    else:
        yield binary


def verify_macho(data, special):
    magic, arch, _, _, count = struct.unpack_from("<5I", data)
    assert magic == 0xFEEDFACF, "expected a Mach-O 64-bit slice"
    cursor = 32
    signature = None
    for _ in range(count):
        command, size = struct.unpack_from("<2I", data, cursor)
        assert size >= 8
        if command == 0x1D:
            offset, length = struct.unpack_from("<2I", data, cursor + 8)
            signature = data[offset:offset + length]
        cursor += size
    assert signature, "missing LC_CODE_SIGNATURE"
    magic, _, count = struct.unpack_from(">3I", signature)
    assert magic == 0xFADE0CC0
    directory = None
    for index in range(count):
        slot, offset = struct.unpack_from(">2I", signature, 12 + 8 * index)
        if slot == 0:
            magic, length = struct.unpack_from(">2I", signature, offset)
            assert magic == 0xFADE0C02
            directory = signature[offset:offset + length]
            break
    assert directory, "missing CodeDirectory"
    _, _, _, flags, hash_offset, _, specials, pages, limit = struct.unpack_from(">9I", directory)
    hash_size, hash_type, _, page_shift = struct.unpack_from("4B", directory, 36)
    assert flags & 2, "expected an ad-hoc signature"
    algorithm = {1: "sha1", 2: "sha256", 3: "sha256", 4: "sha384"}[hash_type]

    def digest(content):
        return hashlib.new(algorithm, content).digest()[:hash_size]

    page_size = 1 << page_shift
    assert pages == (limit + page_size - 1) // page_size
    for index in range(pages):
        page = data[index * page_size:min((index + 1) * page_size, limit)]
        start = hash_offset + index * hash_size
        assert directory[start:start + hash_size] == digest(page), f"code page {index} does not match"
    for slot, content in special.items():
        assert slot <= specials
        start = hash_offset - slot * hash_size
        assert directory[start:start + hash_size] == digest(content), f"special slot {slot} does not match"
    return arch


def dotnet_payload(binary):
    marker = binary.index(DOTNET_BUNDLE_MARKER)
    cursor, = struct.unpack_from("<Q", binary, marker - 8)
    major, _, entries = struct.unpack_from("<3I", binary, cursor)
    assert major == 6 and 0 < entries < 2000
    cursor += 12

    def string():
        nonlocal cursor
        length = 0
        for shift in range(0, 35, 7):
            byte = binary[cursor]
            cursor += 1
            length |= (byte & 127) << shift
            if byte < 128:
                break
        value = binary[cursor:cursor + length].decode("utf-8")
        cursor += length
        return value

    string()  # Bundle ID.
    cursor += 40  # deps and runtime config locations, then bundle flags.
    expected = {"NXNativeEasyBuilder.dll", "Avalonia.Native.dll", "NXNativeEasyBuilder.runtimeconfig.json"}
    for _ in range(entries):
        offset, size, compressed, _ = struct.unpack_from("<qqqB", binary, cursor)
        cursor += 25
        name = string()
        assert 0 < offset < len(binary) and 0 <= size and 0 <= compressed
        end = offset + (compressed or size)
        assert end <= len(binary)
        if name in expected:
            content = binary[offset:end]
            if compressed:
                content = zlib.decompress(content, wbits=-15)
            assert len(content) == size, f"wrong bundle member length for {name}"
            expected.remove(name)
    assert not expected, f"missing bundle members {expected}"
    return entries


def main(app):
    contents = app / "Contents"
    info_bytes = (contents / "Info.plist").read_bytes()
    info = plistlib.loads(info_bytes)
    assert info["CFBundlePackageType"] == "APPL"
    exe = contents / "MacOS" / info["CFBundleExecutable"]
    assert exe.stat().st_mode & 0o100, "missing executable permission"
    resources = (contents / "_CodeSignature" / "CodeResources").read_bytes()
    manifest = plistlib.loads(resources)["files2"]
    binary = exe.read_bytes()
    assert [verify_macho(data, {1: info_bytes, 3: resources}) for data in slices(binary)] == [ARM64]
    libraries = list(exe.parent.glob("*.dylib"))
    assert len(libraries) == 3
    for library in libraries:
        assert ARM64 in [verify_macho(data, {}) for data in slices(library.read_bytes())]
        assert "MacOS/" + library.name in manifest
    for resource in (contents / "Resources").iterdir():
        assert manifest["Resources/" + resource.name]["hash2"] == hashlib.sha256(resource.read_bytes()).digest()
    print(f"PASS ARM64 Mach-O, ad-hoc code hashes, resource seal, {len(libraries)} native libraries, "
          f"and {dotnet_payload(binary)} .NET bundle entries")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
