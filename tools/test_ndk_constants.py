import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from verify_ndk_constants import c_expression, expression, probe_source, typed_raw


class NdkConstantVerifierTests(unittest.TestCase):
    def test_novus_scalar_expression_becomes_target_c(self):
        self.assertEqual("((unsigned long)1 << 31) | 2",
                         c_expression("((u32)1 << 31) | 2"))
        self.assertEqual("0x80000000", c_expression("$80000000"))
        self.assertEqual("(1UL << 31UL)", typed_raw("(1 << 31)", "u32"))
        self.assertEqual("7", expression("pub const VALUE: u32 = 7"))

    def test_probe_compares_header_macro_to_raw_definition(self):
        symbol = {"name": "MEMF_PUBLIC", "definition": "(1L << 0)",
                  "raw_definition": "((u32)1 << 0)", "raw_type": "u32"}
        source = probe_source([symbol], {"MEMF_PUBLIC": symbol})
        self.assertIn("#define NDK_MEMF_PUBLIC", source)
        self.assertIn("NDK_MEMF_PUBLIC", source)
        self.assertIn("RAW_MEMF_PUBLIC", source)
        self.assertIn("((unsigned long)1UL << 0UL)", source)

    def test_probe_can_normalize_undefined_signed_header_shifts(self):
        symbol = {"name": "TOP_BIT", "definition": "(1 << 31)",
                  "raw_definition": "(1 << 31)", "raw_type": "u32"}
        source = probe_source([symbol], {"TOP_BIT": symbol}, normalize_ndk=True)
        self.assertIn("#define NDK_TOP_BIT ((1UL << 31UL))", source)
        self.assertIn("#define RAW_TOP_BIT ((1UL << 31UL))", source)


if __name__ == "__main__":
    unittest.main()
