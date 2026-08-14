import tempfile
import unittest
from pathlib import Path

from verify_stdlib_tests import inventory, test_coverage, verify


class StdlibCoverageTests(unittest.TestCase):
    def test_inventory_distinguishes_overloads_and_trait_methods(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "math.novus").write_text("""pub fn abs(value: i8) -> i8 { return value }
pub fn abs(value: i16) -> i16 { return value }
pub trait Convert {
    fn convert(value: i8) -> i16
}
""")

            self.assertEqual({
                "std::math::Convert::convert(i8)",
                "std::math::abs(i8)",
                "std::math::abs(i16)",
            }, set(inventory(root)))

    def test_annotations_must_belong_to_a_test(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "tests.novus").write_text("""// @covers std::math::abs(i8)
pub fn helper() {}

// @covers std::math::abs(i16)
@test("absolute value")
pub fn absolute_value() {}
""")

            self.assertEqual({"std::math::abs(i16)"}, set(test_coverage([root])))

    def test_verify_reports_missing_and_unknown_targets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            std = root / "std"
            tests = root / "tests"
            std.mkdir()
            tests.mkdir()
            (std / "math.novus").write_text("pub fn abs(value: i8) -> i8 { return value }\n")
            (tests / "math.novus").write_text("""// @covers std::math::missing()
@test("bad target")
pub fn bad_target() {}
""")

            report = verify(std, [tests])

            self.assertEqual(1, report["callables_missing"])
            self.assertEqual(["std::math::missing()"], report["unknown_annotations"])

    def test_skipped_tests_are_compile_coverage_not_runtime_coverage(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            std = root / "std"
            tests = root / "tests"
            std.mkdir()
            tests.mkdir()
            (std / "io.novus").write_text("pub fn read_key() -> u8 { return 0 }\n")
            (tests / "io.novus").write_text("""// @covers std::io::read_key()
@test(skip = "requires input")
pub fn read_key_test() {}
""")

            report = verify(std, [tests])

            self.assertEqual(1, report["callables_covered"])
            self.assertEqual(0, report["callables_runtime_covered"])
            self.assertEqual(["std::io::read_key()"], [
                item["id"] for item in report["runtime_unverified"]
            ])


if __name__ == "__main__":
    unittest.main()
