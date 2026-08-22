#!/usr/bin/env python3

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from verify_ndk_tests import measure


class NdkTestEvidenceTests(unittest.TestCase):
    def test_complete_callable_requires_all_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            raw = root / "raw"
            raw.mkdir()
            (raw / "demo.novus").write_text("/// Does the thing.\nextern pub fn Thing(value: u32) -> u32\n")
            (raw / "ndk_coverage.json").write_text(json.dumps({"symbols": [{
                "category": "function", "name": "Thing", "interface": "demo.library",
                "novus_module": "demo", "status": "DIRECTLY_SUPPORTED",
            }]}))
            test = root / "test.novus"
            test.write_text(
                "// @covers-ndk function|demo.library|Thing\n"
                "// @ndk-side-effect reads value and changes no global state\n"
                "@test(\"Thing behaves\")\n"
                "pub fn thing_behaves() { expect(Thing(1) == 1, \"result\") }\n")
            compile_report = root / "compile.json"
            compile_report.write_text(json.dumps({"results": [{
                "module": "demo", "function_results": [
                    {"name": "Thing", "status": "passed", "bytes_delta": 12}],
            }]}))
            unmeasured_compile_report = root / "compile-unmeasured.json"
            unmeasured_compile_report.write_text(json.dumps({"results": [{
                "module": "demo", "function_results": [{"name": "Thing", "status": "passed"}],
            }]}))
            runtime_report = root / "runtime.json"
            runtime_report.write_text(json.dumps({
                "configuration": "A4000", "benchmark": True, "memory_check": True, "tests": [
                    {"build": {"suite": "demo", "profile": "release-o1",
                               "source": str(test), "bytes": 100}},
                    {"run": {"suite": "demo", "profile": "release-o1", "status": "passed",
                             "memory_delta": 0, "result": {"output": "Thing behaves... \u009b1mPASS\u009b0m (7 µs)"}}},
                ]}))
            unmeasured_runtime_report = root / "runtime-unmeasured.json"
            unmeasured_runtime_report.write_text(json.dumps({
                "configuration": "A4000", "benchmark": False, "memory_check": False,
                "tests": [
                    {"build": {"suite": "demo", "profile": "release-o1",
                               "source": str(test), "bytes": 100}},
                    {"run": {"suite": "demo", "profile": "release-o1", "status": "passed",
                             "memory_delta": 0, "result": {"output": "Thing behaves... PASS"}}},
                ]}))

            result = measure(raw, [test], [compile_report, unmeasured_compile_report],
                             [runtime_report, unmeasured_runtime_report], "A4000")

            self.assertEqual([], result["errors"])
            self.assertEqual("A4000", result["configuration"])
            self.assertEqual(1, result["summary"]["functions_complete"], result)
            self.assertEqual(12, result["functions"][0]["size_bytes"])
            self.assertEqual(7, result["functions"][0]["speed_us"])
            self.assertNotIn("tests", result["functions"][0]["runtime"][0])

            wrong_machine = measure(raw, [test], [compile_report], [runtime_report], "A1200")
            self.assertFalse(wrong_machine["functions"][0]["runtime_verified"])

    def test_unknown_annotation_and_missing_side_effect_are_reported(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            raw = root / "raw"
            raw.mkdir()
            (raw / "demo.novus").write_text("/// Documented.\nextern pub fn Thing()\n")
            (raw / "ndk_coverage.json").write_text(json.dumps({"symbols": [{
                "category": "function", "name": "Thing", "interface": "demo.library",
                "novus_module": "demo", "status": "DIRECTLY_SUPPORTED",
            }]}))
            test = root / "test.novus"
            test.write_text(
                "// @covers-ndk function|demo.library|Missing\n"
                "@test(\"missing\")\npub fn missing() {}\n"
                "// @covers-ndk function|demo.library|Thing\n"
                "@test(\"thing\")\npub fn thing() {}\n")

            result = measure(raw, [test], [], [])

            self.assertIn("unknown @covers-ndk function: demo.library|Missing", result["errors"])
            self.assertFalse(result["functions"][0]["side_effects_documented"])

    def test_unconditional_passing_assertions_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            raw = root / "raw"
            raw.mkdir()
            (raw / "demo.novus").write_text("/// Documented.\nextern pub fn Thing()\n")
            (raw / "ndk_coverage.json").write_text(json.dumps({"symbols": [{
                "category": "function", "name": "Thing", "interface": "demo.library",
                "novus_module": "demo", "status": "DIRECTLY_SUPPORTED",
            }]}))
            test = root / "test.novus"
            test.write_text(
                "// @covers-ndk function|demo.library|Thing\n"
                "// @ndk-side-effect none\n"
                "@test(\"fake\")\npub fn fake() {\n"
                "  expect(true, \"literal\")\n"
                "  expect(1 == 1, \"constant\")\n"
                "  expect_eq(value, value, \"self comparison\")\n"
                "}\n")

            result = measure(raw, [test], [], [])

            self.assertEqual(3, sum("unconditional passing assertion" in error
                                    for error in result["errors"]), result["errors"])


if __name__ == "__main__":
    unittest.main()
