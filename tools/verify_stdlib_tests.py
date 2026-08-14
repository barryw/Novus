#!/usr/bin/env python3
"""Verify compile and runtime coverage for public generic std callables."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from generate_api_docs import extract, function_shape


ROOT = Path(__file__).resolve().parents[1]
COVERS = re.compile(r"^\s*//\s*@covers\s+(.+?)\s*$")
TEST = re.compile(r"^\s*@test(?:\((.*)\))?\s*$")
FUNCTION = re.compile(r"^\s*pub\s+fn\s+([A-Za-z_]\w*)")


def callable_id(module: str, owner: str, name: str, signature: str) -> str:
    parameters, _ = function_shape(signature, "")
    types = ", ".join(parameter["type"] for parameter in parameters if not parameter["receiver"])
    path = "::".join(value for value in ("std", module, owner, name) if value)
    return f"{path}({types})"


def inventory(std_root: Path) -> dict[str, dict]:
    callables: dict[str, dict] = {}
    for symbol in extract(std_root, None, {}):
        if symbol["source"].startswith("amiga/"):
            continue
        if symbol["kind"] == "fn":
            identifier = callable_id(
                symbol["module"], symbol["owner"], symbol["name"], symbol["signature"]
            )
            if identifier in callables:
                raise ValueError(f"duplicate public callable: {identifier}")
            callables[identifier] = {
                "module": symbol["module"],
                "source": symbol["source"],
                "line": symbol["line"],
                "signature": symbol["signature"].splitlines()[0],
            }

        if symbol["kind"] not in {"trait", "class"}:
            continue
        for member in symbol["members"]:
            if "fn " not in member["signature"]:
                continue
            identifier = callable_id(
                symbol["module"], symbol["name"], member["name"], member["signature"]
            )
            if identifier in callables:
                raise ValueError(f"duplicate public callable: {identifier}")
            callables[identifier] = {
                "module": symbol["module"],
                "source": symbol["source"],
                "line": symbol["line"],
                "signature": member["signature"],
            }
    return callables


def test_coverage(test_roots: list[Path]) -> dict[str, list[dict]]:
    coverage: dict[str, list[dict]] = defaultdict(list)
    for root in test_roots:
        files = [root] if root.is_file() else sorted(root.rglob("*.novus"))
        for path in files:
            pending: list[tuple[str, int]] = []
            awaiting_test: tuple[bool, str | None] | None = None
            for line_number, line in enumerate(path.read_text().splitlines(), 1):
                match = COVERS.match(line)
                if match:
                    pending.append((match.group(1).strip(), line_number))
                    continue
                annotation = TEST.match(line)
                if annotation:
                    arguments = annotation.group(1) or ""
                    reason = re.search(r'\bskip\s*=\s*"([^"]*)"', arguments)
                    skipped = bool(reason or re.search(r"(?:^|,)\s*skip\s*(?:,|$)", arguments))
                    awaiting_test = (skipped, reason.group(1) if reason else None)
                    continue
                function = FUNCTION.match(line) if awaiting_test is not None else None
                if function:
                    skipped, skip_reason = awaiting_test
                    for identifier, annotation_line in pending:
                        coverage[identifier].append({
                            "test": function.group(1),
                            "source": (path.relative_to(ROOT) if path.is_relative_to(ROOT) else path).as_posix(),
                            "line": annotation_line,
                            "skipped": skipped,
                            "skip_reason": skip_reason,
                        })
                    pending = []
                    awaiting_test = None
                elif line.strip() and not line.lstrip().startswith(("//", "@")):
                    pending = []
                    awaiting_test = None
    return dict(coverage)


def verify(std_root: Path, test_roots: list[Path]) -> dict:
    callables = inventory(std_root)
    coverage = test_coverage(test_roots)
    unknown = sorted(set(coverage) - set(callables))
    missing = sorted(set(callables) - set(coverage))
    runtime_covered = {
        identifier for identifier, tests in coverage.items()
        if any(not test["skipped"] for test in tests)
    }
    runtime_unverified = sorted(set(callables) - runtime_covered)
    return {
        "schema_version": 1,
        "scope": "Novus/std excluding Novus/std/amiga",
        "callables_total": len(callables),
        "callables_covered": len(callables) - len(missing),
        "callables_missing": len(missing),
        "callables_runtime_covered": len(callables) - len(runtime_unverified),
        "callables_runtime_unverified": len(runtime_unverified),
        "unknown_annotations": unknown,
        "missing": [{"id": identifier, **callables[identifier]} for identifier in missing],
        "runtime_unverified": [
            {"id": identifier, **callables[identifier], "tests": coverage.get(identifier, [])}
            for identifier in runtime_unverified
        ],
        "covered": [{"id": identifier, **callables[identifier], "tests": coverage[identifier]}
                    for identifier in sorted(set(callables) & set(coverage))],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--std-root", type=Path, default=ROOT / "Novus/std")
    parser.add_argument("--tests", type=Path, action="append",
                        default=[ROOT / "Novus/std/tests", ROOT / "Novus.Tests/AmigaRuntime"])
    parser.add_argument("--json", type=Path, help="write the complete machine-readable report")
    parser.add_argument("--allow-missing", action="store_true", help="report gaps without failing")
    parser.add_argument("--require-runtime", action="store_true",
                        help="also fail when a callable is covered only by skipped tests")
    args = parser.parse_args()

    report = verify(args.std_root, args.tests)
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(report, indent=2) + "\n")

    print(f"generic std callables: {report['callables_covered']}/{report['callables_total']} covered")
    print(f"generic std runtime: {report['callables_runtime_covered']}/{report['callables_total']} exercised")
    if report["unknown_annotations"]:
        print("unknown @covers targets:", file=sys.stderr)
        for identifier in report["unknown_annotations"]:
            print(f"  {identifier}", file=sys.stderr)
    if report["missing"]:
        modules: dict[str, int] = defaultdict(int)
        for entry in report["missing"]:
            modules[entry["module"]] += 1
        print("missing by module: " + ", ".join(
            f"{module}={count}" for module, count in sorted(modules.items())
        ))
    if report["runtime_unverified"]:
        print(f"runtime fixture required: {report['callables_runtime_unverified']} callable(s)")
    failed = bool(report["unknown_annotations"] or report["missing"] or
                  (args.require_runtime and report["runtime_unverified"]))
    return 0 if args.allow_missing or not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
