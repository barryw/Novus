#!/usr/bin/env python3
"""
Update Novus FFI files with complete NDK autodoc documentation.

This script reads Novus FFI files, finds extern fn declarations,
and adds documentation comments from the NDK autodocs.

Usage:
    python update_ffi_docs.py <autodocs_dir> <ffi_file> [--dry-run]

Example:
    python update_ffi_docs.py ~/amiga-cc/NDK3.9/Documentation/Autodocs Novus/std/amiga/raw/exec.novus
"""

import os
import re
import sys
import argparse
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# Import the autodoc parser
sys.path.insert(0, os.path.dirname(__file__))
from autodoc_parser import AutodocParser, FunctionDoc
from generate_api_docs import sfd_aliases


class FFIUpdater:
    """Updates Novus FFI files with autodoc documentation"""

    # Pattern to match extern fn declarations (with optional pub)
    EXTERN_FN_PATTERN = re.compile(
        r'^(\s*)extern\s+(?:pub\s+)?fn\s+(\w+)\s*\(',
        re.MULTILINE
    )

    # Pattern to match existing doc comments before a declaration
    DOC_COMMENT_PATTERN = re.compile(r'^(\s*)///.*$', re.MULTILINE)

    def __init__(self, autodoc_parser: AutodocParser, force_update: bool = False,
                 aliases: Dict[Tuple[str, str], str] | None = None):
        self.autodoc_parser = autodoc_parser
        self.force_update = force_update
        self.aliases = aliases or {}
        self.stats = {
            'functions_found': 0,
            'functions_documented': 0,
            'functions_already_documented': 0,
            'functions_no_autodoc': 0,
        }

    def update_file(self, filepath: Path, dry_run: bool = False) -> str:
        """Update a single FFI file with autodoc documentation"""
        content = filepath.read_text()
        lines = content.split('\n')
        declared_library = re.search(r'^//\s*Library:\s*(\S+)', content, re.MULTILINE)
        file_library = "amiga.lib" if filepath.stem == "amiga_lib" else \
            declared_library.group(1) if declared_library else ""

        new_lines = []
        i = 0

        while i < len(lines):
            line = lines[i]

            # Check if this line has an extern fn declaration
            match = self.EXTERN_FN_PATTERN.match(line)
            if match:
                indent = match.group(1)
                func_name = match.group(2)
                self.stats['functions_found'] += 1

                attributes = []
                while new_lines and new_lines[-1].strip().startswith('@'):
                    attributes.insert(0, new_lines.pop())
                has_existing_docs = self._has_doc_comment_before(new_lines)
                annotation = next((re.match(r'\s*@library\("([^"]+)"\)', item)
                                   for item in attributes if '@library(' in item), None)
                library = annotation.group(1) if annotation else file_library
                candidates = [func_name, func_name + "A"]
                if (library, func_name) in self.aliases:
                    candidates.insert(0, self.aliases[(library, func_name)])
                if func_name.endswith("A"):
                    candidates.append(func_name[:-1])
                if func_name.endswith("Tags"):
                    candidates.extend((func_name[:-4] + "A", func_name[:-4]))
                candidates.extend(name.replace("Attrs", "Attr") for name in list(candidates)
                                  if "Attrs" in name)
                if func_name.startswith("Is") and func_name != "IsXXXX":
                    candidates.append("IsXXXX")
                if func_name == "UCopperListInit":
                    candidates.append("CINIT")
                doc = next((self.autodoc_parser.get_function(name, library)
                            for name in candidates
                            if self.autodoc_parser.get_function(name, library)), None)
                doc = doc or next((self.autodoc_parser.get_unique_function(name)
                                   for name in candidates
                                   if self.autodoc_parser.get_unique_function(name)), None)

                if has_existing_docs and not self.force_update:
                    self.stats['functions_already_documented'] += 1
                elif doc:
                    if has_existing_docs:
                        while new_lines and not new_lines[-1].strip():
                            new_lines.pop()
                        while new_lines and new_lines[-1].strip().startswith('///'):
                            new_lines.pop()
                    novus_doc = self._generate_compact_doc(doc, indent)
                    if novus_doc:
                        new_lines.append(novus_doc)
                        self.stats['functions_documented'] += 1
                    else:
                        self.stats['functions_no_autodoc'] += 1
                else:
                    self.stats['functions_no_autodoc'] += 1
                new_lines.extend(attributes)
                new_lines.append(line)
            else:
                new_lines.append(line)

            i += 1

        result = '\n'.join(new_lines)

        if not dry_run:
            filepath.write_text(result)

        return result

    def _has_doc_comment_before(self, lines: List[str]) -> bool:
        """Check if the previous non-empty lines are doc comments"""
        for i in range(len(lines) - 1, -1, -1):
            line = lines[i].strip()
            if not line:
                continue
            if line.startswith('///'):
                return True
            else:
                return False
        return False

    def _generate_compact_doc(self, doc: FunctionDoc, indent: str = "") -> Optional[str]:
        """Generate the complete structured autodoc as Novus doc comments."""
        rendered = doc.to_novus_doc()
        return '\n'.join(indent + line for line in rendered.splitlines()) if rendered else None


def main():
    parser = argparse.ArgumentParser(description='Update Novus FFI files with autodocs')
    parser.add_argument('autodocs_dir', help='Path to NDK Autodocs directory')
    parser.add_argument('ffi_file', help='Path to Novus FFI file to update')
    parser.add_argument('--dry-run', action='store_true', help='Print result without modifying file')
    parser.add_argument('--force', '-f', action='store_true', help='Replace existing documentation')

    args = parser.parse_args()

    # Parse autodocs
    print(f"Parsing autodocs from {args.autodocs_dir}...", file=sys.stderr)
    autodoc_parser = AutodocParser(args.autodocs_dir)
    autodoc_parser.parse_all()
    print(f"Loaded {len(autodoc_parser.functions)} function docs", file=sys.stderr)

    # Update FFI file
    ffi_path = Path(args.ffi_file)
    if not ffi_path.exists():
        print(f"Error: {ffi_path} does not exist", file=sys.stderr)
        sys.exit(1)

    updater = FFIUpdater(autodoc_parser, force_update=args.force,
                         aliases=sfd_aliases(Path(args.autodocs_dir).parents[1]))
    files = sorted(ffi_path.rglob('*.novus')) if ffi_path.is_dir() else [ffi_path]
    for path in files:
        print(f"Updating {path}...", file=sys.stderr)
        result = updater.update_file(path, dry_run=args.dry_run)
        if args.dry_run:
            print(result)

    # Print statistics
    print(f"\nStatistics:", file=sys.stderr)
    print(f"  Functions found: {updater.stats['functions_found']}", file=sys.stderr)
    print(f"  Functions documented: {updater.stats['functions_documented']}", file=sys.stderr)
    print(f"  Already documented: {updater.stats['functions_already_documented']}", file=sys.stderr)
    print(f"  No autodoc available: {updater.stats['functions_no_autodoc']}", file=sys.stderr)


if __name__ == '__main__':
    main()
