"""Windows path adapter for NXNativeWindows' upstream Xcode XIP extractor.

The upstream file is loaded without modifying the managed repository. Apple
downloads and authorization are supplied by the installer; no Apple data is bundled.
Usage: sdk_extract_windows.py <upstream extract_xip.py> <Xcode.xip> <output>
"""
import importlib.util
import json
import ntpath
import os
import shutil
import sys
import threading
import time
from pathlib import Path


def windows_long_path(value):
    """Normalize dot segments BEFORE adding the Win32 extended-length prefix."""
    text = os.fspath(value).replace("/", "\\")
    if text.startswith("\\\\.\\"):
        raise ValueError("Device paths are not supported for SDK extraction")
    if text.lower().startswith("\\\\?\\unc\\"):
        text = "\\\\" + text[8:]
    elif text.startswith("\\\\?\\"):
        text = text[4:]
    text = ntpath.normpath(ntpath.abspath(text))
    if text.startswith("\\\\"):
        return "\\\\?\\UNC\\" + text[2:]
    if not ntpath.isabs(text) or not ntpath.splitdrive(text)[0]:
        raise ValueError("An absolute Windows file path is required")
    return "\\\\?\\" + text


def io_path(value):
    return Path(windows_long_path(value) if os.name == "nt" else os.path.abspath(value))


def inside(path, root):
    return os.path.normcase(os.path.commonpath([str(path), str(root)])) == os.path.normcase(str(root))


def sdk_path(root, relative):
    root = io_path(root)
    parts = relative.replace("\\", "/").split("/")
    if relative.startswith(("/", "\\")) or ":" in relative or any(p in (".", "..") for p in parts):
        raise ValueError("Unsafe SDK archive path: " + relative)
    result = io_path(root / relative)
    if not inside(result, root):
        raise ValueError("SDK archive path escapes output")
    return result


def materialize(root, links):
    # Preserve upstream's copy-based symlink behavior. Normalize ../ targets so
    # extended Win32 paths never contain unresolved dot segments.
    root = io_path(root)
    pending = [(rel, target) for rel, target in links if "/" in rel.rstrip("/")]
    while pending:
        left = []
        for relative, target in pending:
            dest = sdk_path(root, relative)
            if target.startswith(("/", "\\")) or ":" in target:
                print("  external SDK link skipped: " + relative, flush=True)
                continue
            source = io_path(dest.parent / target)
            if not inside(source, root):
                print("  SDK link outside output skipped: " + relative, flush=True)
                continue
            if dest.exists():
                continue
            if not source.exists():
                left.append((relative, target))
                continue
            dest.parent.mkdir(parents=True, exist_ok=True)
            if source.is_dir():
                if inside(dest, source):
                    raise ValueError("Recursive SDK directory link: " + relative)
                shutil.copytree(source, dest)
            else:
                shutil.copyfile(source, dest)
        if len(left) == len(pending):
            for relative, _ in left:
                print("  link target not in SDK, skipped: " + relative, flush=True)
            break
        pending = left


def probe_long_paths(root):
    root = io_path(root)
    probe = root / "_nxw_long_path_probe"
    target = probe / ("a" * 90) / ("b" * 90) / "probe.txt"
    try:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b"NXW long path probe")
        if target.read_bytes() != b"NXW long path probe":
            raise OSError("Long-path write/read probe failed")
    finally:
        if probe.exists():
            shutil.rmtree(probe)
    print("SDK path I/O probe passed" + (" (Win32 extended-length paths)" if os.name == "nt" else " (non-Windows test host)"), flush=True)


def validate_result(root, upstream, expected_links):
    root = io_path(root)
    for relative in expected_links:
        if not sdk_path(root, relative).is_file():
            raise OSError("Unresolved SDK hard link: " + relative)
    sdk = next((p for p in root.iterdir() if p.name == "iPhoneOS.sdk" and p.is_dir()), None)
    if sdk is None:
        sdk = next((p for p in root.iterdir() if p.name.startswith("iPhoneOS") and p.suffix == ".sdk" and p.is_dir()), None)
    if sdk is None:
        raise OSError("No iPhoneOS SDK was extracted")
    for name in ("Foundation", "UIKit"):
        if not (sdk / "System" / "Library" / "Frameworks" / (name + ".framework") / "Headers" / (name + ".h")).is_file():
            raise OSError("SDK missing " + name + " headers")
    metadata = json.loads((sdk / "SDKSettings.json").read_text(encoding="utf-8"))
    if int(str(metadata["Version"]).split(".")[0]) < 26:
        raise OSError("iOS SDK 26 or later is required")
    files = 0
    for directory, _, names in os.walk(sdk):
        for name in names:
            item = io_path(Path(directory) / name)
            size = item.stat().st_size
            with item.open("rb") as stream:
                if upstream.is_canary(stream.read(64), size):
                    raise OSError("Unresolved placeholder in SDK: " + str(item))
            files += 1
    print(f"SDK validation passed: {files} files; {len(expected_links)} hard links resolved", flush=True)
    return sdk


def extract(script, xip, output):
    root = io_path(output)
    root.mkdir(parents=True, exist_ok=True)
    probe_long_paths(root)
    spec = importlib.util.spec_from_file_location("nxw_upstream_extract_xip", io_path(script))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    required = ("main", "entries", "scan", "is_canary", "WANT")
    if not all(hasattr(module, name) for name in required):
        raise RuntimeError("Upstream XIP extractor API changed; update Easy Installer")
    expected = set()
    original_entries, original_scan = module.entries, module.scan
    def checked_entries(reader):
        for entry in original_entries(reader):
            name = entry[0]
            if module.WANT in name:
                sdk_path(root, name.split(module.WANT, 1)[1])
            yield entry
    def checked_scan(*args):
        need, have = original_scan(*args)
        missing = [p for key, paths in need.items() if key not in have for p in paths]
        if missing:
            raise OSError("XIP has hard links without data carriers: " + missing[0])
        expected.update(p for paths in need.values() for p in paths)
        return need, have
    module.Path, module.materialize = io_path, materialize
    module.entries, module.scan = checked_entries, checked_scan
    previous_argv = sys.argv
    stop, started = threading.Event(), time.monotonic()
    def heartbeat():
        while not stop.wait(10):
            print(f"SDK extraction running: {time.monotonic() - started:.0f}s elapsed", flush=True)
    thread = threading.Thread(target=heartbeat, daemon=True)
    thread.start()
    try:
        sys.argv = [str(script), str(xip), str(output)]
        module.main()
        return validate_result(root, module, expected)
    finally:
        sys.argv = previous_argv
        stop.set()
        thread.join()


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit("Usage: sdk_extract_windows.py <upstream extract_xip.py> <Xcode.xip> <output>")
    extract(*sys.argv[1:])
