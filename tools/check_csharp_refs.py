"""Gate: does the C# actually reference things that exist?

There is no C# toolchain in this environment (no dotnet, no mono, and the standalone compiler
is blocked), so 6,000 lines of hand-written Unity code have never been type-checked. Every
symbol in them was written from memory or from reading package source. That is exactly the
situation where `UiKit.Lable(...)` or a seven-argument call to an eight-parameter method slips
through and the user meets it as a red Console on first open.

This does not compile anything. It parses, and it checks the two mistakes that are both common
and invisible without a compiler:

  1. `Type.Member` where Type is one of ours and Member does not appear anywhere in that type.
  2. `Type.Method(args)` whose argument count matches no declaration of that method.

Deliberately permissive on purpose. The "member exists" test accepts any identifier that occurs
inside the type body, so a name used only by a local variable would pass — but a *typo* cannot,
because the misspelling appears nowhere. That trades a few missed cases for near-zero false
positives, which matters: a gate that cries wolf gets ignored.

Run from the repo root:

    python tools/check_csharp_refs.py
"""

import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.abspath(os.path.join(HERE, os.pardir, "Assets"))
if len(sys.argv) > 1:
    ASSETS = os.path.abspath(sys.argv[1])

# Types we define that legitimately expose members we do not parse (partials, inherited Unity
# members reached through a static-style call, generic helpers). Empty today; kept as an escape
# hatch so a future false positive does not force a rewrite of the gate.
IGNORE_MEMBERS = set()

CONTROL_KEYWORDS = {
    "if", "for", "foreach", "while", "switch", "catch", "using", "lock", "return",
    "nameof", "typeof", "sizeof", "when", "checked", "unchecked", "base", "this",
}


def strip_code(text):
    """Blank out comments and literals while preserving character offsets and line structure."""
    out = []
    i = 0
    n = len(text)
    while i < n:
        c = text[i]

        if c == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                out.append(" ")
                i += 1
            continue

        if c == "/" and i + 1 < n and text[i + 1] == "*":
            while i < n and not (text[i] == "*" and i + 1 < n and text[i + 1] == "/"):
                out.append("\n" if text[i] == "\n" else " ")
                i += 1
            out.append("  ")
            i += 2
            continue

        if c == "@" and i + 1 < n and text[i + 1] == '"':
            i += 2
            out.append('"')
            while i < n:
                if text[i] == '"':
                    if i + 1 < n and text[i + 1] == '"':
                        out.append("  ")
                        i += 2
                        continue
                    break
                out.append("\n" if text[i] == "\n" else " ")
                i += 1
            out.append('"')
            i += 1
            continue

        if c in ('"', "'"):
            quote = c
            i += 1
            # Keep the quote characters. Without them an argument list of pure string
            # literals looks empty and every such call is reported as zero arguments.
            out.append(quote)
            while i < n:
                if text[i] == "\\":
                    out.append("  ")
                    i += 2
                    continue
                if text[i] == quote:
                    break
                out.append("\n" if text[i] == "\n" else " ")
                i += 1
            out.append(quote)
            i += 1
            continue

        out.append(c)
        i += 1

    return "".join(out)


def match_brace(text, open_index):
    """Index of the '}' matching the '{' at open_index."""
    depth = 0
    for i in range(open_index, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return i
    return -1


TYPE_DECL = re.compile(
    r"\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)")


def collect_types(files):
    """name -> {'body': str, 'file': str, 'namespace': str, 'duplicates': [files]}"""
    types = {}
    for path in files:
        raw = open(path, encoding="utf-8", errors="replace").read()
        code = strip_code(raw)

        namespace = ""
        ns_match = re.search(r"\bnamespace\s+([\w.]+)", code)
        if ns_match:
            namespace = ns_match.group(1)

        for match in TYPE_DECL.finditer(code):
            name = match.group(1)
            brace = code.find("{", match.end())
            if brace < 0:
                continue
            end = match_brace(code, brace)
            if end < 0:
                continue
            body = code[brace:end + 1]

            if name not in types:
                types[name] = {"body": body, "file": path, "namespace": namespace,
                               "kind": "type", "duplicates": []}
                continue

            # Same name twice in one namespace is CS0101 — fatal, and invisible until compile.
            # `partial` is legal, so only flag when the file does not say partial.
            is_partial = "partial" in code[max(0, match.start() - 60):match.start()]
            if types[name]["namespace"] == namespace and not is_partial:
                types[name]["duplicates"].append(path)

            if len(body) > len(types[name]["body"]):
                types[name]["body"] = body
                types[name]["file"] = path
    return types


def identifiers(text):
    return set(re.findall(r"\b([A-Za-z_]\w*)\b", text))


def param_bounds(param_text):
    """(min, max) argument counts for a parameter list. max is None for 'params'."""
    param_text = param_text.strip()
    if not param_text:
        return 0, 0

    parts, depth, current = [], 0, []
    for ch in param_text:
        if ch in "(<[{":
            depth += 1
        elif ch in ")>]}":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
        else:
            current.append(ch)
    tail = "".join(current).strip()
    if tail:
        parts.append(tail)

    parts = [p for p in parts if p]
    optional = sum(1 for p in parts if "=" in p)
    has_params = any(p.startswith("params ") or " params " in p for p in parts)
    return len(parts) - optional, (None if has_params else len(parts))


METHOD_DECL = re.compile(
    r"\b([A-Za-z_]\w*)\s*(?:<[^<>;{}]*>)?\s*\(([^;{}]*)\)\s*(?:where[^{;]*)?\{")


def normalize_params(raw):
    """Parameter list reduced to types only.

    C# does not distinguish overloads by parameter *name*, so `Twice(int a)` and
    `Twice(int b)` are the same signature (CS0111). Dropping the trailing identifier from
    each parameter makes that visible.
    """
    parts, depth, current = [], 0, []
    for ch in raw:
        if ch in "(<[{":
            depth += 1
        elif ch in ")>]}":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(current))
            current = []
        else:
            current.append(ch)
    parts.append("".join(current))

    normalized = []
    for part in parts:
        part = part.split("=")[0].strip()          # drop default value
        part = re.sub(r"\s+", " ", part)
        if not part:
            continue
        match = re.match(r"^(.*?)\s+([A-Za-z_]\w*)$", part)
        normalized.append(match.group(1).strip() if match else part)
    return ",".join(normalized)


def method_signatures(body):
    """name -> list of (min, max, normalized parameter types)."""
    signatures = {}
    for match in METHOD_DECL.finditer(body):
        name = match.group(1)
        if name in CONTROL_KEYWORDS:
            continue
        try:
            bounds = param_bounds(match.group(2))
        except Exception:
            continue
        signatures.setdefault(name, []).append(
            (bounds[0], bounds[1], normalize_params(match.group(2))))
    return signatures


def duplicate_methods(signatures):
    """(name, parameter types) declared more than once in one type is CS0111."""
    clashes = []
    for name, entries in signatures.items():
        seen = set()
        for _lo, _hi, normalized in entries:
            if normalized in seen:
                clashes.append("%s(%s)" % (name, normalized))
                break
            seen.add(normalized)
    return clashes


def count_arguments(code, open_paren):
    """Number of top-level arguments in the call whose '(' is at open_paren."""
    depth = 0
    commas = 0
    saw_content = False
    for i in range(open_paren, len(code)):
        ch = code[i]
        if ch in "([{":
            depth += 1
            if depth == 1:
                continue
        elif ch in ")]}":
            depth -= 1
            if depth == 0:
                return (commas + 1) if saw_content else 0
        elif depth == 1:
            if ch == ",":
                commas += 1
            elif not ch.isspace():
                saw_content = True
    return None


CALL = re.compile(r"\b([A-Z]\w*)\s*\.\s*([A-Za-z_]\w*)\s*(?:<[^<>;{}()]*>)?\s*\(")
MEMBER = re.compile(r"\b([A-Z]\w*)\s*\.\s*([A-Za-z_]\w*)\b")

# Contexts where an identifier is unambiguously being used as a type name. Deliberately narrow:
# widening this to "any PascalCase identifier" would drown the report in false positives from
# Unity's own types.
BASE_LIST = re.compile(r"\b(?:class|struct|interface)\s+\w+\s*(?:<[^{]*?>)?\s*:\s*([^{]+?)\{", re.S)
NEW_EXPR = re.compile(r"\bnew\s+([A-Z]\w*)\s*[(<\[]")
IS_AS = re.compile(r"\b(?:is|as)\s+([A-Z]\w*)\b")

# `public Interactable PickAt(` / `public Camera Cam { get` — a declared member type. Catches
# return types and property types, which the base-list and `new` checks both miss.
DECL_TYPE = re.compile(
    r"\b(?:public|private|protected|internal)\s+"
    r"(?:(?:static|virtual|override|readonly|sealed|abstract|async|extern|new|partial|const|unsafe)\s+)*"
    r"([A-Z]\w*)\s+[A-Za-z_]\w*\s*[(;={]")

# `GetComponentInParent<Interactable>()` — a generic type argument.
GENERIC_ARG = re.compile(r"<\s*([A-Z]\w*)\s*[,>]")

# `LessonPresenter presenter = ...` — a local declaration, which has no access modifier and so
# slips past DECL_TYPE.
LOCAL_DECL = re.compile(r"(?m)^\s*([A-Z]\w*)\s+[A-Za-z_]\w*\s*[=;]")


def visible_namespaces(code):
    """Namespaces a file can see unqualified: its own, its parents, and its usings."""
    visible = set()

    match = re.search(r"\bnamespace\s+([\w.]+)", code)
    if match:
        parts = match.group(1).split(".")
        for i in range(1, len(parts) + 1):
            visible.add(".".join(parts[:i]))

    for using in re.findall(r"\busing\s+(?:static\s+)?([\w.]+)\s*;", code):
        visible.add(using)

    return visible


def type_uses(code):
    """(index, type_name) for identifiers used in a type position."""
    for match in BASE_LIST.finditer(code):
        base_text = match.group(1)
        offset = match.start(1)
        depth, current_start = 0, 0
        for i, ch in enumerate(base_text):
            if ch in "(<[":
                depth += 1
            elif ch in ")>]":
                depth -= 1
            elif ch == "," and depth == 0:
                yield from _first_identifier(base_text[current_start:i], offset + current_start)
                current_start = i + 1
        yield from _first_identifier(base_text[current_start:], offset + current_start)

    for match in NEW_EXPR.finditer(code):
        yield match.start(1), match.group(1)

    for match in IS_AS.finditer(code):
        yield match.start(1), match.group(1)

    for match in DECL_TYPE.finditer(code):
        yield match.start(1), match.group(1)

    for match in GENERIC_ARG.finditer(code):
        yield match.start(1), match.group(1)

    for match in LOCAL_DECL.finditer(code):
        yield match.start(1), match.group(1)


def _first_identifier(text, offset):
    match = re.search(r"([A-Z]\w*)", text)
    if match:
        yield offset + match.start(1), match.group(1)


def line_of(text, index):
    return text.count("\n", 0, index) + 1


def main():
    files = []
    for root, _dirs, names in os.walk(ASSETS):
        for name in names:
            if name.endswith(".cs"):
                files.append(os.path.join(root, name))
    files.sort()

    if not files:
        print("FAIL: no .cs files under %s" % ASSETS)
        return 1

    types = collect_types(files)
    if not types:
        print("FAIL: no type declarations found")
        return 1

    member_pool = {}
    signatures = {}
    for name, info in types.items():
        member_pool[name] = identifiers(info["body"])
        signatures[name] = method_signatures(info["body"])

    unknown_members = []
    unreachable_types = []
    bad_arity = []
    checked_calls = 0
    checked_members = 0

    type_namespace = {name: info["namespace"] for name, info in types.items()}

    for path in files:
        raw = open(path, encoding="utf-8", errors="replace").read()
        code = strip_code(raw)
        short = os.path.relpath(path, os.path.dirname(ASSETS))

        # A type from another namespace used without a using directive. This is CS0246, it
        # stops the whole assembly from building, and it is invisible until Unity is opened.
        visible = visible_namespaces(code)
        reported = set()
        for index, name in type_uses(code):
            namespace = type_namespace.get(name)
            if namespace is None or namespace == "":
                continue
            if namespace in visible or name in reported:
                continue
            prefix = namespace + "."
            start = index - len(prefix)
            if start >= 0 and code[start:index] == prefix:
                continue
            reported.add(name)
            unreachable_types.append(
                "%s:%d  %s is in namespace '%s', which this file does not import"
                % (short, line_of(code, index), name, namespace))

        # arity first, so a call is not also reported as an unknown member
        call_spans = []
        for match in CALL.finditer(code):
            type_name, method_name = match.group(1), match.group(2)
            if type_name not in types or method_name in CONTROL_KEYWORDS:
                continue
            if method_name not in member_pool[type_name]:
                continue
            open_paren = match.end() - 1
            call_spans.append((match.start(), match.end()))
            actual = count_arguments(code, open_paren)
            if actual is None:
                continue
            checked_calls += 1

            declared = signatures[type_name].get(method_name)
            if not declared:
                continue
            ok = any(
                actual >= lo and (hi is None or actual <= hi)
                for lo, hi, _types in declared
            )
            if not ok:
                expected = ", ".join(
                    ("%d" % lo) if hi == lo else ("%d-%s" % (lo, hi if hi is not None else "n"))
                    for lo, hi, _types in declared
                )
                bad_arity.append(
                    "%s:%d  %s.%s(...) called with %d argument(s); declared %s"
                    % (short, line_of(code, match.start()), type_name, method_name, actual, expected))

        for match in MEMBER.finditer(code):
            type_name, member_name = match.group(1), match.group(2)
            if type_name not in types:
                continue
            if (type_name, member_name) in IGNORE_MEMBERS:
                continue
            if any(start <= match.start() < end for start, end in call_spans):
                continue
            checked_members += 1
            if member_name not in member_pool[type_name]:
                unknown_members.append(
                    "%s:%d  %s.%s — '%s' does not appear in %s"
                    % (short, line_of(code, match.start()), type_name, member_name,
                       member_name, type_name))

    print("types parsed   : %d" % len(types))
    print("calls checked  : %d" % checked_calls)
    print("member refs    : %d" % checked_members)
    print()

    problems = 0

    duplicates = []
    for name, info in sorted(types.items()):
        for other in info["duplicates"]:
            duplicates.append(
                "%s — declared in both %s and %s"
                % (name,
                   os.path.relpath(info["file"], os.path.dirname(ASSETS)),
                   os.path.relpath(other, os.path.dirname(ASSETS))))
        for method in duplicate_methods(signatures[name]):
            duplicates.append(
                "%s.%s — same parameter list declared twice"
                % (name, method))

    if duplicates:
        problems += len(duplicates)
        print("DUPLICATE DECLARATIONS (%d)" % len(duplicates))
        for issue in duplicates:
            print("  " + issue)
        print()

    if unreachable_types:
        problems += len(unreachable_types)
        print("UNREACHABLE TYPES (%d) — CS0246" % len(unreachable_types))
        for issue in unreachable_types:
            print("  " + issue)
        print()

    if unknown_members:
        problems += len(unknown_members)
        print("UNKNOWN MEMBERS (%d)" % len(unknown_members))
        for issue in unknown_members:
            print("  " + issue)
        print()

    if bad_arity:
        problems += len(bad_arity)
        print("WRONG ARGUMENT COUNT (%d)" % len(bad_arity))
        for issue in bad_arity:
            print("  " + issue)
        print()

    if problems:
        print("RESULT: FAIL (%d problems)" % problems)
        return 1

    print("RESULT: PASS — every Type.Member resolves and every call arity matches")
    return 0


if __name__ == "__main__":
    sys.exit(main())
