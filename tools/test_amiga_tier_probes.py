import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_amiga_tier_probes import import_aliases, probe_source


class AmigaTierProbeTests(unittest.TestCase):
    def test_trait_method_probe_uses_a_concrete_implementation(self):
        member = {
            "name": "value", "signature": "fn value(&self) -> u32 {",
            "parameters": [{"name": "self", "type": "Self", "modifiers": "&", "receiver": True}],
            "returns": {"type": "u32"},
        }
        item = {
            "id": "amiga::demo::Demo::value()", "owner": "Demo", "name": "value",
            "owner_generics": [], "symbol": {**member, "module": "amiga::demo"},
            "trait_members": [member],
        }
        source = probe_source("amiga::demo", [item], {"Demo": {"amiga::demo"}})
        self.assertIn("impl Demo for _TierProbe0", source)
        self.assertIn("receiver0.value()", source)
        self.assertNotIn("{ {", source)

    def test_error_bounded_generic_uses_a_real_error_type(self):
        symbol = {
            "module": "amiga::demo", "name": "show", "signature": "pub fn show<E>(error: E) where E: Error",
            "parameters": [{"name": "error", "type": "E", "modifiers": "", "receiver": False}],
            "returns": None,
        }
        item = {"id": "amiga::demo::show(E)", "owner": "", "name": "show",
                "owner_generics": [], "symbol": symbol, "trait_members": None}
        source = probe_source("amiga::demo", [item], {"StringError": {"string::core"}})
        self.assertIn("from std::string::core import StringError", source)
        self.assertIn("show::<StringError>(@zeroed(StringError))", source)

    def test_signature_import_alias_uses_the_published_type(self):
        symbol = {
            "module": "amiga::demo", "name": "from_system",
            "signature": "pub fn from_system(value: SystemValue)",
            "parameters": [{"name": "value", "type": "SystemValue", "modifiers": "", "receiver": False}],
            "returns": None,
        }
        item = {"id": "amiga::demo::from_system(SystemValue)", "owner": "", "name": "from_system",
                "owner_generics": [], "symbol": symbol, "trait_members": None,
                "aliases": {"SystemValue": ("Value", "amiga::sys::demo")}}

        source = probe_source("amiga::demo", [item], {"Value": {"amiga::sys::demo"}})

        self.assertIn("from amiga::sys::demo import Value", source)
        self.assertIn("from_system(@zeroed(Value))", source)

    def test_unaliased_import_records_the_source_module(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            module = root / "amiga" / "demo.novus"
            module.parent.mkdir()
            module.write_text("from amiga::sys::audio::errors import AudioError\n")

            imports = import_aliases(root)

        self.assertEqual(
            ("AudioError", "amiga::sys::audio::errors"),
            imports["amiga::demo"]["AudioError"],
        )

        source = probe_source("amiga::demo", [{
            "id": "demo", "owner": "", "name": "use_result", "owner_generics": [],
            "symbol": {"module": "amiga::demo", "signature": "fn use_result(value: Result)",
                       "parameters": [{"name": "value", "type": "Result", "modifiers": "", "receiver": False}],
                       "returns": None},
            "aliases": {"Result": ("Result", "std::core")}, "trait_members": None,
        }], {"Result": {"core"}})
        self.assertIn("from std::core import Result", source)

    def test_extension_method_probe_imports_its_defining_module(self):
        symbol = {
            "module": "amiga::sys::device::block", "name": "read_at",
            "signature": "pub fn read_at(&var self, offset: u32)",
            "parameters": [
                {"name": "self", "type": "DeviceRequest", "modifiers": "&var", "receiver": True},
                {"name": "offset", "type": "u32", "modifiers": "", "receiver": False},
            ],
            "returns": None,
        }
        item = {"id": "read_at", "owner": "DeviceRequest", "name": "read_at",
                "owner_generics": [], "symbol": symbol, "trait_members": None}

        source = probe_source("amiga::sys::device::block", [item], {
            "DeviceRequest": {"amiga::sys::device::request"},
            "SystemBlockGeometry": {"amiga::sys::device::block"},
        })

        self.assertIn("from amiga::sys::device::block import SystemBlockGeometry", source)
        self.assertIn("from amiga::sys::device::request import DeviceRequest", source)


if __name__ == "__main__":
    unittest.main()
