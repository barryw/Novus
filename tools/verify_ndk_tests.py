#!/usr/bin/env python3
"""Measure per-callable NDK documentation and test evidence."""

from __future__ import annotations

import argparse
import ast
import json
import operator
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from generate_api_docs import coverage_metadata, extract


ROOT = Path(__file__).resolve().parents[1]
COVER = re.compile(r"^\s*//\s*@covers-ndk\s+([^|\s]+)\|([^|]+)\|([^\s]+)\s*$")
SIDE_EFFECT = re.compile(r"^\s*//\s*@ndk-side-effects?\s+(.+?)\s*$")
TEST = re.compile(r'^\s*@test\("([^"]+)"\)')
TEST_FN = re.compile(r"^\s*pub\s+fn\s+([A-Za-z_]\w*)\s*\(")
ANSI = re.compile(r"(?:\x1b\[[0-9;]*[A-Za-z]|\x9b[0-9;]*[A-Za-z])")
ASSERTION = re.compile(r"\b(expect|expect_true|expect_false|expect_eq|expect_ne)\s*\(")
UNKNOWN = object()


def _assertion_arguments(text: str, opening: int) -> list[str] | None:
    args, start, depth, quote, escaped = [], opening + 1, 1, None, False
    for index in range(opening + 1, len(text)):
        char = text[index]
        if quote:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                quote = None
            continue
        if char in "\"'":
            quote = char
        elif char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
            if depth == 0:
                args.append(text[start:index].strip())
                return args
        elif char == "," and depth == 1:
            args.append(text[start:index].strip())
            start = index + 1
    return None


def _constant_value(expression: str):
    expression = re.sub(r"\$([0-9A-Fa-f]+)", r"0x\1", expression)
    expression = re.sub(r"\btrue\b", "True", expression, flags=re.IGNORECASE)
    expression = re.sub(r"\bfalse\b", "False", expression, flags=re.IGNORECASE)
    expression = expression.replace("&&", " and ").replace("||", " or ")
    expression = re.sub(r"!(?!=)", " not ", expression)
    try:
        node = ast.parse(expression.strip(), mode="eval").body
    except (SyntaxError, ValueError):
        return UNKNOWN

    binary = {
        ast.Add: operator.add, ast.Sub: operator.sub, ast.Mult: operator.mul,
        ast.Div: operator.floordiv, ast.FloorDiv: operator.floordiv,
        ast.Mod: operator.mod, ast.LShift: operator.lshift, ast.RShift: operator.rshift,
        ast.BitAnd: operator.and_, ast.BitOr: operator.or_, ast.BitXor: operator.xor,
    }
    compare = {
        ast.Eq: operator.eq, ast.NotEq: operator.ne, ast.Lt: operator.lt,
        ast.LtE: operator.le, ast.Gt: operator.gt, ast.GtE: operator.ge,
    }

    def evaluate(item):
        if isinstance(item, ast.Constant) and isinstance(item.value, (bool, int)):
            return item.value
        if isinstance(item, ast.UnaryOp) and isinstance(item.op, (ast.Not, ast.USub, ast.UAdd, ast.Invert)):
            value = evaluate(item.operand)
            if value is UNKNOWN:
                return UNKNOWN
            return {ast.Not: operator.not_, ast.USub: operator.neg,
                    ast.UAdd: operator.pos, ast.Invert: operator.invert}[type(item.op)](value)
        if isinstance(item, ast.BinOp) and type(item.op) in binary:
            left, right = evaluate(item.left), evaluate(item.right)
            if UNKNOWN in (left, right):
                return UNKNOWN
            try:
                return binary[type(item.op)](left, right)
            except (ArithmeticError, TypeError):
                return UNKNOWN
        if isinstance(item, ast.BoolOp) and isinstance(item.op, (ast.And, ast.Or)):
            values = [evaluate(value) for value in item.values]
            if any(value is UNKNOWN for value in values):
                return UNKNOWN
            return all(values) if isinstance(item.op, ast.And) else any(values)
        if isinstance(item, ast.Compare) and len(item.ops) == len(item.comparators) == 1:
            left, right = evaluate(item.left), evaluate(item.comparators[0])
            if UNKNOWN in (left, right) or type(item.ops[0]) not in compare:
                return UNKNOWN
            return compare[type(item.ops[0])](left, right)
        return UNKNOWN

    return evaluate(node)


def tautological_assertions(path: Path, text: str) -> list[str]:
    errors = []
    for match in ASSERTION.finditer(text):
        args = _assertion_arguments(text, match.end() - 1)
        if not args:
            continue
        name, first = match.group(1), args[0]
        value = _constant_value(first)
        normalized = lambda item: re.sub(r"\s+", "", item)
        always_passes = ((name in ("expect", "expect_true") and value is True)
                         or (name == "expect_false" and value is False))
        if name in ("expect_eq", "expect_ne") and len(args) > 1:
            second = args[1]
            second_value = _constant_value(second)
            identical = normalized(first) == normalized(second)
            always_passes = ((name == "expect_eq" and (identical or
                              value is not UNKNOWN and second_value is not UNKNOWN and value == second_value))
                             or (name == "expect_ne" and value is not UNKNOWN and
                                 second_value is not UNKNOWN and value != second_value))
        if always_passes:
            line = text.count("\n", 0, match.start()) + 1
            errors.append(f"{path}:{line}: unconditional passing assertion cannot provide NDK evidence")
    return errors


def callable_inventory(raw_root: Path) -> dict[tuple[str, str], dict]:
    metadata, _ = coverage_metadata(raw_root)
    docs = {(item["module"], item["name"]): item
            for item in extract(raw_root, None, {}, metadata) if item["kind"] == "fn"}
    manifest = json.loads((raw_root / "ndk_coverage.json").read_text())
    result = {}
    missing = []
    for item in manifest["symbols"]:
        if item["category"] != "function" or item["status"] != "DIRECTLY_SUPPORTED":
            continue
        key = (item["novus_module"], item["name"])
        if key not in docs:
            missing.append("::".join(key))
            continue
        result[key] = {"manifest": item, "docs": docs[key]}
    if missing:
        raise ValueError("manifest functions missing from source: " + ", ".join(sorted(missing)))
    return result


def relative(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path.resolve())


def test_annotations(roots: list[Path]) -> tuple[dict[tuple[str, str], list[dict]], list[str]]:
    coverage: dict[tuple[str, str], list[dict]] = {}
    errors = []
    paths = sorted({path for root in roots for path in
                    ([root] if root.is_file() else root.rglob("*.novus"))})
    for path in paths:
        text_content = path.read_text()
        if "@covers-ndk" in text_content:
            errors.extend(tautological_assertions(path, text_content))
        pending: list[tuple[tuple[str, str], int]] = []
        effects: list[str] = []
        description = None
        for line_number, line in enumerate(text_content.splitlines(), 1):
            if match := COVER.match(line):
                category, interface, name = match.groups()
                if category != "function":
                    errors.append(f"{path}:{line_number}: callable coverage category must be function")
                pending.append(((interface.strip(), name.strip()), line_number))
            elif match := SIDE_EFFECT.match(line):
                effects.append(match.group(1).strip())
            elif match := TEST.match(line):
                description = match.group(1)
            elif match := TEST_FN.match(line):
                if pending:
                    if description is None:
                        errors.append(f"{path}:{line_number}: @covers-ndk must precede a named @test")
                    for key, annotation_line in pending:
                        coverage.setdefault(key, []).append({
                            "source": relative(path), "line": annotation_line,
                            "test": match.group(1), "description": description,
                            "side_effects": list(effects), "covered_functions": len(pending),
                        })
                pending, effects, description = [], [], None
            elif line.strip() and not line.lstrip().startswith(("//", "@")):
                pending, effects, description = [], [], None
        for _, line_number in pending:
            errors.append(f"{path}:{line_number}: @covers-ndk is not attached to a test")
    return coverage, errors


def compile_evidence(paths: list[Path]) -> dict[tuple[str, str], dict]:
    evidence = {}
    for path in paths:
        report = json.loads(path.read_text())
        for module in report.get("results", []):
            for function in module.get("function_results", []):
                if function.get("status") == "passed":
                    key = (module["module"], function["name"])
                    item = {
                        "report": relative(path),
                        "size_bytes": function.get("bytes_delta"),
                        "build_us": function.get("build_us"),
                    }
                    if key not in evidence or (evidence[key]["size_bytes"] is None and
                                               item["size_bytes"] is not None):
                        evidence[key] = item
    return evidence


def runtime_evidence(paths: list[Path], configuration: str | None = None) -> dict[tuple[str, str, str], dict]:
    evidence = {}
    for path in paths:
        report = json.loads(path.read_text())
        if configuration and report.get("configuration") != configuration:
            continue
        builds = {(item["build"]["suite"], item["build"]["profile"]): item["build"]
                  for item in report.get("tests", []) if "build" in item}
        for item in report.get("tests", []):
            run = item.get("run")
            if not run or run.get("status") != "passed":
                continue
            build = builds.get((run["suite"], run["profile"]), {})
            source = build.get("source")
            if not source:
                continue
            source_path = Path(source)
            source = relative(source_path if source_path.is_absolute() else ROOT / source_path)
            output = run.get("result", {}).get("output", "")
            timings = {}
            for line in ANSI.sub("", output).splitlines():
                if match := re.match(r"^(.*?)\.\.\.\s+PASS(?:\s+\((\d+)\s+µs\))?\s*$", line):
                    timings[match.group(1)] = int(match.group(2)) if match.group(2) else None
            report_path = relative(path)
            evidence[(source, run["profile"], report_path)] = {
                "report": report_path, "tests": timings,
                "memory_checked": bool(report.get("memory_check")),
                "benchmark": bool(report.get("benchmark")),
                "binary_bytes": build.get("bytes"),
                "process_memory_delta": run.get("memory_delta"),
            }
    return evidence


def measure(raw_root: Path, test_roots: list[Path], compile_reports: list[Path],
            runtime_reports: list[Path], configuration: str | None = None) -> dict:
    inventory = callable_inventory(raw_root)
    annotations, errors = test_annotations(test_roots)
    by_interface = {(value["manifest"]["interface"], key[1]): key
                    for key, value in inventory.items()}
    unknown = sorted(set(annotations) - set(by_interface))
    errors.extend(f"unknown @covers-ndk function: {interface}|{name}" for interface, name in unknown)
    compiled = compile_evidence(compile_reports)
    runtime = runtime_evidence(runtime_reports, configuration)
    rows = []
    for key, value in sorted(inventory.items()):
        manifest, docs = value["manifest"], value["docs"]
        mapped = annotations.get((manifest["interface"], key[1]), [])
        runs = []
        for test in mapped:
            for (source, profile, _), evidence in runtime.items():
                if source == test["source"] and test["description"] in evidence["tests"]:
                    runs.append({"test": test["test"], "profile": profile,
                                 "exclusive_timing": test["covered_functions"] == 1,
                                 "microseconds": evidence["tests"][test["description"]], **evidence})
        compile_item = compiled.get(key)
        documented = bool(docs["documentation"].strip())
        effects = bool(mapped) and all(item["side_effects"] for item in mapped)
        runtime_verified = bool(runs)
        leak_verified = any(item["memory_checked"] for item in runs)
        speed_us = min((item["microseconds"] for item in runs
                        if item["exclusive_timing"] and item["microseconds"] is not None), default=None)
        size_bytes = compile_item.get("size_bytes") if compile_item else None
        checks = (documented, compile_item is not None, bool(mapped), effects,
                  runtime_verified, leak_verified, size_bytes is not None, speed_us is not None)
        rows.append({
            "module": key[0], "interface": manifest["interface"], "name": key[1],
            "documented": documented, "compile_link_verified": compile_item is not None,
            "behavior_mapped": bool(mapped), "side_effects_documented": effects,
            "runtime_verified": runtime_verified, "leak_verified": leak_verified,
            "size_bytes": size_bytes, "speed_us": speed_us,
            "bugs": docs["sections"].get("bugs"),
            "superseded_by": docs["sections"].get("superseded_by"),
            "complete": all(checks), "tests": mapped, "runtime": runs,
        })
    fields = ("documented", "compile_link_verified", "behavior_mapped",
              "side_effects_documented", "runtime_verified", "leak_verified", "complete")
    summary = {"functions_total": len(rows), **{
        f"functions_{field}": sum(bool(row[field]) for row in rows) for field in fields},
        "functions_size_measured": sum(row["size_bytes"] is not None for row in rows),
        "functions_speed_measured": sum(row["speed_us"] is not None for row in rows),
        "annotation_errors": len(errors),
    }
    return {"schema_version": 1, "scope": "pinned classic 68k NDK raw callable surface",
            "summary": summary, "errors": errors, "functions": rows}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-root", type=Path, default=ROOT / "Novus/std/amiga/raw")
    parser.add_argument("--test-root", action="append", type=Path, default=[])
    parser.add_argument("--compile-report", action="append", type=Path, default=[])
    parser.add_argument("--runtime-report", action="append", type=Path, default=[])
    parser.add_argument("--report-root", action="append", type=Path, default=[])
    parser.add_argument("--configuration")
    parser.add_argument("--json", type=Path)
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()
    roots = args.test_root or [ROOT / "Novus.Tests/AmigaRuntime", ROOT / "Novus.Tests/Examples",
                               ROOT / "Novus/std/tests"]
    reports = sorted({path for root in args.report_root for path in root.rglob("report.json")})
    result = measure(args.raw_root, roots, args.compile_report + reports,
                     args.runtime_report + reports, args.configuration)
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(result, indent=2) + "\n")
    summary = result["summary"]
    print("NDK callable evidence: " + ", ".join(
        f"{key.removeprefix('functions_').replace('_', ' ')} {value}/{summary['functions_total']}"
        for key, value in summary.items() if key.startswith("functions_") and key != "functions_total"))
    for error in result["errors"]:
        print(error, file=sys.stderr)
    incomplete = summary["functions_complete"] != summary["functions_total"]
    return 1 if result["errors"] or (args.require_complete and incomplete) else 0


if __name__ == "__main__":
    raise SystemExit(main())
