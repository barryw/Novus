#!/usr/bin/env python3
"""Run the Amiga runtime suite on multiple configurations with evidence gates."""

from __future__ import annotations

import argparse
import json
import shlex
import shutil
import subprocess
import sys
from pathlib import Path


def _parse_args() -> tuple[argparse.Namespace, list[str]]:
    parser = argparse.ArgumentParser(
        description=(
            "Run tools/amiga/run_runtime_suite.py across multiple configurations and "
            "optionally gate NDK evidence on each one."
        )
    )
    parser.add_argument(
        "--configuration",
        "--configurations",
        action="append",
        choices=("A4000", "A1200"),
        help="machine configuration(s) to execute; defaults to A4000 and A1200",
    )
    parser.add_argument(
        "--build-dir",
        type=Path,
        default=Path(__file__).resolve().parents[2] / ".novus-cache/amiga-runtime-suite",
        help="shared build cache/report directory for runtime runs",
    )
    parser.add_argument(
        "--require-complete",
        action="store_true",
        help=(
            "require complete runtime evidence for each config; with --compile-report, "
            "require complete per-callable evidence"
        ),
    )
    parser.add_argument(
        "--compile-report",
        action="append",
        type=Path,
        default=[],
        help=(
            "optional compile evidence report; used when --require-complete is set "
            "to enforce full function evidence"
        ),
    )
    parser.add_argument(
        "--evidence-dir",
        type=Path,
        help="directory for per-configuration evidence JSON (defaults beside amiga/raw)",
    )
    parser.add_argument(
        "--no-copy-reports",
        action="store_true",
        help="do not preserve per-config report snapshots",
    )
    args, runtime_args = parser.parse_known_args()
    if runtime_args and runtime_args[0] == "--":
        runtime_args = runtime_args[1:]
    args.configurations = args.configuration or ["A4000", "A1200"]
    return args, runtime_args


def _run(cmd: list[str], cwd: Path) -> int:
    return subprocess.run(cmd, cwd=cwd, check=False).returncode


def _run_runtime(
    config: str,
    runtime_runner: Path,
    build_dir: Path,
    runtime_args: list[str],
    cwd: Path,
) -> Path:
    report_path = build_dir / "report.json"
    cmd = [
        sys.executable,
        str(runtime_runner),
        "--configuration",
        config,
        "--build-dir",
        str(build_dir),
        *runtime_args,
    ]
    print("$", " ".join(shlex.quote(part) for part in cmd), flush=True)
    status = _run(cmd, cwd)
    if status:
        raise RuntimeError(f"runtime suite failed on {config}")
    if not report_path.is_file():
        raise FileNotFoundError(f"runtime report missing for {config}: {report_path}")
    return report_path


def _require_complete(
    raw_root: Path,
    runtime_reports: list[Path],
    compile_reports: list[Path],
    config: str,
    strict: bool,
    evidence_dir: Path | None = None,
) -> int:
    verify = Path(__file__).resolve().parents[2] / "tools" / "verify_ndk_tests.py"
    output = (evidence_dir or raw_root.parent / "verify-ndk-dual") / f"runtime-{config.lower()}.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    cmd = [
        sys.executable,
        str(verify),
        "--raw-root",
        str(raw_root),
        "--configuration",
        config,
        "--json",
        str(output),
    ]
    if strict and compile_reports:
        cmd.append("--require-complete")
    for report in runtime_reports:
        cmd.extend(["--runtime-report", str(report)])
    for report in compile_reports:
        cmd.extend(["--compile-report", str(report)])
    print("$", " ".join(shlex.quote(part) for part in cmd), flush=True)
    status = _run(cmd, raw_root.parents[3])
    if status:
        return 1
    data = json.loads(output.read_text())
    if data["errors"]:
        print(f"evidence errors for {config}:")
        print("\n".join(data["errors"]), file=sys.stderr)
        return 1
    required = (
        "runtime_verified",
        "leak_verified",
        "documented",
        "behavior_mapped",
        "side_effects_documented",
    )
    if strict and compile_reports:
        required = ("complete",)
    missing = [
        f"{f['module']}::{f['interface']}::{f['name']}"
        for f in data["functions"]
        if any(not f[field] for field in required)
    ]
    if missing:
        sample = ", ".join(missing[:6]) + ("..." if len(missing) > 6 else "")
        print(
            f"{config}: evidence missing for {len(missing)} functions: {sample}",
            file=sys.stderr,
        )
        return 1
    return 0


def main() -> int:
    args, runtime_args = _parse_args()
    root = Path(__file__).resolve().parents[2]
    runtime_runner = root / "tools" / "amiga" / "run_runtime_suite.py"
    raw_root = root / "Novus" / "std" / "amiga" / "raw"

    print(f"runtime configurations: {', '.join(args.configurations)}")
    for config in args.configurations:
        try:
            report = _run_runtime(config, runtime_runner, args.build_dir, runtime_args, root)
        except RuntimeError as error:
            print(error, file=sys.stderr)
            return 1
        except FileNotFoundError as error:
            print(error, file=sys.stderr)
            return 1

        snapshot = args.build_dir / f"report-{config.lower()}.json"
        if args.no_copy_reports:
            snapshot = report
        else:
            shutil.copy2(report, snapshot)

        if args.require_complete:
            print(f"verifying complete evidence for {config}")
            if _require_complete(
                raw_root,
                [snapshot],
                args.compile_report,
                config,
                args.require_complete,
                args.evidence_dir,
            ):
                return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
