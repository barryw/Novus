#!/usr/bin/env python3
"""
NDK Autodoc Parser for Novus

Parses AmigaOS NDK 3.9 .doc files and extracts function documentation
for use in Novus FFI declarations.

Usage:
    python autodoc_parser.py <ndk_autodocs_dir> [--output-json] [--function <name>]

Example:
    python autodoc_parser.py ~/amiga-cc/NDK3.9/Documentation/Autodocs
    python autodoc_parser.py ~/amiga-cc/NDK3.9/Documentation/Autodocs --function AllocMem
"""

import os
import re
import sys
import json
import argparse
from dataclasses import dataclass, field, asdict
from typing import Dict, List, Optional
from pathlib import Path


@dataclass
class FunctionDoc:
    """Parsed documentation for a single function"""
    library: str
    name: str
    summary: str = ""
    synopsis: str = ""
    function_desc: str = ""
    inputs: List[str] = field(default_factory=list)
    result: str = ""
    warning: str = ""
    notes: List[str] = field(default_factory=list)
    examples: List[str] = field(default_factory=list)
    see_also: List[str] = field(default_factory=list)

    def to_novus_doc(self) -> str:
        """Convert to Novus /// doc comment format"""
        lines = []

        # Summary line
        if self.summary:
            lines.append(f"/// {self.summary}")
            lines.append("///")

        # Function description
        if self.function_desc:
            for line in self.function_desc.strip().split('\n'):
                line = line.strip()
                if line:
                    lines.append(f"/// {line}")
            lines.append("///")

        # Inputs (parameters)
        if self.inputs:
            lines.append("/// # Arguments")
            lines.append("///")
            for inp in self.inputs:
                # Try to extract param name and description
                inp = inp.strip()
                if ' - ' in inp:
                    param, desc = inp.split(' - ', 1)
                    lines.append(f"/// * `{param.strip()}` - {desc.strip()}")
                elif inp:
                    lines.append(f"/// * {inp}")
            lines.append("///")

        # Result/return value
        if self.result:
            lines.append("/// # Returns")
            lines.append("///")
            for line in self.result.strip().split('\n'):
                line = line.strip()
                if line:
                    lines.append(f"/// {line}")
            lines.append("///")

        # Warning
        if self.warning:
            lines.append("/// # Warning")
            lines.append("///")
            for line in self.warning.strip().split('\n'):
                line = line.strip()
                if line:
                    lines.append(f"/// {line}")
            lines.append("///")

        # Notes
        if self.notes:
            lines.append("/// # Notes")
            lines.append("///")
            for note in self.notes:
                for line in note.strip().split('\n'):
                    line = line.strip()
                    if line:
                        lines.append(f"/// {line}")
            lines.append("///")

        # See Also
        if self.see_also:
            see_also_str = ", ".join(self.see_also)
            lines.append(f"/// # See Also")
            lines.append(f"///")
            lines.append(f"/// {see_also_str}")

        # Remove trailing empty comment
        while lines and lines[-1] == "///":
            lines.pop()

        return '\n'.join(lines)


class AutodocParser:
    """Parser for AmigaOS NDK autodoc files"""

    # Pattern to match function headers like "exec.library/AllocMem"
    FUNC_HEADER_PATTERN = re.compile(r'^(\w+(?:\.\w+)?)/(\w+)\s+\1/\2\s*$')

    # Section markers
    SECTIONS = ['NAME', 'SYNOPSIS', 'FUNCTION', 'INPUTS', 'RESULT', 'RESULTS',
                'WARNING', 'NOTE', 'NOTES', 'EXAMPLE', 'EXAMPLES', 'SEE ALSO',
                'TAGS', 'BUGS']

    def __init__(self, autodocs_dir: str):
        self.autodocs_dir = Path(autodocs_dir)
        self.functions: Dict[str, FunctionDoc] = {}

    def parse_all(self) -> Dict[str, FunctionDoc]:
        """Parse all .doc files in the autodocs directory"""
        for doc_file in self.autodocs_dir.glob("*.doc"):
            if doc_file.name.endswith('.uaem'):
                continue
            self.parse_file(doc_file)
        return self.functions

    def parse_file(self, filepath: Path) -> None:
        """Parse a single autodoc file"""
        try:
            # Try different encodings
            content = None
            for encoding in ['latin-1', 'utf-8', 'cp1252']:
                try:
                    content = filepath.read_text(encoding=encoding)
                    break
                except UnicodeDecodeError:
                    continue

            if content is None:
                print(f"Warning: Could not decode {filepath}", file=sys.stderr)
                return

            self._parse_content(content, filepath.stem)
        except Exception as e:
            print(f"Error parsing {filepath}: {e}", file=sys.stderr)

    def _parse_content(self, content: str, library_name: str) -> None:
        """Parse the content of an autodoc file"""
        lines = content.split('\n')

        i = 0
        while i < len(lines):
            line = lines[i]

            # Look for function header pattern (repeated name on same line)
            # e.g., "exec.library/AllocMem                                   exec.library/AllocMem"
            match = self.FUNC_HEADER_PATTERN.match(line.strip())
            if match:
                lib = match.group(1)
                func_name = match.group(2)

                # Find the end of this function's documentation
                end_i = i + 1
                while end_i < len(lines):
                    next_line = lines[end_i]
                    # Check if we hit another function header
                    if self.FUNC_HEADER_PATTERN.match(next_line.strip()):
                        break
                    # Also check for TABLE OF CONTENTS which appears at start
                    if next_line.strip() == 'TABLE OF CONTENTS':
                        break
                    end_i += 1

                # Parse this function's documentation
                func_lines = lines[i+1:end_i]
                func_doc = self._parse_function(lib, func_name, func_lines)
                if func_doc:
                    key = f"{lib}/{func_name}"
                    self.functions[key] = func_doc
                    # Also store by just function name for easier lookup
                    self.functions[func_name] = func_doc

                i = end_i
            else:
                i += 1

    def _parse_function(self, library: str, name: str, lines: List[str]) -> Optional[FunctionDoc]:
        """Parse the documentation for a single function"""
        doc = FunctionDoc(library=library, name=name)

        current_section = None
        section_content: List[str] = []

        for line in lines:
            stripped = line.strip()

            # Check if this is a section header
            if stripped in self.SECTIONS:
                # Save previous section
                if current_section:
                    self._apply_section(doc, current_section, section_content)
                current_section = stripped
                section_content = []
            elif current_section:
                # Add to current section content
                section_content.append(line.rstrip())

        # Don't forget the last section
        if current_section:
            self._apply_section(doc, current_section, section_content)

        return doc

    def _apply_section(self, doc: FunctionDoc, section: str, lines: List[str]) -> None:
        """Apply parsed section content to the FunctionDoc"""
        # Clean up lines - remove leading/trailing empty lines
        while lines and not lines[0].strip():
            lines.pop(0)
        while lines and not lines[-1].strip():
            lines.pop()

        content = '\n'.join(lines)

        if section == 'NAME':
            # Extract just the summary (after the function name and dash)
            for line in lines:
                if ' -- ' in line:
                    doc.summary = line.split(' -- ', 1)[1].strip()
                    break
                elif ' - ' in line:
                    doc.summary = line.split(' - ', 1)[1].strip()
                    break

        elif section == 'SYNOPSIS':
            doc.synopsis = self._dedent(content)

        elif section == 'FUNCTION':
            doc.function_desc = self._dedent(content)

        elif section == 'INPUTS':
            doc.inputs = self._parse_inputs(lines)

        elif section in ('RESULT', 'RESULTS'):
            doc.result = self._dedent(content)

        elif section == 'WARNING':
            doc.warning = self._dedent(content)

        elif section in ('NOTE', 'NOTES'):
            doc.notes.append(self._dedent(content))

        elif section in ('EXAMPLE', 'EXAMPLES'):
            doc.examples.append(self._dedent(content))

        elif section == 'SEE ALSO':
            # Parse comma-separated function names
            see_also_text = ' '.join(line.strip() for line in lines)
            doc.see_also = [s.strip() for s in see_also_text.split(',') if s.strip()]

    def _parse_inputs(self, lines: List[str]) -> List[str]:
        """Parse the INPUTS section into a list of parameter descriptions"""
        inputs = []
        current_input = []

        for line in lines:
            stripped = line.strip()
            if not stripped:
                if current_input:
                    inputs.append(' '.join(current_input))
                    current_input = []
            elif line.startswith('\t') or line.startswith('        '):
                # Indented line - part of current input
                current_input.append(stripped)
            else:
                if current_input:
                    inputs.append(' '.join(current_input))
                current_input = [stripped]

        if current_input:
            inputs.append(' '.join(current_input))

        return inputs

    def _dedent(self, text: str) -> str:
        """Remove common leading whitespace from text"""
        lines = text.split('\n')
        if not lines:
            return text

        # Find minimum indentation (excluding empty lines)
        min_indent = float('inf')
        for line in lines:
            if line.strip():
                indent = len(line) - len(line.lstrip())
                min_indent = min(min_indent, indent)

        if min_indent == float('inf'):
            return text

        # Remove that much indentation from each line
        return '\n'.join(
            line[min_indent:] if len(line) > min_indent else line.strip()
            for line in lines
        )

    def get_function(self, name: str) -> Optional[FunctionDoc]:
        """Get documentation for a specific function"""
        return self.functions.get(name)

    def get_library_functions(self, library: str) -> Dict[str, FunctionDoc]:
        """Get all functions for a specific library"""
        result = {}
        for key, doc in self.functions.items():
            if '/' in key and key.startswith(library):
                result[doc.name] = doc
        return result


def main():
    parser = argparse.ArgumentParser(description='Parse NDK autodocs for Novus')
    parser.add_argument('autodocs_dir', help='Path to NDK Autodocs directory')
    parser.add_argument('--output-json', action='store_true', help='Output as JSON')
    parser.add_argument('--function', '-f', help='Get docs for specific function')
    parser.add_argument('--library', '-l', help='Get docs for specific library')
    parser.add_argument('--novus', action='store_true', help='Output as Novus doc comments')

    args = parser.parse_args()

    autodoc_parser = AutodocParser(args.autodocs_dir)
    functions = autodoc_parser.parse_all()

    print(f"Parsed {len(functions)} function definitions", file=sys.stderr)

    if args.function:
        doc = autodoc_parser.get_function(args.function)
        if doc:
            if args.novus:
                print(doc.to_novus_doc())
            elif args.output_json:
                print(json.dumps(asdict(doc), indent=2))
            else:
                print(f"Library: {doc.library}")
                print(f"Name: {doc.name}")
                print(f"Summary: {doc.summary}")
                print(f"Description: {doc.function_desc[:200]}...")
        else:
            print(f"Function '{args.function}' not found", file=sys.stderr)
            sys.exit(1)

    elif args.library:
        lib_funcs = autodoc_parser.get_library_functions(args.library)
        if args.output_json:
            print(json.dumps({k: asdict(v) for k, v in lib_funcs.items()}, indent=2))
        else:
            for name, doc in sorted(lib_funcs.items()):
                print(f"{name}: {doc.summary}")

    elif args.output_json:
        output = {k: asdict(v) for k, v in functions.items() if '/' in k}
        print(json.dumps(output, indent=2))

    else:
        # Print summary
        libraries = set()
        for key in functions.keys():
            if '/' in key:
                libraries.add(key.split('/')[0])

        print(f"\nLibraries found: {len(libraries)}")
        for lib in sorted(libraries):
            count = len([k for k in functions.keys() if k.startswith(f"{lib}/")])
            print(f"  {lib}: {count} functions")


if __name__ == '__main__':
    main()
