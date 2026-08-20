import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from autodoc_parser import AutodocParser, FunctionDoc
from update_ffi_docs import FFIUpdater


class FfiDocumentationUpdaterTests(unittest.TestCase):
    def test_rendered_autodoc_has_no_trailing_whitespace(self):
        doc = FunctionDoc("demo.library", "Demo", synopsis="Demo()\n\nvoid Demo();")

        result = doc.to_novus_doc()

        self.assertFalse(any(line.endswith(" ") for line in result.splitlines()))

    def test_library_annotation_resolves_duplicate_function_names(self):
        with tempfile.TemporaryDirectory() as temporary:
            parser = AutodocParser(temporary)
            wrong = FunctionDoc("wrong.library", "Shared", summary="Wrong contract")
            right = FunctionDoc("right.library", "Shared", summary="Right contract")
            parser.functions.update({"Shared": wrong, "wrong.library/Shared": wrong,
                                     "right.library/Shared": right})
            path = Path(temporary) / "raw.novus"
            path.write_text('@library("right.library")\nextern pub fn Shared()\n')

            result = FFIUpdater(parser).update_file(path, dry_run=True)

            self.assertIn("/// Right contract", result)
            self.assertNotIn("Wrong contract", result)

    def test_variadic_wrapper_uses_a_entry_contract(self):
        with tempfile.TemporaryDirectory() as temporary:
            parser = AutodocParser(temporary)
            doc = FunctionDoc("demo.library", "MakeA", summary="Creates the object")
            parser.functions["demo.library/MakeA"] = doc
            path = Path(temporary) / "raw.novus"
            path.write_text('// Library: demo.library\nextern pub fn Make()\n')

            result = FFIUpdater(parser).update_file(path, dry_run=True)

            self.assertIn("/// Creates the object", result)


if __name__ == "__main__":
    unittest.main()
