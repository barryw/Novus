#!/usr/bin/env python3
"""Compile-check every raw NDK constant against its pinned header value."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from generate_api_docs import coverage_metadata, extract


ROOT = Path(__file__).resolve().parents[1]
TYPES = {"u8": "unsigned char", "u16": "unsigned short", "u32": "unsigned long",
         "u64": "unsigned long long", "i8": "signed char", "i16": "signed short",
         "i32": "signed long", "i64": "signed long long", "usize": "unsigned long",
         "isize": "signed long", "bool": "unsigned char"}


def expression(signature: str) -> str:
    return signature.split("=", 1)[1].strip()


def constant_type(signature: str) -> str:
    match = re.search(r"\bconst\s+[A-Za-z_]\w*\s*:\s*([^=]+?)\s*=", signature)
    return match.group(1).strip() if match else ""


def c_expression(value: str) -> str:
    value = re.sub(r"\$([0-9A-Fa-f]+)", r"0x\1", value)
    value = re.sub(r"@sizeof\s*\(", "sizeof(", value)
    value = re.sub(r"\btrue\b", "1", value)
    value = re.sub(r"\bfalse\b|\bnull\b", "0", value)
    for novus, c_type in TYPES.items():
        value = re.sub(rf"\(\s*{novus}\s*\)", f"({c_type})", value)
    return value


def inventory(raw_root: Path) -> list[dict]:
    metadata, _ = coverage_metadata(raw_root)
    raw = {(item["module"], item["name"]): {
               "definition": expression(item["signature"]),
               "type": constant_type(item["signature"]),
           }
           for item in extract(raw_root, None, {}, metadata) if item["kind"] == "const"}
    manifest = json.loads((raw_root / "ndk_coverage.json").read_text())
    result = []
    for symbol in manifest["symbols"]:
        if symbol["category"] != "constant" or symbol["status"] != "DIRECTLY_SUPPORTED":
            continue
        key = (symbol["novus_module"], symbol["name"])
        value = raw.get(key)
        result.append({**symbol, "raw_definition": value["definition"] if value else None,
                       "raw_type": value["type"] if value else None})
    return result


def identifiers(value: str, names: set[str]) -> set[str]:
    return set(re.findall(r"\b[A-Za-z_]\w*\b", value)) & names


def prefixed(value: str, prefix: str, names: set[str]) -> str:
    value = c_expression(value)
    return re.sub(r"\b[A-Za-z_]\w*\b",
                  lambda match: prefix + match.group() if match.group() in names else match.group(), value)


def typed_raw(value: str, type_name: str) -> str:
    if '"' in value or "'" in value:
        return value
    suffix = "UL" if type_name.startswith("u") or type_name.startswith("*") else "L"
    return re.sub(r"(?<![A-Za-z0-9_])(?:0x[0-9A-Fa-f]+|\d+)(?![A-Za-z0-9_])",
                  lambda match: match.group() + suffix, value)


def probe_source(symbols: list[dict], all_symbols: dict[str, dict], normalize_ndk: bool = False) -> str:
    names = set(all_symbols)
    needed = {symbol["name"] for symbol in symbols}
    pending = list(needed)
    while pending:
        symbol = all_symbols[pending.pop()]
        dependencies = (identifiers(symbol["definition"], names) |
                        identifiers(symbol["raw_definition"], names)) - needed
        needed.update(dependencies)
        pending.extend(dependencies)
    lines = ["/* Generated NDK constant verifier. */",
             "typedef unsigned char UBYTE; typedef signed char BYTE;",
             "typedef unsigned short UWORD; typedef signed short WORD;",
             "typedef unsigned long ULONG; typedef signed long LONG;",
             "typedef unsigned long BOOL; typedef unsigned long BPTR;",
             "typedef void *APTR; typedef unsigned long IPTR; typedef void Object;",
             "#define MAKE_ID(a,b,c,d) ((ULONG)(a)<<24 | (ULONG)(b)<<16 | (ULONG)(c)<<8 | (ULONG)(d))"]
    for name in sorted(needed):
        symbol = all_symbols[name]
        ndk = typed_raw(symbol["definition"], symbol.get("raw_type") or "") if normalize_ndk else symbol["definition"]
        lines.append(f"#define NDK_{name} ({prefixed(ndk, 'NDK_', names)})")
        raw = typed_raw(symbol["raw_definition"], symbol.get("raw_type") or "")
        lines.append(f"#define RAW_{name} ({prefixed(raw, 'RAW_', names)})")
    for index, symbol in enumerate(symbols):
        lines.append(f"typedef char ndk_constant_{index}["
                     f"((unsigned long)(NDK_{symbol['name']}) == "
                     f"(unsigned long)(RAW_{symbol['name']})) ? 1 : -1];")
    return "\n".join(lines) + "\n"


def compile_group(symbols: list[dict], name: str, args: argparse.Namespace,
                  all_symbols: dict[str, dict]) -> list[dict]:
    source = args.output / f"{name}.c"
    output = args.output / f"{name}.o"
    source.write_text(probe_source(symbols, all_symbols))
    environment = os.environ.copy()
    environment["VBCC"] = str(args.vbcc)
    command = [str(args.vbcc / "bin/vc"), "+aos68k", "-c99", "-cpu=68020", "-c",
               "-o", str(output), str(source)]
    completed = subprocess.run(command, cwd=ROOT, env=environment, text=True,
                               errors="replace", capture_output=True)
    if completed.returncode == 0 and "warning 61" not in completed.stderr:
        return [{"name": symbol["name"], "interface": symbol["interface"],
                 "status": "passed"} for symbol in symbols]
    if len(symbols) == 1:
        normalized = args.output / f"{name}_unsigned.c"
        normalized_output = args.output / f"{name}_unsigned.o"
        normalized.write_text(probe_source(symbols, all_symbols, normalize_ndk=True))
        normalized_command = [str(args.vbcc / "bin/vc"), "+aos68k", "-c99", "-cpu=68020", "-c",
                              "-o", str(normalized_output), str(normalized)]
        normalized_result = subprocess.run(normalized_command, cwd=ROOT, env=environment, text=True,
                                           errors="replace", capture_output=True)
        if normalized_result.returncode == 0 and "warning 61" not in normalized_result.stderr:
            return [{"name": symbols[0]["name"], "interface": symbols[0]["interface"],
                     "status": "passed", "method": "unsigned_normalized",
                     "note": "authoritative C expression has signed-overflow undefined behavior; unsigned normalization matches the Novus value"}]
        return [{"name": symbols[0]["name"], "interface": symbols[0]["interface"],
                 "status": "failed", "stderr": completed.stderr}]
    midpoint = len(symbols) // 2
    return (compile_group(symbols[:midpoint], name + "_a", args, all_symbols) +
            compile_group(symbols[midpoint:], name + "_b", args, all_symbols))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-root", type=Path, default=ROOT / "Novus/std/amiga/raw")
    parser.add_argument("--ndk-path", type=Path, required=True)
    parser.add_argument("--vbcc", type=Path, default=ROOT / "vendor/vbcc")
    parser.add_argument("--output", type=Path, default=ROOT / ".novus-cache/ndk-constants")
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)

    results = []
    numeric = []
    all_symbols = {}
    for symbol in inventory(args.raw_root):
        if symbol["raw_definition"] is None:
            results.append({"name": symbol["name"], "interface": symbol["interface"],
                            "status": "failed", "error": "missing raw definition"})
        elif symbol["raw_definition"].lstrip().startswith(('"', "'")):
            status = "passed" if symbol["raw_definition"].strip() == symbol["definition"].strip() else "failed"
            results.append({"name": symbol["name"], "interface": symbol["interface"],
                            "status": status, "method": "exact_text"})
        elif symbol["name"] in all_symbols and all_symbols[symbol["name"]]["definition"] != symbol["definition"]:
            results.append({"name": symbol["name"], "interface": symbol["interface"],
                            "status": "failed", "error": "conflicting authoritative definitions"})
        else:
            all_symbols[symbol["name"]] = symbol
            numeric.append(symbol)
    for index in range(0, len(numeric), 256):
        symbols = numeric[index:index + 256]
        print(f"constants {index + 1}-{index + len(symbols)}", flush=True)
        results.extend(compile_group(symbols, f"constants_{index // 256:03}", args, all_symbols))

    report = {"schema_version": 1, "scope": "pinned classic 68k NDK constants",
              "constants_total": len(results),
              "constants_value_verified": sum(item["status"] == "passed" for item in results),
              "results": results}
    path = args.report or args.output / "report.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, indent=2) + "\n")
    print(f"NDK constant values: {report['constants_value_verified']}/{report['constants_total']}")
    return 1 if report["constants_value_verified"] != report["constants_total"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
