"""Gate: does the C# port still agree with the validated reference?

The QR encoder was validated in Python first (480 module-for-module comparisons against
the `qrcode` package, then 49 encode -> render -> ZXing round-trips). Porting it to C# is
where typos get in: a single wrong number in the Reed-Solomon block table produces a
symbol with perfect finder patterns and an unreadable payload.

There is no C# toolchain in this workspace, so this gate reads the C# source and diffs the
constants and the mask expressions against `qr_ref.py`. It cannot prove the C# executes
correctly, but it does prove the data it executes on is right, which is where the port
actually goes wrong.

Needs nothing but the standard library. Run from the repo root:

    python tools/check_qr_tables.py
"""

import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import qr_ref

CS_PATH = os.path.join(HERE, os.pardir, "Assets", "_Project", "Scripts", "Cert", "QrEncoder.cs")

failures = []


def fail(message):
    failures.append(message)
    print("FAIL " + message)


def ok(message):
    print("ok   " + message)


def block_after(source, declaration):
    """Text of an array initialiser: from the '=' after `declaration` to its closing '};'."""
    start = source.index(declaration)
    start = source.index("{", start)
    depth = 0
    for i in range(start, len(source)):
        if source[i] == "{":
            depth += 1
        elif source[i] == "}":
            depth -= 1
            if depth == 0:
                return source[start:i + 1]
    raise ValueError("unbalanced braces after " + declaration)


def entries(block):
    """Split a top-level initialiser into its comma-separated entries."""
    body = block[1:-1]
    parts, depth, current = [], 0, []
    for ch in body:
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
        else:
            current.append(ch)
    tail = "".join(current).strip()
    if tail:
        parts.append(tail)
    return [p for p in parts if p]


def strip_comments(text):
    return re.sub(r"//[^\n]*", "", text)


def main():
    with open(CS_PATH, encoding="utf-8") as handle:
        source = handle.read()

    # ---- 1. Reed-Solomon block table ----

    rs_block = block_after(source, "RsBlock[][] RsL")
    rs_entries = entries(strip_comments(rs_block))

    parsed_rs = []
    for entry in rs_entries:
        if entry.strip() == "null":
            parsed_rs.append(None)
            continue
        triples = re.findall(r"new\s+RsBlock\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", entry)
        parsed_rs.append([tuple(int(v) for v in t) for t in triples])

    if len(parsed_rs) != 13 or parsed_rs[0] is not None:
        fail("RsL should be 13 entries (null + versions 1-12), got %d" % len(parsed_rs))
    else:
        mismatched = 0
        for version in range(1, 13):
            expected = qr_ref.RS_L[version]
            got = parsed_rs[version]
            if got != expected:
                fail("RsL[%d] is %s, reference is %s" % (version, got, expected))
                mismatched += 1
        if mismatched == 0:
            ok("RsL: all 12 versions match the reference exactly")

    # ---- 2. Alignment pattern table ----

    align_block = block_after(source, "int[][] Align")
    align_entries = entries(strip_comments(align_block))

    parsed_align = []
    for entry in align_entries:
        if entry.strip() == "null":
            parsed_align.append(None)
            continue
        if re.fullmatch(r"new\s+int\s*\[\s*0\s*\]", entry.strip()):
            parsed_align.append([])
            continue
        numbers = re.findall(r"\d+", entry)
        parsed_align.append([int(n) for n in numbers])

    if len(parsed_align) != 13 or parsed_align[0] is not None:
        fail("Align should be 13 entries (null + versions 1-12), got %d" % len(parsed_align))
    else:
        mismatched = 0
        for version in range(1, 13):
            expected = qr_ref.ALIGN[version]
            got = parsed_align[version]
            if got != expected:
                fail("Align[%d] is %s, reference is %s" % (version, got, expected))
                mismatched += 1
        if mismatched == 0:
            ok("Align: all 12 versions match the reference exactly")

    # ---- 3. derived capacities and version selection, computed from the C# table ----

    def cs_capacity(version):
        return sum(count * data for count, total, data in parsed_rs[version])

    bad = [v for v in range(1, 13) if cs_capacity(v) != qr_ref.data_capacity_bytes(v)]
    if bad:
        fail("data capacity differs for versions %s" % bad)
    else:
        ok("data capacity: all 12 versions agree (%d bytes at v12)" % cs_capacity(12))

    def cs_choose(byte_length):
        for version in range(1, 13):
            header = 4 + (8 if version <= 9 else 16)
            if header + byte_length * 8 <= cs_capacity(version) * 8:
                return version
        return -1

    fit_bad = 0
    for length in range(1, 400):
        try:
            expected = qr_ref.choose_version(bytes(length))
        except ValueError:
            expected = -1
        if cs_choose(length) != expected:
            fit_bad += 1
            if fit_bad <= 5:
                fail("version choice differs at %d bytes: C# %d, reference %d"
                     % (length, cs_choose(length), expected))
    if fit_bad == 0:
        ok("version selection: identical for 1-399 byte payloads")

    # ---- 4. mask expressions, evaluated as written ----

    mask_body = source[source.index("private static bool MaskApplies"):]
    mask_body = mask_body[:mask_body.index("private static void ApplyMask")]

    cases = {}
    for match in re.finditer(r"case\s+(\d)\s*:\s*return\s+([^;]+);", mask_body):
        cases[int(match.group(1))] = match.group(2).strip()
    default = re.search(r"default\s*:\s*return\s+([^;]+);", mask_body)
    if default:
        cases[7] = default.group(1).strip()

    if len(cases) != 8:
        fail("expected 8 mask cases, found %d" % len(cases))
    else:
        mismatch = 0
        for mask in range(8):
            expression = cases[mask]
            # C# integer division is Python's //; these expressions contain no comments.
            python_expression = expression.replace("/", "//")
            try:
                compiled = compile(python_expression, "<mask>", "eval")
            except SyntaxError as error:
                fail("mask %d expression %r does not parse: %s" % (mask, expression, error))
                mismatch += 1
                continue

            reference = qr_ref.MASK_FUNCS[mask]
            for y in range(0, 66):
                for x in range(0, 66):
                    got = bool(eval(compiled, {"__builtins__": {}}, {"x": x, "y": y}))
                    if got != bool(reference(x, y)):
                        fail("mask %d disagrees at (%d,%d): C# %r -> %s"
                             % (mask, x, y, expression, got))
                        mismatch += 1
                        break
                if mismatch:
                    break
        if mismatch == 0:
            ok("mask functions: all 8 identical to the reference over a 66x66 grid")

    # ---- 5. named constants ----

    constants = {
        "0x537": "format BCH polynomial",
        "0x5412": "format mask",
        "0x1F25": "version BCH polynomial",
        "0x11D": "GF(256) field polynomial",
        "0b01": "ECC level L",
        "0b0100": "byte mode indicator",
    }
    for literal, description in constants.items():
        if literal not in source:
            fail("%s (%s) missing from QrEncoder.cs" % (description, literal))
    if not any(literal not in source for literal in constants):
        ok("constants: field/format/version polynomials and mode markers all present")

    # ---- 6. the encoder must stay free of Unity so it remains testable ----

    if "UnityEngine" in source:
        fail("QrEncoder.cs references UnityEngine; it must stay portable")
    else:
        ok("QrEncoder.cs has no UnityEngine dependency")

    # ---- 7. regression guards for the two bugs that actually bit ----

    # The generator polynomial is accumulated constant-first and must be reversed before
    # division, or every ECC byte comes out wrong while the payload looks fine.
    if "Array.Reverse(poly)" not in source:
        fail("RsGenerator no longer reverses the generator polynomial "
             "(this was a real bug: ECC bytes were all wrong)")
    else:
        ok("RsGenerator still reverses the generator to highest-term-first")

    # The format region must be reserved before the data zigzag. Reordering these two calls
    # silently writes payload bits into the 31 format modules.
    encode_body = source[source.index("public static QrCode Encode(byte[] data, int version, int forceMask)"):]
    encode_body = encode_body[:encode_body.index("public static QrCode TryEncode")]
    format_at = encode_body.find("PlaceFormat(m, 0)")
    data_at = encode_body.find("PlaceData(m, codewords)")

    if format_at < 0 or data_at < 0:
        fail("could not locate PlaceFormat/PlaceData in Encode()")
    elif format_at > data_at:
        fail("PlaceFormat(m, 0) must come BEFORE PlaceData(m, codewords); "
             "the data zigzag would overwrite the format region")
    else:
        ok("format region is reserved before the data zigzag")

    print()
    if failures:
        print("RESULT: FAIL (%d problems)" % len(failures))
        return 1
    print("RESULT: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
