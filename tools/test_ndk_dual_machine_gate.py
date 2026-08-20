#!/usr/bin/env python3

import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

import runpy
from unittest.mock import patch


class _DualGateTest(unittest.TestCase):
    def test_gateway_runs_requested_configurations_and_verifies_each(self):
        module = runpy.run_path("tools/amiga/run_ndk_dual_machine_gate.py")
        main = module["main"]
        executed = []

        def fake_run(cmd, cwd=None, check=False):  # noqa: ARG001
            executed.append(list(cmd))
            if Path(cmd[1]).name == "run_runtime_suite.py":
                build_dir = Path(cmd[5])
                build_dir.mkdir(parents=True, exist_ok=True)
                report = build_dir / "report.json"
                config = cmd[3]
                report.write_text(json.dumps({"configuration": config, "tests": []}))
                return SimpleNamespace(returncode=0)
            if Path(cmd[1]).name == "verify_ndk_tests.py":
                json_index = cmd.index("--json")
                output = Path(cmd[json_index + 1])
                output.parent.mkdir(parents=True, exist_ok=True)
                output.write_text(json.dumps({
                    "errors": [],
                    "functions": [{
                        "module": "demo",
                        "interface": "demo.library",
                        "name": "Thing",
                        "runtime_verified": True,
                        "leak_verified": True,
                        "documented": True,
                        "behavior_mapped": True,
                        "side_effects_documented": True,
                    }],
                    "summary": {},
                }))
                return SimpleNamespace(returncode=0)
            return SimpleNamespace(returncode=0)

        with tempfile.TemporaryDirectory() as temporary:
            build_dir = Path(temporary) / "runtime"
            with patch("tools.amiga.run_ndk_dual_machine_gate.subprocess.run", side_effect=fake_run):
                with patch.object(sys, "argv", [
                    "tools/amiga/run_ndk_dual_machine_gate.py",
                    "--build-dir", str(build_dir),
                    "--configuration", "A1200",
                    "--require-complete",
                    "--layer", "amiga",
                    "--suite", "ndk-intuition-core",
                ]):
                    status = main()
            self.assertEqual(0, status)

        runtime_commands = [entry for entry in executed if Path(entry[1]).name == "run_runtime_suite.py"]
        verify_commands = [entry for entry in executed if Path(entry[1]).name == "verify_ndk_tests.py"]
        self.assertEqual(1, len(runtime_commands))
        self.assertEqual(1, len(verify_commands))
        for command in runtime_commands:
            self.assertIn("--layer", command)
            self.assertIn("amiga", command)


if __name__ == "__main__":
    unittest.main()
