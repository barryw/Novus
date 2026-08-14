#!/usr/bin/env python3
"""Extract Novus public API docs to stable JSON and static HTML/CSS."""

import argparse
import html
import json
import os
import re
import sys
import tomllib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from autodoc_parser import AutodocParser

DECL = re.compile(r"^\s*(?:extern\s+)?pub\s+(fn|struct|union|enum|trait|class|type|const)\s+([A-Za-z_]\w*)")
FN = re.compile(r"\bfn\s+[A-Za-z_]\w*(?:<[^>]*>)?\s*\(")


def module_name(root: Path, path: Path) -> str:
    module = "::".join(path.relative_to(root).with_suffix("").parts).replace("::mod", "")
    return f"amiga::raw::{module}" if root.name == "raw" and root.parent.name == "amiga" else module


def configured_ndk() -> Path | None:
    if os.environ.get("NOVUS_NDK_PATH"):
        return Path(os.environ["NOVUS_NDK_PATH"])
    config = Path.home() / ".novus" / "config.toml"
    if config.exists():
        value = tomllib.loads(config.read_text()).get("ndk_path")
        if value:
            return Path(value)
    return None


def sections(markdown: str) -> dict[str, str]:
    result: dict[str, list[str]] = {"description": []}
    current = "description"
    for line in markdown.splitlines():
        heading = re.match(r"^#\s+(.+)$", line)
        if heading:
            current = heading.group(1).strip().lower().replace(" ", "_")
            result.setdefault(current, [])
        else:
            result[current].append(line)
    return {key: "\n".join(value).strip() for key, value in result.items() if any(line.strip() for line in value)}


def matching_paren(value: str, start: int) -> int:
    depth = 0
    for index in range(start, len(value)):
        if value[index] == "(":
            depth += 1
        elif value[index] == ")":
            depth -= 1
            if depth == 0:
                return index
    return -1


def split_top_level(value: str) -> list[str]:
    values, current, depth = [], [], 0
    for char in value:
        if char in "<([":
            depth += 1
        elif char in ">)]":
            depth -= 1
        if char == "," and depth == 0:
            values.append("".join(current).strip())
            current = []
        else:
            current.append(char)
    if current or value:
        values.append("".join(current).strip())
    return [item for item in values if item and item != ".."]


def documented_parameter(name: str, parsed: dict[str, str]) -> str:
    text = "\n".join(parsed.get(key, "") for key in ("parameters", "arguments", "inputs"))
    match = re.search(rf"(?im)^\s*(?:[*-]\s*)?`?{re.escape(name)}`?\s*(?:[-:—]\s*)?(.+)$", text)
    if match:
        return match.group(1).strip()
    human = name.replace("_", " ")
    return f"The `{name}` value used as {human}."


def function_shape(signature: str, markdown: str) -> tuple[list[dict], dict | None]:
    match = FN.search(signature)
    if not match:
        return [], None
    start = signature.find("(", match.start())
    end = matching_paren(signature, start)
    if end < 0:
        return [], None
    parsed = sections(markdown)
    parameters = []
    for value in split_top_level(signature[start + 1:end]):
        left, separator, type_name = value.partition(":")
        names = re.findall(r"[A-Za-z_]\w*", left)
        if not names:
            continue
        name = names[-1]
        receiver = name == "self"
        parameters.append({
            "name": name,
            "type": type_name.strip() if separator else "Self",
            "modifiers": left[:left.rfind(name)].strip(),
            "receiver": receiver,
            "documentation": "The method receiver." if receiver else documented_parameter(name, parsed),
        })
    tail = signature[end + 1:]
    result = re.search(r"->\s*(.+?)(?:\s+where\b|\s*\{|$)", tail)
    if not result:
        return parameters, None
    type_name = result.group(1).strip()
    documentation = parsed.get("returns") or parsed.get("result")
    if not documentation:
        if type_name.startswith("Result<"):
            documentation = "The value on success, or the declared error when the operation cannot complete."
        elif type_name.startswith("Option<"):
            documentation = "The requested value, or `Option::None` when it is unavailable."
        elif type_name == "bool":
            documentation = "Whether the documented condition holds."
        elif type_name.startswith("&var "):
            documentation = "A mutable borrow tied to the receiver or input lifetime."
        elif type_name.startswith("&"):
            documentation = "A shared borrow tied to the receiver or input lifetime."
        else:
            documentation = f"The resulting `{type_name}` value."
    return parameters, {"type": type_name, "documentation": documentation}


def type_members(block: list[str], raw_source: bool) -> list[dict]:
    members = []
    docs: list[str] = []
    for line in block[1:-1]:
        stripped = line.strip().rstrip(",")
        if stripped.startswith("///"):
            docs.append(stripped[3:].lstrip())
            continue
        match = re.match(r"(?:pub\s+)?fn\s+([A-Za-z_]\w*)|([A-Za-z_]\w*)\s*(?::|\(|$)", stripped)
        if not match:
            if stripped:
                docs.clear()
            continue
        name = match.group(1) or match.group(2)
        documentation = "\n".join(docs).strip()
        if not documentation and raw_source:
            documentation = f"Raw ABI member `{name}`; its exact type and representation are shown in the declaration."
        parameters, returns = function_shape(stripped, documentation)
        members.append({"name": name, "signature": stripped, "documentation": documentation,
                        "parameters": parameters, "returns": returns})
        docs.clear()
    return members


def implementation_owners(lines: list[str]) -> dict[int, str]:
    # ponytail: lexical brace tracking; use the compiler AST if docs ever need macro-expanded ownership.
    owners: dict[int, str] = {}
    stack: list[tuple[str, int]] = []
    depth = 0
    for index, line in enumerate(lines):
        match = re.match(r"^\s*impl(?:\s*<[^>]+>)?\s+([A-Za-z_]\w*)", line)
        if match and "{" in line:
            stack.append((match.group(1), depth))
        if stack:
            owners[index] = stack[-1][0]
        depth += line.count("{") - line.count("}")
        while stack and depth <= stack[-1][1]:
            stack.pop()
    return owners


def sfd_aliases(ndk_path: Path) -> dict[tuple[str, str], str]:
    aliases = {}
    for path in sorted((ndk_path / "Include" / "sfd").glob("*_lib.sfd")):
        library = ""
        previous = ""
        alias = False
        for raw in path.read_text(encoding="latin-1").splitlines():
            line = raw.strip()
            if line.startswith("==libname "):
                library = line.split(None, 1)[1]
            elif line in {"==alias", "==varargs"}:
                alias = True
            elif line and not line.startswith(("==", "*")):
                match = re.match(r".+?\s+([A-Za-z_]\w*)\s*\(", line)
                if match:
                    name = match.group(1)
                    if alias and previous:
                        aliases[(library, name)] = previous
                    else:
                        previous = name
                    alias = False
    return aliases


def coverage_metadata(root: Path) -> tuple[dict[tuple[str, str, str], list[dict]], dict | None]:
    path = root / "ndk_coverage.json"
    if not path.exists():
        return {}, None
    manifest = json.loads(path.read_text())
    result: dict[tuple[str, str, str], list[dict]] = {}
    for symbol in manifest.get("symbols", []):
        result.setdefault((symbol["category"], symbol["name"], symbol.get("novus_module", "")), []).append({
            key: symbol.get(key) for key in
            ("status", "scope", "interface", "sources", "minimum_version", "definition", "notes")
        })
    for symbol in manifest.get("extension_symbols", []):
        result.setdefault((symbol["category"], symbol["name"], symbol["module"]), []).append({
            "status": "NOVUS_EXTENSION", "scope": symbol.get("scope"), "interface": "Novus compatibility",
            "sources": [], "minimum_version": 0, "definition": "", "notes": symbol.get("notes")
        })
    return result, manifest.get("baseline")


def extract(root: Path, autodocs: AutodocParser | None, aliases: dict[tuple[str, str], str],
            coverage: dict[tuple[str, str, str], list[dict]] | None = None) -> list[dict]:
    coverage = coverage or {}
    symbols = []
    for path in sorted(root.rglob("*.novus")):
        if "tests" in path.relative_to(root).parts:
            continue
        raw_source = root.name == "raw" or "/amiga/raw/" in path.as_posix()
        lines = path.read_text().splitlines()
        owners = implementation_owners(lines)
        pending_docs: list[str] = []
        annotations: list[str] = []
        i = 0
        while i < len(lines):
            stripped = lines[i].strip()
            if stripped.startswith("///"):
                pending_docs.append(stripped[3:].lstrip())
                i += 1
                continue
            if stripped.startswith("@") or stripped.startswith("#["):
                annotations.append(stripped)
                i += 1
                continue
            match = DECL.match(lines[i])
            if not match:
                if stripped:
                    pending_docs.clear()
                    annotations.clear()
                i += 1
                continue

            kind, name = match.groups()
            declaration_line = i + 1
            if name.startswith("__novus_"):
                pending_docs.clear()
                annotations.clear()
                i += 1
                continue
            signature = stripped
            while signature.count("(") > signature.count(")") and i + 1 < len(lines):
                i += 1
                signature += " " + lines[i].strip()
            block = [signature]
            if kind in {"struct", "union", "enum", "trait", "class"} and "{" in signature:
                depth = signature.count("{") - signature.count("}")
                while depth > 0 and i + 1 < len(lines):
                    i += 1
                    block.append(lines[i])
                    depth += lines[i].count("{") - lines[i].count("}")
                signature = "\n".join(block)
            markdown = "\n".join(pending_docs).strip()
            library = ""
            for annotation in annotations:
                found = re.match(r'@library\("([^"]+)"\)', annotation)
                if found:
                    library = found.group(1)
            if raw_source and path.stem == "amiga_lib":
                library = "amiga.lib"
            if autodocs and raw_source and kind == "fn":
                candidates = [name, name + "A"]
                if name.endswith("A"):
                    candidates.append(name[:-1])
                if (library, name) in aliases:
                    candidates.insert(0, aliases[(library, name)])
                if name.endswith("Tags"):
                    candidates.extend([name[:-4] + "A", name[:-4]])
                if "Attrs" in name:
                    candidates.append(name.replace("Attrs", "Attr"))
                if name.startswith("Is") and name not in {"IsXXXX"}:
                    candidates.append("IsXXXX")
                special = {"UCopperListInit": "CINIT"}.get(name)
                if special:
                    candidates.append(special)
                candidates.extend(candidate.replace("Attrs", "Attr") for candidate in list(candidates) if "Attrs" in candidate)
                doc = next((autodocs.get_function(candidate, library) for candidate in candidates
                            if library and autodocs.get_function(candidate, library)), None)
                doc = doc or next((autodocs.get_unique_function(candidate) for candidate in candidates
                                   if autodocs.get_unique_function(candidate)), None)
                if doc:
                    markdown = "\n".join(line[4:] if line.startswith("/// ") else "" for line in doc.to_novus_doc().splitlines()).strip()
            if not markdown and raw_source and kind in {"struct", "union", "enum", "type"}:
                markdown = f"Raw ABI definition for `{name}`. Its declaration below is the machine-verifiable layout used by Novus."
            category = "function" if kind == "fn" else "constant" if kind == "const" else "type"
            module = module_name(root, path)
            ndk = coverage.get((category, name, module), [])
            if not markdown and raw_source and kind == "const":
                source = ", ".join(dict.fromkeys(item for entry in ndk for item in (entry.get("sources") or [])))
                markdown = f"Raw constant `{name}` with the exact value shown in its declaration."
                if source:
                    markdown += f" Authoritative NDK source: {source}."
            parsed_sections = sections(markdown)
            description = parsed_sections.get("description", "")
            owner = owners.get(declaration_line - 1, "") if kind == "fn" else ""
            parameters, returns = function_shape(signature, markdown) if kind == "fn" else ([], None)
            symbols.append({
                "module": module,
                "owner": owner,
                "name": name,
                "qualified_name": "::".join(value for value in (module, owner, name) if value),
                "kind": kind,
                "signature": signature,
                "summary": next((line for line in description.splitlines() if line.strip()), ""),
                "documentation": markdown,
                "sections": parsed_sections,
                "parameters": parameters,
                "returns": returns,
                "members": type_members(block, raw_source),
                "ndk": ndk,
                "source": path.relative_to(root).as_posix(),
                "line": declaration_line,
            })
            pending_docs.clear()
            annotations.clear()
            i += 1
    return symbols


def render_markdown(value: str) -> str:
    output = []
    in_list = False
    for raw in value.splitlines():
        line = raw.strip()
        if line.startswith("# "):
            if in_list:
                output.append("</ul>")
                in_list = False
            output.append(f"<h3>{html.escape(line[2:])}</h3>")
        elif line.startswith("* "):
            if not in_list:
                output.append("<ul>")
                in_list = True
            output.append(f"<li>{html.escape(line[2:])}</li>")
        elif line:
            if in_list:
                output.append("</ul>")
                in_list = False
            output.append(f"<p>{html.escape(line)}</p>")
    if in_list:
        output.append("</ul>")
    return "".join(output)


def render_ndk(entries: list[dict]) -> str:
    if not entries:
        return ""
    values = lambda key: ", ".join(dict.fromkeys(str(value) for entry in entries
        for value in (entry.get(key) if isinstance(entry.get(key), list) else [entry.get(key)]) if value))
    rows = [("Status", values("status")), ("Scope", values("scope")),
            ("Minimum version", values("minimum_version")), ("NDK source", values("sources"))]
    return '<dl class="metadata">' + "".join(
        f"<dt>{html.escape(label)}</dt><dd>{html.escape(value)}</dd>" for label, value in rows if value and value != "0") + "</dl>"


def render_function_shape(parameters: list[dict], returns: dict | None) -> str:
    output = []
    visible = [parameter for parameter in parameters if not parameter["receiver"]]
    if visible:
        output.append("<h4>Parameters</h4><dl>")
        for parameter in visible:
            modifiers = (f' <span class="modifiers">{html.escape(parameter["modifiers"])}</span>'
                         if parameter["modifiers"] else "")
            output.append(f'<dt><code>{html.escape(parameter["name"])}: {html.escape(parameter["type"])}</code>{modifiers}</dt>'
                          f'<dd>{render_markdown(parameter["documentation"])}</dd>')
        output.append("</dl>")
    if returns:
        output.append(f'<h4>Returns</h4><dl><dt><code>{html.escape(returns["type"])}</code></dt>'
                      f'<dd>{render_markdown(returns["documentation"])}</dd></dl>')
    return "".join(output)


def write_site(output: Path, root: Path, symbols: list[dict], baseline: dict | None = None) -> None:
    output.mkdir(parents=True, exist_ok=True)
    payload = {"schema_version": 4, "source_root": str(root.as_posix()), "baseline": baseline, "symbols": symbols}
    (output / "api.json").write_text(json.dumps(payload, indent=2) + "\n")
    modules: dict[str, list[dict]] = {}
    for symbol in symbols:
        modules.setdefault(symbol["module"], []).append(symbol)
    nav = "".join(f'<li><a href="#{html.escape(module)}">{html.escape(module)}</a></li>' for module in modules)
    body = []
    for module, entries in modules.items():
        body.append(f'<section><h2 id="{html.escape(module)}">{html.escape(module)}</h2>')
        for entry in entries:
            display_name = "::".join(value for value in (entry["owner"], entry["name"]) if value)
            body.append(f'<article><div class="kind">{entry["kind"]}</div><h3>{html.escape(display_name)}</h3>')
            body.append(f'<pre><code>{html.escape(entry["signature"])}</code></pre>')
            body.append(render_ndk(entry["ndk"]))
            body.append(render_markdown(entry["documentation"]))
            body.append(render_function_shape(entry["parameters"], entry["returns"]))
            if entry["members"]:
                body.append('<h4>Members</h4><dl>')
                for member in entry["members"]:
                    body.append(f'<dt><code>{html.escape(member["signature"])}</code></dt><dd>{render_markdown(member["documentation"])}</dd>')
                    body.append(render_function_shape(member["parameters"], member["returns"]))
                body.append('</dl>')
            body.append(f'<div class="source">{html.escape(entry["source"])}:{entry["line"]}</div></article>')
        body.append("</section>")
    page = f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Novus API</title><link rel="stylesheet" href="api.css"></head><body><aside><h1>Novus API</h1><input id="search" type="search" placeholder="Search APIs" aria-label="Search APIs"><ul>{nav}</ul></aside><main>{''.join(body)}</main><script>const search=document.getElementById('search');search.oninput=()=>{{let q=search.value.toLowerCase();document.querySelectorAll('article').forEach(x=>x.hidden=!x.textContent.toLowerCase().includes(q))}}</script></body></html>'''
    (output / "index.html").write_text(page)
    (output / "api.css").write_text(""":root{color-scheme:dark;font:16px system-ui;background:#0b1020;color:#e7eaf0}body{margin:0;display:grid;grid-template-columns:18rem 1fr}aside{position:sticky;top:0;height:100vh;overflow:auto;padding:1.5rem;background:#121a30}aside ul{padding:0;list-style:none}a{color:#72d8ff}input{box-sizing:border-box;width:100%;padding:.65rem}main{max-width:72rem;padding:2rem 3rem}section{margin-bottom:4rem}article{position:relative;margin:1rem 0;padding:1.25rem;border:1px solid #2c3754;border-radius:.6rem;background:#11182a}article h3{margin:.2rem 0 1rem}.kind{float:right;color:#a6b1cc;text-transform:uppercase;font-size:.75rem}pre{overflow:auto;padding:1rem;background:#080c17;border-radius:.35rem}.metadata{display:grid;grid-template-columns:max-content 1fr;gap:.25rem 1rem;font-size:.85rem}.metadata dt{font-weight:700}.metadata dd{margin:0}.source{font-size:.8rem}@media(max-width:800px){body{display:block}aside{position:static;height:auto}main{padding:1.25rem}}""")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("--output", type=Path, default=Path("website/public/api"))
    parser.add_argument("--ndk-path", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    args.ndk_path = args.ndk_path or configured_ndk()
    autodocs = None
    if args.ndk_path:
        autodocs = AutodocParser(args.ndk_path / "Documentation" / "Autodocs")
        autodocs.parse_all()
    aliases = sfd_aliases(args.ndk_path) if args.ndk_path else {}
    coverage, baseline = coverage_metadata(args.source)
    symbols = extract(args.source, autodocs, aliases, coverage)
    missing = [symbol for symbol in symbols if not symbol["summary"]]
    missing_members = [(symbol, member) for symbol in symbols for member in symbol["members"] if not member["documentation"]]
    if args.check and (missing or missing_members):
        for symbol in missing[:200]:
            print(f'{symbol["source"]}:{symbol["line"]}: undocumented public {symbol["kind"]} {symbol["name"]}', file=sys.stderr)
        if len(missing) > 200:
            print(f"... and {len(missing) - 200} more", file=sys.stderr)
        for symbol, member in missing_members[:200 - min(200, len(missing))]:
            print(f'{symbol["source"]}:{symbol["line"]}: undocumented {symbol["kind"]} member {symbol["name"]}::{member["name"]}', file=sys.stderr)
        print(f"documentation check failed: {len(missing)} public symbols and {len(missing_members)} public members undocumented", file=sys.stderr)
        return 1
    write_site(args.output, args.source, symbols, baseline)
    print(f"wrote {len(symbols)} symbols to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
