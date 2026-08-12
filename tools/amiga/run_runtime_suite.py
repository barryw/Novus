#!/usr/bin/env python3
"""Build Novus @test suites and run them on AmigaOS through the FS-UAE MCP server."""

from __future__ import annotations

import argparse
import base64
import json
import re
import subprocess
import sys
import time
import urllib.request
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
FOUNDATION_SUITES = {
    "foundation-primitives": "Novus.Tests/AmigaRuntime/foundation_primitives.novus",
    "foundation-numeric-extended": "Novus.Tests/AmigaRuntime/foundation_numeric_extended.novus",
    "foundation-control-flow": "Novus.Tests/AmigaRuntime/foundation_control_flow.novus",
    "foundation-functions": "Novus.Tests/AmigaRuntime/foundation_functions.novus",
    "foundation-aggregates": "Novus.Tests/AmigaRuntime/foundation_aggregates.novus",
    "foundation-generics-traits": "Novus.Tests/AmigaRuntime/foundation_generics_traits.novus",
    "foundation-errors-patterns": "Novus.Tests/AmigaRuntime/foundation_errors_patterns.novus",
    "foundation-result-custom-error": "Novus.Tests/AmigaRuntime/foundation_result_custom_error.novus",
    "foundation-ownership": "Novus.Tests/AmigaRuntime/foundation_ownership.novus",
    "foundation-tuple-drop": "Novus.Tests/AmigaRuntime/foundation_tuple_drop.novus",
    "foundation-strings": "Novus.Tests/AmigaRuntime/foundation_strings.novus",
    "foundation-bytes": "Novus.Tests/AmigaRuntime/foundation_bytes.novus",
    "foundation-inline-asm": "Novus.Tests/AmigaRuntime/foundation_inline_asm.novus",
    "foundation-systems": "Novus.Tests/AmigaRuntime/foundation_systems.novus",
    "foundation-modules": "Novus.Tests/AmigaRuntime/foundation_modules",
    "const-fn": "Novus.Tests/Examples/test_const_fn.novus",
    "intrinsics": "Novus.Tests/Examples/test_intrinsics.novus",
    "fixed32": "Novus.Tests/Examples/test_fixed32_asm.novus",
}
FOUNDATION_ALL = {"foundation-all": "Novus.Tests/AmigaRuntime"}
FOUNDATION_STANDALONE = {
    name: FOUNDATION_SUITES[name] for name in ("const-fn", "intrinsics", "fixed32")
}
AMIGA_SUITES = {
    "block-device-read": "Novus.Tests/AmigaRuntime/block_device_read.novus",
    "dos-device-list": "Novus.Tests/AmigaRuntime/dos_device_list.novus",
    "dos-node-draft": "Novus.Tests/AmigaRuntime/dos_node_draft.novus",
    "embedded-segment": "Novus.Tests/AmigaRuntime/embedded_segment.novus",
    "filesystem-registry": "Novus.Tests/AmigaRuntime/filesystem_registry.novus",
    "memory": "Novus/std/tests/test_memory.novus",
    "str": "Novus/std/tests/test_str.novus",
    "string": "Novus/std/tests/test_string.novus",
    "string-builder": "Novus/std/tests/test_string_builder.novus",
    "string-parsing": "Novus/std/tests/test_string_parsing.novus",
    "vec": "Novus/std/tests/test_vec.novus",
    "vecdeque": "Novus/std/tests/test_vecdeque.novus",
    "hashset": "Novus/std/tests/test_hashset.novus",
    "path": "Novus/std/tests/test_path.novus",
    "file-io": "Novus/std/tests/test_file_io.novus",
    "prefs": "Novus/std/tests/test_prefs.novus",
    "window": "Novus/std/tests/test_window.novus",
    "drawing": "Novus/std/tests/test_drawing.novus",
    "async-sleep": "Novus.Tests/AmigaRuntime/async_sleep_failures.novus",
    "result-contracts": "Novus.Tests/AmigaRuntime/result_contract_failures.novus",
    "channel": "Novus.Tests/Examples/channel_comprehensive_test.novus",
}
ALL_SUITES = FOUNDATION_ALL | FOUNDATION_SUITES | AMIGA_SUITES
PROFILES = {
    "debug": (0, 2, False),
    "release-o1": (1, 1, True),
    "release-o3": (3, 1, True),
}


class McpError(RuntimeError):
    pass


class McpClient:
    def __init__(self, url: str):
        self.url = url
        self.request_id = 1

    def call(self, name: str, arguments: dict[str, Any], timeout: int = 150) -> Any:
        body = json.dumps({
            "jsonrpc": "2.0",
            "id": self.request_id,
            "method": "tools/call",
            "params": {"name": name, "arguments": arguments},
        }).encode()
        self.request_id += 1
        request = urllib.request.Request(
            self.url,
            data=body,
            headers={"Content-Type": "application/json", "Accept": "application/json"},
        )
        with urllib.request.urlopen(request, timeout=timeout) as response:
            payload = json.load(response)
        if "error" in payload:
            raise McpError(json.dumps(payload["error"], sort_keys=True))
        result = payload.get("result", {})
        text = "\n".join(
            item.get("text", "") for item in result.get("content", [])
            if item.get("type") == "text"
        )
        if result.get("isError"):
            raise McpError(text or "MCP tool failed")
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            return text


class Machine:
    def __init__(self, client: McpClient, configuration: str):
        self.client = client
        self.configuration = configuration
        self.id: str | None = None

    def start(self) -> None:
        running = self.client.call("fsuae_machines_list", {})
        if running:
            raise McpError("Refusing to reuse or stop an existing FS-UAE machine")
        result = self.client.call("fsuae_machine_start", {"configuration": self.configuration})
        match = re.search(r"machine_id ([0-9a-f-]+)", str(result))
        if not match:
            raise McpError(f"Could not parse machine id from: {result}")
        self.id = match.group(1)
        self.wait()

    def wait(self) -> Any:
        assert self.id
        return self.client.call("fsuae_machine_wait", {
            "machine_id": self.id,
            "condition": "workbench",
            "timeout_seconds": 120,
        })

    def diagnostics(self) -> dict[str, Any]:
        assert self.id
        try:
            result = self.client.call("fsuae_machine_diagnostics", {"machine_id": self.id})
            return result if isinstance(result, dict) else {"message": str(result)}
        except Exception as error:
            return {"diagnostics_error": str(error)}

    def recover(self) -> str:
        assert self.id
        try:
            self.client.call("fsuae_machine_reset", {"machine_id": self.id, "hard": True})
            self.wait()
            return "hard_reset"
        except Exception as reset_error:
            previous_id = self.id
            self.stop()
            try:
                self.start()
                return f"restart_after_reset_failure: {reset_error}"
            except Exception as restart_error:
                self.id = previous_id
                return f"recovery_failed: reset={reset_error}; restart={restart_error}"

    def stop(self) -> None:
        if not self.id:
            return
        machine_id, self.id = self.id, None
        try:
            self.client.call("fsuae_machine_stop", {"machine_id": machine_id}, timeout=30)
        except Exception as error:
            print(f"warning: failed to stop {machine_id}: {error}", file=sys.stderr)


def compiler_path(explicit: str | None) -> Path:
    candidates = ([Path(explicit)] if explicit else []) + [
        ROOT / "Novus/bin/Debug/net10.0/Novus.dll",
        ROOT / "Novus/bin/Release/net10.0/Novus.dll",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("Build Novus first, or pass --compiler PATH")


def build_suite(
    compiler: Path, build_root: Path, suite: str, source: Path, profile: str,
    test_filter: str | None,
) -> tuple[Path | None, dict[str, Any]]:
    optimize, safety, release = PROFILES[profile]
    output_dir = build_root / profile / suite
    command = [
        "dotnet", str(compiler), "test", str(source),
        "-o", str(output_dir), "--cpu", "68020",
        "--safety-level", str(safety), "-O", str(optimize),
    ]
    if release:
        command.append("--release")
    if test_filter:
        command.extend(("--filter", test_filter))
    started = time.monotonic()
    process = subprocess.run(command, cwd=ROOT, text=True, capture_output=True)
    record = {
        "status": "built" if process.returncode == 0 else "build_failed",
        "seconds": round(time.monotonic() - started, 3),
        "return_code": process.returncode,
    }
    if process.returncode != 0:
        record["stdout"] = process.stdout[-8000:]
        record["stderr"] = process.stderr[-8000:]
        return None, record
    executable = output_dir / "tests"
    if not executable.is_file():
        record.update(status="build_failed", error=f"missing executable: {executable}")
        return None, record
    record["bytes"] = executable.stat().st_size
    return executable, record


def diagnostic_summary(diagnostics: dict[str, Any]) -> str:
    exception = diagnostics.get("cpu_exception") or {}
    alert = diagnostics.get("alert_code") or diagnostics.get("guru") or diagnostics.get("alert")
    if exception:
        return "cpu exception {vector} ({name}) at {faulting_pc}, task {task_name}".format(
            **{key: exception.get(key, "?") for key in
               ("vector", "name", "faulting_pc", "task_name")}
        )
    if alert:
        return f"alert/guru {alert}"
    return str(diagnostics.get("status", "no structured crash data"))


def run_suite(
    machine: Machine, executable: Path, suite: str, profile: str, timeout: int, index: int
) -> dict[str, Any]:
    assert machine.id
    amiga_name = f"n{index:02x}{profile[-2:].replace('-', '')}"
    machine.client.call("fsuae_exchange_put", {
        "machine_id": machine.id,
        "name": amiga_name,
        "data_base64": base64.b64encode(executable.read_bytes()).decode(),
    })
    started = time.monotonic()
    record: dict[str, Any] = {"suite": suite, "profile": profile}
    try:
        result = machine.client.call("fsuae_command_execute", {
            "machine_id": machine.id,
            "command": f"MCP:{amiga_name}",
            "timeout_seconds": timeout,
        }, timeout=timeout + 15)
        record["result"] = result
        output = result.get("output", "") if isinstance(result, dict) else str(result)
        passed = (
            isinstance(result, dict)
            and result.get("status") == "completed"
            and result.get("succeeded") is True
            and result.get("exit_code") == 0
            and "*** ALL TESTS PASSED ***" in output
        )
        record["status"] = "passed" if passed else "failed"
        if not passed:
            diagnostics = machine.diagnostics()
            record["diagnostics"] = diagnostics
            if (
                diagnostics.get("status") in {"guest_crashed", "guest_command_timed_out", "guruing"}
                or diagnostics.get("guest_control_ready") is False
            ):
                record["recovery"] = machine.recover()
    except Exception as error:
        record.update(status="infrastructure_failed", error=str(error))
        diagnostics = machine.diagnostics()
        record["diagnostics"] = diagnostics
        record["recovery"] = machine.recover()
    record["seconds"] = round(time.monotonic() - started, 3)
    return record


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mcp-url", default="http://localhost:6800/mcp")
    parser.add_argument("--configuration", default="A4000")
    parser.add_argument("--compiler")
    parser.add_argument("--build-dir", type=Path,
                        default=ROOT / ".novus-cache/amiga-runtime-suite")
    parser.add_argument("--profile", action="append", choices=PROFILES,
                        help="repeat for a matrix; default: release-o1")
    parser.add_argument("--layer", choices=("foundation", "amiga", "all"),
                        default="foundation", help="default suite layer")
    parser.add_argument("--suite", action="append", choices=ALL_SUITES,
                        help="repeat to select explicit suites")
    parser.add_argument("--timeout", type=int, default=120,
                        help="seconds allowed for each Amiga test executable")
    parser.add_argument("--filter", help="test-name filter passed to novus test")
    parser.add_argument("--list", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.list:
        for name, path in ALL_SUITES.items():
            layer = "foundation" if name in FOUNDATION_ALL or name in FOUNDATION_SUITES else "amiga"
            print(f"{layer:10} {name:28} {path}")
        return 0
    profiles = args.profile or ["release-o1"]
    layer_suites = {
        "foundation": FOUNDATION_ALL | FOUNDATION_STANDALONE,
        "amiga": AMIGA_SUITES,
        "all": FOUNDATION_ALL | FOUNDATION_STANDALONE | AMIGA_SUITES,
    }
    suites = args.suite or list(layer_suites[args.layer])
    compiler = compiler_path(args.compiler)
    args.build_dir.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {
        "configuration": args.configuration,
        "compiler": str(compiler),
        "profiles": profiles,
        "tests": [],
    }

    builds: list[tuple[str, str, Path]] = []
    for profile in profiles:
        for suite in suites:
            print(f"BUILD {profile:10} {suite}...", end=" ", flush=True)
            executable, build = build_suite(
                compiler, args.build_dir, suite, ROOT / ALL_SUITES[suite], profile,
                args.filter,
            )
            build.update(suite=suite, profile=profile)
            report["tests"].append({"build": build})
            print(f"{build['status']} ({build['seconds']}s)")
            if executable:
                builds.append((profile, suite, executable))

    machine = Machine(McpClient(args.mcp_url), args.configuration)
    try:
        machine.start()
        for index, (profile, suite, executable) in enumerate(builds):
            print(f"RUN   {profile:10} {suite}...", end=" ", flush=True)
            result = run_suite(machine, executable, suite, profile, args.timeout, index)
            report["tests"].append({"run": result})
            detail = ""
            if "diagnostics" in result:
                detail = f" — {diagnostic_summary(result['diagnostics'])}"
            print(f"{result['status']} ({result['seconds']}s){detail}")
    finally:
        machine.stop()

    report_path = args.build_dir / "report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    failures = [
        item for item in report["tests"]
        if next(iter(item.values())).get("status") not in {"built", "passed"}
    ]
    print(f"\nReport: {report_path}")
    print(f"Result: {len(report['tests']) - len(failures)} passed records, {len(failures)} failed")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
