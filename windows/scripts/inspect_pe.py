"""Static Windows EXE inspection. This does not execute or validate the GUI."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument("exe", type=Path)
p.add_argument("--expected", choices=("x64", "arm64"), required=True)
p.add_argument("--report", type=Path, required=True)
a = p.parse_args()
b = a.exe.read_bytes()
assert b[:2] == b"MZ", "Missing DOS header"
offset = struct.unpack_from("<I", b, 0x3C)[0]
assert b[offset:offset + 4] == b"PE\0\0", "Missing PE signature"
machine = struct.unpack_from("<H", b, offset + 4)[0]
optional = offset + 24
magic = struct.unpack_from("<H", b, optional)[0]
subsystem = struct.unpack_from("<H", b, optional + 68)[0]
certificate_offset, certificate_size = struct.unpack_from("<II", b, optional + 112 + 4 * 8)
assert machine == {"x64": 0x8664, "arm64": 0xAA64}[a.expected], "Wrong executable architecture"
assert magic == 0x20B, "Expected 64-bit PE32+"
assert subsystem == 2, "Expected Windows GUI subsystem"
assert len(b) > 10 * 1024 * 1024, "Unexpectedly small self-contained artifact"
report = {
    "file": a.exe.name,
    "architecture": a.expected,
    "pe_machine": hex(machine),
    "format": "PE32+",
    "subsystem": "Windows GUI",
    "size_bytes": len(b),
    "sha256": hashlib.sha256(b).hexdigest(),
    "authenticode_certificate_present": bool(certificate_offset and certificate_size),
    "result": "PASS (static header inspection only)",
    "runtime_execution_tested": False,
}
a.report.parent.mkdir(parents=True, exist_ok=True)
a.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
