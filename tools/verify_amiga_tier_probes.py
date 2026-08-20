#!/usr/bin/env python3
"""Generate and optionally compile/link a typed reference to every tier-1/tier-2 callable.

The tier-3 equivalent is tools/verify_ndk_compile_probes.py. Raw NDK entry points take
only ABI types, so their probes need two fixed imports. The Novus-authored layers take
their own types, so each probe resolves the modules that publish the names its signature
mentions and imports exactly those.

Probes are never executed. `@zeroed` values satisfy the type checker so the compiler and
linker can prove the callable resolves, and `--measure-size` records the linked-binary
delta each callable contributes.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import time
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from generate_api_docs import extract
from verify_amiga_tiers import callable_id, tier_of

ROOT = Path(__file__).resolve().parents[1]
PRIMITIVES = {"u8", "u16", "u32", "u64", "usize", "i8", "i16", "i32", "i64", "isize",
              "f32", "f64", "bool", "unit", "Self", "fixed16", "fixed32"}
IDENTIFIER = re.compile(r"[A-Za-z_]\w*")


def split_generic_parameters(text: str) -> list[str]:
    parts, depth, start = [], 0, 0
    for index, char in enumerate(text):
        if char in "<([":
            depth += 1
        elif char in ">)]":
            depth -= 1
        elif char == "," and depth == 0:
            parts.append(text[start:index].strip())
            start = index + 1
    parts.append(text[start:].strip())
    return [part for part in parts if part]


def generic_parameters(signature: str, declaration: str) -> list[tuple[str, bool]]:
    """Return (name, is_const) for the generic parameters declared right after `declaration`."""
    match = re.search(re.escape(declaration) + r"\s*<", signature)
    if not match:
        return []
    depth, start = 1, match.end()
    for index in range(match.end(), len(signature)):
        if signature[index] == "<":
            depth += 1
        elif signature[index] == ">":
            depth -= 1
            if depth == 0:
                parameters = []
                for part in split_generic_parameters(signature[start:index]):
                    is_const = part.startswith("const ")
                    name = part.removeprefix("const ").split(":")[0].strip()
                    if name:
                        parameters.append((name, is_const))
                return parameters
    return []


def type_owners(std_root: Path, symbols: list[dict]) -> dict[str, str]:
    """Map an exported type name to the module that publishes it."""
    owners: dict[str, str] = {}
    for symbol in symbols:
        if symbol["kind"] in {"struct", "enum", "union", "type", "trait", "class"}:
            owners.setdefault(symbol["name"], symbol["module"])
    return owners


def argument(type_name: str) -> str:
    stripped = type_name.strip()
    if stripped == "bool":
        return "false"
    if stripped in PRIMITIVES:
        return f"({stripped})0"
    if stripped.startswith("*"):
        return f"({stripped})null"
    return f"@zeroed({stripped})"


def receiver_expression(modifiers: str, variable: str) -> str:
    if "&var" in modifiers:
        return f"&var {variable}"
    if "&" in modifiers:
        return f"&{variable}"
    return variable


def substitute(type_name: str, bindings: dict[str, str]) -> str:
    if not bindings:
        return type_name
    return re.sub(r"\b\w+\b", lambda match: bindings.get(match.group(0), match.group(0)), type_name)


def probe_body(item: dict, index: int) -> list[str]:
    parameters = [parameter for parameter in item["symbol"]["parameters"]
                  if parameter["modifiers"] != "..."]
    receiver = next((parameter for parameter in parameters if parameter["receiver"]), None)
    owner, name = item["owner"], item["name"]

    # Generic callables need concrete arguments; the probe only has to type-check and link,
    # so bind every type parameter to u32 and every const parameter to a small extent.
    declared = item["owner_generics"] + generic_parameters(item["symbol"]["signature"], f"fn {name}")
    bindings = {parameter: ("4" if is_const else "u32") for parameter, is_const in declared}
    owner_type = owner_path = owner
    if owner and item["owner_generics"]:
        arguments_text = ", ".join(bindings[p] for p, _ in item["owner_generics"])
        owner_type = f"{owner}<{arguments_text}>"
        # Type position takes `Owner<u32>`; expression position takes the turbofish form.
        owner_path = f"{owner}::<{arguments_text}>"

    arguments = [argument(substitute(parameter["type"], bindings)) for parameter in parameters
                 if not parameter["receiver"]]
    # A call with no result cannot bind a discard, so only capture one when there is a value.
    returns = ((item["symbol"].get("returns") or {}).get("type") or "").strip()
    binding = "let _ = " if returns and returns != "unit" else ""

    if receiver is not None and owner:
        # Method syntax rather than UFCS: the compiler rejects `Owner::<T>::method(&value)`
        # for generic owners, and method syntax applies the receiver's borrow form itself.
        variable = f"receiver{index}"
        return [
            f"        var {variable} = @zeroed({owner_type})",
            f"        {binding}{variable}.{name}({', '.join(arguments)})",
        ]
    path = f"{owner_path}::{name}" if owner else name
    return [f"        {binding}{path}({', '.join(arguments)})"]


def probe_imports(items: list[dict], owners: dict[str, str], module: str) -> list[str]:
    needed: dict[str, set[str]] = defaultdict(set)
    for item in items:
        names = {item["owner"]} if item["owner"] else set()
        if not item["owner"]:
            needed[item["symbol"]["module"]].add(item["name"])
        for parameter in item["symbol"]["parameters"]:
            names.update(IDENTIFIER.findall(parameter["type"]))
        returns = item["symbol"].get("returns") or {}
        names.update(IDENTIFIER.findall(returns.get("type") or ""))
        for candidate in names:
            if not candidate or candidate in PRIMITIVES:
                continue
            source_module = owners.get(candidate)
            if source_module and source_module != module:
                needed[source_module].add(candidate)
            elif source_module == module:
                needed[module].add(candidate)
    # `extract` reports std modules relative to Novus/std, so restore the `std::` root.
    return [f"from {source if source.startswith('amiga::') else 'std::' + source} "
            f"import {', '.join(sorted(names))}"
            for source, names in sorted(needed.items()) if names]


def probe_source(module: str, items: list[dict], owners: dict[str, str]) -> str:
    body: list[str] = []
    for index, item in enumerate(items):
        body.extend(probe_body(item, index))
    return "\n".join([
        "// Generated compile/link probe. Never execute this binary.",
        *probe_imports(items, owners, module),
        "",
        "fn probe_all() {",
        "    unsafe {",
        *body,
        "    }",
        "}",
        "",
        "pub fn main() -> i32 {",
        "    probe_all()",
        "    return 0",
        "}",
        "",
    ])


def safe_name(module: str) -> str:
    return module.replace("::", "_")


def compile_source(source: Path, binary: Path, args: argparse.Namespace) -> dict:
    command = ["dotnet", str(args.compiler), "compile", str(source), "-o", str(binary),
               "--release", "--no-cache"]
    started = time.monotonic_ns()
    completed = subprocess.run(command, cwd=ROOT, text=True, errors="replace", capture_output=True)
    result = {"return_code": completed.returncode, "stdout": completed.stdout,
              "stderr": completed.stderr,
              "build_us": (time.monotonic_ns() - started) // 1_000}
    if completed.returncode == 0:
        result["binary_bytes"] = binary.stat().st_size
    return result


def failure_phase(attempt: dict) -> str:
    return "link" if "undefined symbol" in attempt["stderr"] else "compile"


def compile_group(module: str, items: list[dict], owners: dict[str, str], name: str,
                  args: argparse.Namespace) -> tuple[list[dict], dict]:
    source = args.output / f"{name}.novus"
    source.write_text(probe_source(module, items, owners))
    attempt = compile_source(source, args.output / name, args)
    if attempt["return_code"] == 0:
        return ([{"name": item["id"], "status": "passed"} for item in items], attempt)
    if len(items) == 1:
        return ([{"name": items[0]["id"], "status": "failed", "phase": failure_phase(attempt),
                  "stderr": attempt["stderr"][-4000:]}], attempt)
    midpoint = len(items) // 2
    left, _ = compile_group(module, items[:midpoint], owners, name + "_a", args)
    right, _ = compile_group(module, items[midpoint:], owners, name + "_b", args)
    return left + right, attempt


def compile_measured(module: str, items: list[dict], owners: dict[str, str], name: str,
                     args: argparse.Namespace) -> tuple[list[dict], dict]:
    started = time.monotonic_ns()
    baseline_source = args.output / f"{name}_baseline.novus"
    baseline_source.write_text(probe_source(module, [], owners))
    baseline = compile_source(baseline_source, args.output / f"{name}_baseline", args)
    if baseline["return_code"] != 0:
        return ([{"name": item["id"], "status": "failed", "phase": "baseline",
                  "stderr": baseline["stderr"][-4000:]} for item in items], baseline)
    results = []
    for index, item in enumerate(items):
        probe_name = f"{name}_{index}"
        source = args.output / f"{probe_name}.novus"
        source.write_text(probe_source(module, [item], owners))
        attempt = compile_source(source, args.output / probe_name, args)
        if attempt["return_code"] == 0:
            results.append({"name": item["id"], "status": "passed",
                            "baseline_bytes": baseline["binary_bytes"],
                            "binary_bytes": attempt["binary_bytes"],
                            "bytes_delta": attempt["binary_bytes"] - baseline["binary_bytes"],
                            "build_us": attempt["build_us"]})
        else:
            results.append({"name": item["id"], "status": "failed",
                            "phase": failure_phase(attempt),
                            "stderr": attempt["stderr"][-4000:]})
    baseline["build_us"] = (time.monotonic_ns() - started) // 1_000
    return results, baseline


def inventory(std_root: Path, tiers: set[str]) -> tuple[dict[str, list[dict]], dict[str, str]]:
    symbols = list(extract(std_root, None, {}))
    owners = type_owners(std_root, symbols)
    declared_generics = {
        symbol["name"]: generic_parameters(symbol["signature"], symbol["name"])
        for symbol in symbols
        if symbol["kind"] in {"struct", "enum", "union", "class"}
    }
    by_module: dict[str, list[dict]] = defaultdict(list)
    for symbol in symbols:
        if symbol["kind"] != "fn":
            continue
        tier = tier_of(symbol["source"])
        if tier is None or tier not in tiers:
            continue
        identifier = callable_id(symbol["module"], symbol["owner"], symbol["name"],
                                 symbol["signature"])
        by_module[symbol["module"]].append({
            "id": identifier, "tier": tier, "owner": symbol["owner"],
            "name": symbol["name"], "symbol": symbol,
            "owner_generics": declared_generics.get(symbol["owner"], []),
        })
    return ({module: sorted(items, key=lambda item: item["id"])
             for module, items in sorted(by_module.items())}, owners)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--std-root", type=Path, default=ROOT / "Novus/std")
    parser.add_argument("--output", type=Path, default=ROOT / ".novus-cache/amiga-tier-probes")
    parser.add_argument("--compiler", type=Path, default=ROOT / "Novus/bin/Debug/net10.0/Novus.dll")
    parser.add_argument("--tier", action="append", choices=("tier1", "tier2"),
                        help="limit generation to a tier; defaults to both")
    parser.add_argument("--module", action="append", help="limit generation to an exact module")
    parser.add_argument("--compile", action="store_true", help="compile and link every probe")
    parser.add_argument("--measure-size", action="store_true",
                        help="compile each callable separately and record its binary-size delta")
    parser.add_argument("--shard-index", type=int, help="zero-based module shard")
    parser.add_argument("--shard-count", type=int, help="number of module shards")
    parser.add_argument("--report", type=Path, help="write machine-readable results")
    args = parser.parse_args()
    if args.measure_size:
        args.compile = True

    modules, owners = inventory(args.std_root, set(args.tier or ("tier1", "tier2")))
    if args.module:
        selected = set(args.module)
        unknown = selected - set(modules)
        if unknown:
            parser.error("unknown module(s): " + ", ".join(sorted(unknown)))
        modules = {module: items for module, items in modules.items() if module in selected}
    if args.shard_index is not None or args.shard_count is not None:
        if args.shard_index is None or args.shard_count is None or not 0 <= args.shard_index < args.shard_count:
            parser.error("--shard-index and --shard-count must define a valid zero-based shard")
        modules = {module: items for index, (module, items) in enumerate(modules.items())
                   if index % args.shard_count == args.shard_index}

    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    passed = failed = 0
    for module, items in modules.items():
        name = safe_name(module)
        print(f"proving {module} ({len(items)} callable(s))", flush=True)
        source = args.output / f"{name}.novus"
        source.write_text(probe_source(module, items, owners))
        record = {"module": module, "callables": len(items), "source": str(source),
                  "status": "generated"}
        if args.compile:
            started = time.monotonic_ns()
            function_results, _ = (compile_measured(module, items, owners, name, args)
                                   if args.measure_size
                                   else compile_group(module, items, owners, name, args))
            failures = [item for item in function_results if item["status"] == "failed"]
            record.update(status="failed" if failures else "passed",
                          function_results=function_results,
                          milliseconds=(time.monotonic_ns() - started) // 1_000_000)
            passed += len(function_results) - len(failures)
            failed += len(failures)
            print(f"{module}: {len(function_results) - len(failures)}/{len(function_results)} "
                  f"passed in {record['milliseconds']}ms", flush=True)
        results.append(record)

    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps({"schema_version": 1, "results": results}, indent=2) + "\n")
    if args.compile:
        print(f"Amiga tier callable compile/link coverage: {passed}/{passed + failed}")
        return 0 if failed == 0 else 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
