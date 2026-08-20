#!/usr/bin/env python3
"""Generate and optionally compile/link a typed reference to every raw NDK call."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from generate_api_docs import coverage_metadata, extract


ROOT = Path(__file__).resolve().parents[1]
PRIMITIVES = {"u8", "u16", "u32", "u64", "usize", "i8", "i16", "i32", "i64", "isize", "f32", "f64"}


def argument(type_name: str) -> str:
    if type_name == "bool":
        return "false"
    if type_name in PRIMITIVES:
        return f"({type_name})0"
    if type_name.startswith("*"):
        return f"({type_name})null"
    if "fn(" in type_name:
        return f"@zeroed({type_name})"
    return f"@zeroed({type_name})"


def inventory(raw_root: Path) -> dict[str, list[dict]]:
    metadata, _ = coverage_metadata(raw_root)
    api = {(symbol["module"], symbol["name"]): symbol
           for symbol in extract(raw_root, None, {}, metadata)
           if symbol["kind"] == "fn"}
    manifest = json.loads((raw_root / "ndk_coverage.json").read_text())
    by_module: dict[str, list[dict]] = defaultdict(list)
    missing = []
    for entry in manifest["symbols"]:
        if entry["category"] != "function" or entry["status"] != "DIRECTLY_SUPPORTED":
            continue
        key = (entry["novus_module"], entry["name"])
        symbol = api.get(key)
        if symbol is None:
            missing.append("::".join(key))
            continue
        by_module[key[0]].append(symbol)
    if missing:
        raise ValueError("manifest functions missing from API inventory: " + ", ".join(sorted(missing)))
    return {module: sorted(symbols, key=lambda value: value["name"])
            for module, symbols in sorted(by_module.items())}


def probe_source(module: str, functions: list[dict]) -> str:
    calls = []
    for function in functions:
        parameters = [parameter for parameter in function["parameters"]
                      if parameter["modifiers"] != "..."]
        arguments = ", ".join(argument(parameter["type"]) for parameter in parameters)
        calls.append(f"        let _ = {function['name']}({arguments})")
    return "\n".join([
        "// Generated compile/link probe. Never execute this binary.",
        "from amiga::raw::structs import *",
        "from amiga::raw::types import *",
        f"from {module} import *",
        "",
        "fn probe_all() {",
        "    unsafe {",
        *calls,
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
    return module.removeprefix("amiga::raw::").replace("::", "_")


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


def compile_group(module: str, functions: list[dict], name: str, args: argparse.Namespace) -> tuple[list[dict], dict]:
    print(f"  {module}: probing {len(functions)} function(s)", flush=True)
    source = args.output / f"{name}.novus"
    source.write_text(probe_source(module, functions))
    binary = args.output / name
    try:
        attempt = compile_source(source, binary, args)
    except Exception as error:
        print(f"  compiler invocation failed: {error!r}", flush=True)
        raise
    if attempt["return_code"] == 0:
        return ([{"name": function["name"], "status": "passed"} for function in functions], attempt)
    if len(functions) == 1:
        phase = "link" if "undefined symbol" in attempt["stderr"] else "compile"
        return ([{"name": functions[0]["name"], "status": "failed", "phase": phase,
                  "stderr": attempt["stderr"]}], attempt)

    midpoint = len(functions) // 2
    left, _ = compile_group(module, functions[:midpoint], name + "_a", args)
    right, _ = compile_group(module, functions[midpoint:], name + "_b", args)
    return left + right, attempt


def compile_measured(module: str, functions: list[dict], name: str,
                     args: argparse.Namespace) -> tuple[list[dict], dict]:
    started = time.monotonic_ns()
    baseline_source = args.output / f"{name}_baseline.novus"
    baseline_binary = args.output / f"{name}_baseline"
    baseline_source.write_text(probe_source(module, []))
    baseline = compile_source(baseline_source, baseline_binary, args)
    if baseline["return_code"] != 0:
        return ([{"name": function["name"], "status": "failed", "phase": "baseline",
                  "stderr": baseline["stderr"]} for function in functions], baseline)
    results = []
    for function in functions:
        function_name = f"{name}_{function['name']}"
        source = args.output / f"{function_name}.novus"
        binary = args.output / function_name
        source.write_text(probe_source(module, [function]))
        attempt = compile_source(source, binary, args)
        if attempt["return_code"] == 0:
            results.append({"name": function["name"], "status": "passed",
                            "baseline_bytes": baseline["binary_bytes"],
                            "binary_bytes": attempt["binary_bytes"],
                            "bytes_delta": attempt["binary_bytes"] - baseline["binary_bytes"],
                            "build_us": attempt["build_us"]})
        else:
            phase = "link" if "undefined symbol" in attempt["stderr"] else "compile"
            results.append({"name": function["name"], "status": "failed", "phase": phase,
                            "stderr": attempt["stderr"]})
    attempt = baseline
    attempt["build_us"] = (time.monotonic_ns() - started) // 1_000
    return results, baseline


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-root", type=Path, default=ROOT / "Novus/std/amiga/raw")
    parser.add_argument("--output", type=Path, default=ROOT / ".novus-cache/ndk-compile-probes")
    parser.add_argument("--compiler", type=Path, default=ROOT / "Novus/bin/Debug/net10.0/Novus.dll")
    parser.add_argument("--compile", action="store_true", help="compile and link every generated probe")
    parser.add_argument("--measure-size", action="store_true",
                        help="compile each function separately and record its binary-size delta")
    parser.add_argument("--module", action="append", help="limit generation to an exact Novus module")
    parser.add_argument("--function", action="append", help="limit generation to exact function names")
    parser.add_argument("--exclude-module", action="append", help="exclude an exact Novus module")
    parser.add_argument("--shard-index", type=int, help="zero-based module shard")
    parser.add_argument("--shard-count", type=int, help="number of module shards")
    parser.add_argument("--report", type=Path, help="write machine-readable results")
    args = parser.parse_args()
    if args.measure_size:
        args.compile = True

    modules = inventory(args.raw_root)
    if args.module:
        selected = set(args.module)
        unknown = selected - set(modules)
        if unknown:
            parser.error("unknown module(s): " + ", ".join(sorted(unknown)))
        modules = {module: functions for module, functions in modules.items() if module in selected}
    if args.exclude_module:
        modules = {module: functions for module, functions in modules.items() if module not in set(args.exclude_module)}
    if args.function:
        selected = set(args.function)
        found = {function["name"] for functions in modules.values() for function in functions}
        unknown = selected - found
        if unknown:
            parser.error("unknown function(s): " + ", ".join(sorted(unknown)))
        modules = {module: [function for function in functions if function["name"] in selected]
                   for module, functions in modules.items()}
        modules = {module: functions for module, functions in modules.items() if functions}
    if args.shard_index is not None or args.shard_count is not None:
        if args.shard_index is None or args.shard_count is None or not 0 <= args.shard_index < args.shard_count:
            parser.error("--shard-index and --shard-count must define a valid zero-based shard")
        modules = {module: functions for index, (module, functions) in enumerate(modules.items())
                   if index % args.shard_count == args.shard_index}

    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    for module, functions in modules.items():
        name = safe_name(module)
        source = args.output / f"{name}.novus"
        start = time.monotonic_ns()
        print(f"proving {module} ({len(functions)} function(s))", flush=True)
        source.write_text(probe_source(module, functions))
        result = {"module": module, "functions": len(functions), "source": str(source), "status": "generated"}
        if args.compile:
            function_results, attempt = (compile_measured(module, functions, name, args)
                                         if args.measure_size else compile_group(module, functions, name, args))
            failures = [item for item in function_results if item["status"] == "failed"]
            result.update({"status": "passed" if not failures else "failed",
                           "functions_verified": len(function_results) - len(failures),
                           "function_results": function_results, **attempt})
            elapsed = (time.monotonic_ns() - start) // 1_000_000
            print(f"{module}: {result['functions_verified']}/{len(functions)} passed in {elapsed}ms")
        results.append(result)

    report = {
        "schema_version": 1,
        "scope": "pinned classic 68k NDK raw callable surface",
        "modules_total": len(results),
        "functions_total": sum(result["functions"] for result in results),
        "functions_compile_link_verified": sum(result.get("functions_verified", 0) for result in results),
        "functions_size_measured": sum(
            item.get("bytes_delta") is not None for result in results
            for item in result.get("function_results", [])),
        "results": results,
    }
    report_path = args.report or args.output / "report.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    print(f"NDK callable compile/link coverage: {report['functions_compile_link_verified']}/{report['functions_total']}")
    return 1 if any(result["status"] == "failed" for result in results) else 0


if __name__ == "__main__":
    raise SystemExit(main())
