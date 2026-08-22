#!/usr/bin/env python3
"""Measure per-callable documentation and test evidence for the Amiga tier-1/tier-2 API.

`amiga::raw` (tier 3) is covered by tools/verify_ndk_tests.py, which accounts for the
pinned NDK inventory. This tool accounts for the Novus-authored layers above it:

  tier 1  the application layer under `amiga::` (excluding `amiga::sys` and `amiga::raw`)
  tier 2  the systems layer under `amiga::sys`

Evidence per callable mirrors the tier-3 report so the three tiers compose:
documented, behavior mapped (a `// @covers` annotation on a runtime test), compile/link
verified with a measured size delta, runtime verified, and leak verified. Benchmark
timings are always retained; an exclusive speed is reported separately when a test
covers exactly one callable.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from generate_api_docs import extract, function_shape
from verify_ndk_tests import ANSI, compile_evidence, relative, tautological_assertions
from verify_stdlib_tests import COVERS, TEST, FUNCTION

ROOT = Path(__file__).resolve().parents[1]
TIER_ONE = "tier1"
TIER_TWO = "tier2"


def tier_of(source: str) -> str | None:
    if not source.startswith("amiga/"):
        return None
    if source.startswith("amiga/raw/"):
        return None
    return TIER_TWO if source.startswith("amiga/sys/") else TIER_ONE


def callable_id(module: str, owner: str, name: str, signature: str) -> str:
    parameters, _ = function_shape(signature, "")
    types = ", ".join(parameter["type"] for parameter in parameters if not parameter["receiver"])
    path = "::".join(value for value in (module, owner, name) if value)
    return f"{path}({types})"


def inventory(std_root: Path) -> dict[str, dict]:
    callables: dict[str, dict] = {}

    def record(symbol: dict, owner: str, name: str, signature: str, documentation: str) -> None:
        tier = tier_of(symbol["source"])
        if tier is None:
            return
        identifier = callable_id(symbol["module"], owner, name, signature)
        if identifier in callables:
            raise ValueError(f"duplicate public callable: {identifier}")
        callables[identifier] = {
            "tier": tier,
            "module": symbol["module"],
            "owner": owner,
            "name": name,
            "source": symbol["source"],
            "line": symbol["line"],
            "signature": signature.splitlines()[0],
            "documentation": documentation,
        }

    for symbol in extract(std_root, None, {}):
        if symbol["kind"] == "fn":
            record(symbol, symbol["owner"], symbol["name"],
                   symbol["signature"], symbol["documentation"])
            continue
        if symbol["kind"] not in {"trait", "class"}:
            continue
        for member in symbol["members"]:
            if "fn " not in member["signature"]:
                continue
            record(symbol, symbol["name"], member["name"],
                   member["signature"], member.get("documentation", ""))
    return callables


def test_annotations(test_roots: list[Path]) -> tuple[dict[str, list[dict]], list[str]]:
    """Map each `// @covers` target to the runtime tests that exercise it."""
    coverage: dict[str, list[dict]] = defaultdict(list)
    errors: list[str] = []
    for root in test_roots:
        files = [root] if root.is_file() else sorted(root.rglob("*.novus"))
        for path in files:
            text = path.read_text()
            if "@covers amiga::" in text:
                errors.extend(tautological_assertions(path, text))
            pending: list[tuple[str, int]] = []
            description: str | None = None
            skipped = False
            for line_number, line in enumerate(text.splitlines(), 1):
                if match := COVERS.match(line):
                    pending.append((match.group(1).strip(), line_number))
                    continue
                if annotation := TEST.match(line):
                    arguments = annotation.group(1) or ""
                    description = None
                    if quoted := arguments.partition('"')[2].rpartition('"')[0]:
                        description = quoted
                    skipped = "skip" in arguments
                    continue
                if description is not None and (function := FUNCTION.match(line)):
                    for identifier, annotation_line in pending:
                        coverage[identifier].append({
                            "test": function.group(1),
                            "description": description,
                            "source": relative(path),
                            "line": annotation_line,
                            "skipped": skipped,
                            "covered_functions": len(pending),
                        })
                    pending = []
                    description = None
                    continue
                if line.strip() and not line.lstrip().startswith(("//", "@")):
                    if pending and description is None:
                        pending = []
                    description = None
    return dict(coverage), errors


def runtime_evidence(paths: list[Path], configuration: str | None = None) -> dict:
    """Per (source, profile) runtime results, including per-test microseconds."""
    import re

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
            timings = {}
            for line in ANSI.sub("", run.get("result", {}).get("output", "")).splitlines():
                if match := re.match(r"^(.*?)\.\.\.\s+PASS(?:\s+\((\d+)\s+µs\))?\s*$", line):
                    timings[match.group(1)] = int(match.group(2)) if match.group(2) else None
            evidence[(source, run["profile"], relative(path))] = {
                "report": relative(path),
                "tests": timings,
                "memory_checked": bool(report.get("memory_check")),
                "binary_bytes": build.get("bytes"),
            }
    return evidence


def measure(std_root: Path, test_roots: list[Path], compile_reports: list[Path],
            runtime_reports: list[Path], configuration: str | None = None) -> dict:
    callables = inventory(std_root)
    annotations, errors = test_annotations(test_roots)
    # Annotations that name std:: or amiga::raw:: targets belong to the other verifiers.
    unknown = sorted(identifier for identifier in annotations
                     if identifier.startswith("amiga::") and identifier not in callables)
    errors.extend(f"unknown @covers target: {identifier}" for identifier in unknown)

    compiled = compile_evidence(compile_reports)
    compiled_by_name = {key[1]: value for key, value in compiled.items()}
    runtime = runtime_evidence(runtime_reports, configuration)

    rows = []
    for identifier, value in sorted(callables.items()):
        mapped = annotations.get(identifier, [])
        runs = []
        for test in mapped:
            if test["skipped"]:
                continue
            for (source, profile, _), item in runtime.items():
                if source == test["source"] and test["description"] in item["tests"]:
                    runs.append({
                        "test": test["test"],
                        "profile": profile,
                        "exclusive_timing": test["covered_functions"] == 1,
                        "microseconds": item["tests"][test["description"]],
                        **item,
                    })
        compile_item = compiled_by_name.get(identifier)
        documented = bool(value["documentation"].strip())
        runtime_verified = bool(runs)
        leak_verified = any(item["memory_checked"] for item in runs)
        speed_us = min((item["microseconds"] for item in runs
                        if item["exclusive_timing"] and item["microseconds"] is not None),
                       default=None)
        size_bytes = compile_item.get("size_bytes") if compile_item else None
        checks = (documented, compile_item is not None, bool(mapped), runtime_verified,
                  leak_verified, size_bytes is not None)
        rows.append({
            "id": identifier,
            **{key: value[key] for key in ("tier", "module", "owner", "name", "source", "line")},
            "documented": documented,
            "compile_link_verified": compile_item is not None,
            "behavior_mapped": bool(mapped),
            "runtime_verified": runtime_verified,
            "leak_verified": leak_verified,
            "size_bytes": size_bytes,
            "speed_us": speed_us,
            "complete": all(checks),
            "tests": mapped,
            "runtime": runs,
        })

    fields = ("documented", "compile_link_verified", "behavior_mapped",
              "runtime_verified", "leak_verified", "complete")

    def summarize(selected: list[dict]) -> dict:
        return {"callables_total": len(selected),
                **{f"callables_{field}": sum(bool(row[field]) for row in selected)
                   for field in fields},
                "callables_size_measured": sum(row["size_bytes"] is not None for row in selected),
                "callables_speed_measured": sum(row["speed_us"] is not None for row in selected)}

    return {
        "schema_version": 1,
        "scope": "Novus-authored Amiga tier-1 and tier-2 callables",
        "summary": {
            **summarize(rows),
            "annotation_errors": len(errors),
            TIER_ONE: summarize([row for row in rows if row["tier"] == TIER_ONE]),
            TIER_TWO: summarize([row for row in rows if row["tier"] == TIER_TWO]),
        },
        "errors": errors,
        "callables": rows,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--std-root", type=Path, default=ROOT / "Novus/std")
    parser.add_argument("--test-root", action="append", type=Path, default=[])
    parser.add_argument("--compile-report", action="append", type=Path, default=[])
    parser.add_argument("--runtime-report", action="append", type=Path, default=[])
    parser.add_argument("--report-root", action="append", type=Path, default=[])
    parser.add_argument("--configuration")
    parser.add_argument("--json", type=Path)
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()

    roots = args.test_root or [ROOT / "Novus.Tests/AmigaRuntime", ROOT / "Novus.Tests/Examples",
                               ROOT / "Novus/std/tests",
                               ROOT / "ports/hdpart-novus/tests/a4000/ui_controls_test.novus"]
    reports = [path for root in args.report_root for path in sorted(root.rglob("report*.json"))]
    report = measure(args.std_root, roots, args.compile_report,
                     args.runtime_report + reports, args.configuration)

    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(report, indent=2) + "\n")

    summary = report["summary"]
    print(
        f"Amiga tier-1/2 callable evidence: documented {summary['callables_documented']}/"
        f"{summary['callables_total']}, compile link verified "
        f"{summary['callables_compile_link_verified']}/{summary['callables_total']}, "
        f"behavior mapped {summary['callables_behavior_mapped']}/{summary['callables_total']}, "
        f"runtime verified {summary['callables_runtime_verified']}/{summary['callables_total']}, "
        f"leak verified {summary['callables_leak_verified']}/{summary['callables_total']}, "
        f"complete {summary['callables_complete']}/{summary['callables_total']}, "
        f"size measured {summary['callables_size_measured']}/{summary['callables_total']}, "
        f"speed measured {summary['callables_speed_measured']}/{summary['callables_total']}"
    )
    for tier in (TIER_ONE, TIER_TWO):
        item = summary[tier]
        print(f"  {tier}: complete {item['callables_complete']}/{item['callables_total']}, "
              f"behavior mapped {item['callables_behavior_mapped']}/{item['callables_total']}, "
              f"size {item['callables_size_measured']}/{item['callables_total']}, "
              f"speed {item['callables_speed_measured']}/{item['callables_total']}")

    for error in report["errors"]:
        print(error, file=sys.stderr)
    if report["errors"]:
        return 1
    if args.require_complete and summary["callables_complete"] != summary["callables_total"]:
        incomplete = [row["id"] for row in report["callables"] if not row["complete"]]
        print(f"incomplete evidence for {len(incomplete)} callable(s)", file=sys.stderr)
        for identifier in incomplete[:50]:
            print(f"  {identifier}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
