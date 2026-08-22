#!/usr/bin/env python3

import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent / "amiga"))
import run_runtime_suite as runtime


class _Client:
    def __init__(self):
        self.key_states = []

    def call(self, name, arguments, timeout=150):
        if name == "fsuae_input":
            self.key_states.append(arguments["pressed"])
            return {}
        if name == "fsuae_command_execute":
            raise RuntimeError("guest command failed")
        return {}


class _Machine:
    id = "machine"

    def __init__(self):
        self.client = _Client()

    def diagnostics(self):
        return {}

    def recover(self):
        return "recovered"


class AmigaRuntimeSuiteTests(unittest.TestCase):
    def test_available_memory_tolerates_a_transient_guest_command_failure(self):
        responses = [RuntimeError("transient"), {"output": "total  10\n"},
                     {"output": "total  12\n"}]
        with patch.object(runtime, "guest_command", side_effect=responses):
            self.assertEqual(12, runtime.available_memory(_Machine()))

    def test_disable_patchasl_finds_breaks_and_verifies_process(self):
        machine = _Machine()
        status = {"output": (
            "Process  3: stk  4096, gv 150, pri   0 "
            "Loaded as command: MUI:PatchASL\n"
        )}
        with patch.object(runtime, "guest_command", side_effect=[status, {}, {"output": ""}]) as command:
            result = runtime.disable_patchasl(machine)

        self.assertEqual({"status": "stopped", "process": 3}, result)
        self.assertEqual("Break 3 C", command.call_args_list[1].args[1])

    def test_held_key_is_released_when_guest_command_fails(self):
        machine = _Machine()
        with tempfile.TemporaryDirectory() as temporary:
            executable = Path(temporary) / "tests"
            executable.write_bytes(b"test")
            with patch.object(runtime, "available_memory", return_value=0):
                result = runtime.run_suite(
                    machine, executable, "ndk-input-device", "release-o1", 1, 0)

        self.assertEqual([True, False], machine.client.key_states)
        self.assertEqual("infrastructure_failed", result["status"])


if __name__ == "__main__":
    unittest.main()
