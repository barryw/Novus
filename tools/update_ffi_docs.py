#!/usr/bin/env python3
"""
Update Novus FFI files with NDK autodoc documentation.

This script reads Novus FFI files, finds extern fn declarations,
and adds documentation comments from the NDK autodocs.

Usage:
    python update_ffi_docs.py <autodocs_dir> <ffi_file> [--dry-run]

Example:
    python update_ffi_docs.py ~/amiga-cc/NDK3.9/Documentation/Autodocs Novus/std/ffi/exec.novus
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


class FFIUpdater:
    """Updates Novus FFI files with autodoc documentation"""

    # Pattern to match extern fn declarations (with optional pub)
    EXTERN_FN_PATTERN = re.compile(
        r'^(\s*)extern\s+(?:pub\s+)?fn\s+(\w+)\s*\(',
        re.MULTILINE
    )

    # Pattern to match existing doc comments before a declaration
    DOC_COMMENT_PATTERN = re.compile(r'^(\s*)///.*$', re.MULTILINE)

    def __init__(self, autodoc_parser: AutodocParser, force_update: bool = False):
        self.autodoc_parser = autodoc_parser
        self.force_update = force_update
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

                # Check if there's already a doc comment before this line
                has_existing_docs = self._has_doc_comment_before(new_lines)

                if has_existing_docs and not self.force_update:
                    self.stats['functions_already_documented'] += 1
                    new_lines.append(line)
                else:
                    # If force_update, remove existing doc comments
                    if has_existing_docs and self.force_update:
                        # Remove trailing doc comments from new_lines
                        while new_lines and new_lines[-1].strip().startswith('///'):
                            new_lines.pop()
                        # Also remove trailing blank lines after removing docs
                        while new_lines and not new_lines[-1].strip():
                            new_lines.pop()
                    # Try to find autodoc for this function
                    doc = self.autodoc_parser.get_function(func_name)

                    if doc:
                        # Generate Novus doc comments
                        novus_doc = self._generate_compact_doc(doc, indent)
                        if novus_doc:
                            new_lines.append(novus_doc)
                            self.stats['functions_documented'] += 1
                        else:
                            self.stats['functions_no_autodoc'] += 1
                    else:
                        self.stats['functions_no_autodoc'] += 1

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
        """Generate compact Novus doc comment (summary + key info only)"""
        lines = []

        # Summary line (required)
        if doc.summary:
            summary = doc.summary[0].upper() + doc.summary[1:] if doc.summary else ""
            lines.append(f"{indent}/// {summary}")
        else:
            return None

        # Add abbreviated function description (first 2 sentences max)
        if doc.function_desc:
            desc = doc.function_desc.strip()
            # Get first two sentences
            sentences = re.split(r'(?<=[.!?])\s+', desc)
            short_desc = ' '.join(sentences[:2])
            if short_desc and short_desc != doc.summary:
                lines.append(f"{indent}///")
                # Wrap at ~80 chars
                words = short_desc.split()
                current_line = f"{indent}///"
                for word in words:
                    if len(current_line) + len(word) + 1 > 80:
                        lines.append(current_line)
                        current_line = f"{indent}/// {word}"
                    else:
                        current_line += f" {word}"
                if current_line.strip() != f"{indent}///".strip():
                    lines.append(current_line)

        # Add inputs summary if present
        if doc.inputs and len(doc.inputs) > 0:
            lines.append(f"{indent}///")
            lines.append(f"{indent}/// # Arguments")
            for inp in doc.inputs[:8]:  # Max 8 params
                inp = inp.strip()
                if ' - ' in inp:
                    param, desc = inp.split(' - ', 1)
                    param = param.strip()
                    desc = desc.strip()
                    # Full description, wrapped at 76 chars for readability
                    first_line = f"{indent}/// * `{param}` - "
                    remaining_width = 76 - len(first_line)
                    if len(desc) <= remaining_width:
                        lines.append(f"{first_line}{desc}")
                    else:
                        # Wrap to multiple lines
                        words = desc.split()
                        current_line = first_line
                        continuation_indent = f"{indent}///   "
                        for word in words:
                            if len(current_line) + len(word) + 1 <= 76:
                                current_line += word + " "
                            else:
                                lines.append(current_line.rstrip())
                                current_line = continuation_indent + word + " "
                        if current_line.strip() != continuation_indent.strip():
                            lines.append(current_line.rstrip())

        # Add return info if present
        if doc.result:
            # Get first paragraph (up to blank line)
            result_lines = doc.result.strip().split('\n')
            result_paragraphs = []
            current_para = []
            for line in result_lines:
                if line.strip():
                    current_para.append(line.strip())
                elif current_para:
                    result_paragraphs.append(' '.join(current_para))
                    current_para = []
            if current_para:
                result_paragraphs.append(' '.join(current_para))

            if result_paragraphs:
                lines.append(f"{indent}///")
                lines.append(f"{indent}/// # Returns")
                # Use first paragraph, wrapped
                result_text = result_paragraphs[0]
                words = result_text.split()
                current_line = f"{indent}/// "
                for word in words:
                    if len(current_line) + len(word) + 1 <= 76:
                        current_line += word + " "
                    else:
                        lines.append(current_line.rstrip())
                        current_line = f"{indent}/// " + word + " "
                if current_line.strip() != f"{indent}///".strip():
                    lines.append(current_line.rstrip())

        # Add warning if present (abbreviated)
        if doc.warning:
            warning = doc.warning.strip().split('\n')[0]
            if warning:
                lines.append(f"{indent}///")
                lines.append(f"{indent}/// # Warning")
                if len(warning) > 70:
                    warning = warning[:67] + "..."
                lines.append(f"{indent}/// {warning}")

        # Add see also
        if doc.see_also:
            see_also = ', '.join(doc.see_also[:5])  # Max 5 references
            lines.append(f"{indent}///")
            lines.append(f"{indent}/// See also: {see_also}")

        return '\n'.join(lines) if lines else None


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

    print(f"Updating {ffi_path}...", file=sys.stderr)
    updater = FFIUpdater(autodoc_parser, force_update=args.force)
    result = updater.update_file(ffi_path, dry_run=args.dry_run)

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
