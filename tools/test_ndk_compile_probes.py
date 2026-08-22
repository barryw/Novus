import unittest
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from generate_api_docs import function_shape
from verify_ndk_compile_probes import argument, probe_source, shard_modules


class NdkCompileProbeTests(unittest.TestCase):
    def test_arguments_preserve_raw_types_without_constructing_live_objects(self):
        self.assertEqual("(u32)0", argument("u32"))
        self.assertEqual("(*IORequest)null", argument("*IORequest"))
        self.assertEqual("@zeroed(fn() -> u32)", argument("fn() -> u32"))

    def test_probe_references_every_function_inside_unsafe(self):
        source = probe_source("amiga::raw::demo", [{
            "name": "Demo",
            "parameters": [{"type": "*u8", "modifiers": ""}],
        }])
        self.assertIn("from amiga::raw::demo import *", source)
        self.assertIn("let _ = Demo((*u8)null)", source)
        self.assertIn("unsafe {", source)

    def test_callback_return_arrow_does_not_swallow_following_parameters(self):
        parameters, _ = function_shape(
            "extern pub fn Demo(callback: fn() -> u32, size: u32, data: *u8)", "")
        self.assertEqual(["fn() -> u32", "u32", "*u8"],
                         [parameter["type"] for parameter in parameters])

    def test_shards_balance_callable_counts_without_splitting_modules(self):
        modules = {"large": [{}] * 5, "medium": [{}] * 3, "small": [{}] * 2}
        shards = shard_modules(modules, 2)
        self.assertEqual([5, 5], [sum(map(len, shard.values())) for shard in shards])
        self.assertEqual(set(modules), set().union(*map(set, shards)))


if __name__ == "__main__":
    unittest.main()
