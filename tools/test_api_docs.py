import tempfile
import unittest
from pathlib import Path

from autodoc_parser import AutodocParser
from generate_api_docs import extract


class ApiDocsTests(unittest.TestCase):
    def test_test_modules_are_not_public_api(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "tests").mkdir()
            (root / "tests" / "fixture.novus").write_text("pub fn fixture() {}\n")

            self.assertEqual([], extract(root, None, {}))

    def test_malformed_ndk_page_header_still_splits_functions(self):
        with tempfile.TemporaryDirectory() as directory:
            parser = AutodocParser(directory)
            parser._parse_content("""\fexample.library/Firstexample.library/First
NAME
 First - first summary
FUNCTION
 First body.
\fexample.library/Second        example.library/Second
NAME
 Second - second summary
FUNCTION
 Second body.
""", "example")
            self.assertEqual("first summary", parser.get_function("First").summary)
            self.assertEqual("second summary", parser.get_function("Second").summary)

    def test_extractor_emits_structured_sections(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "demo.novus").write_text("""/// Opens a demo.
/// # Arguments
/// * `name` - demo name
/// # Returns
/// A newly opened demo.
pub fn open(name: *u8) -> Result<Demo, DemoError> {}
""")
            symbol = extract(root, None, {})[0]
            self.assertEqual("Opens a demo.", symbol["summary"])
            self.assertIn("arguments", symbol["sections"])
            self.assertEqual("name", symbol["parameters"][0]["name"])
            self.assertEqual("*u8", symbol["parameters"][0]["type"])
            self.assertEqual("Result<Demo, DemoError>", symbol["returns"]["type"])
            self.assertEqual("A newly opened demo.", symbol["returns"]["documentation"])

    def test_extractor_uses_library_context_for_duplicate_function_names(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "amiga" / "raw"
            root.mkdir(parents=True)
            (root / "amiga_lib.novus").write_text("extern pub fn BeginIO(request: *u8)\n")
            parser = AutodocParser(directory)
            parser._parse_content("""amiga.lib/BeginIO amiga.lib/BeginIO
NAME
 BeginIO - generic device call
serial.device/BeginIO serial.device/BeginIO
NAME
 BeginIO - serial-only call
""", "example")

            symbol = extract(root, parser, {})[0]
            self.assertEqual("generic device call", symbol["summary"])
            self.assertIsNone(parser.get_unique_function("BeginIO"))

    def test_extractor_qualifies_implementation_methods(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "demo.novus").write_text("""pub struct Device {}

impl Device {
    /// Opens the device.
    pub fn open() {}
}
""")

            method = next(symbol for symbol in extract(root, None, {}) if symbol["name"] == "open")
            self.assertEqual("Device", method["owner"])
            self.assertEqual("demo::Device::open", method["qualified_name"])


if __name__ == "__main__":
    unittest.main()
