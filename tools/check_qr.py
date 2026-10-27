"""Gate: do the QR symbols the C# encoder produces actually scan?

Companion to Assets/Editor/QrSelfTest.cs. Run the editor menu item first:

    Karmaan > Run QR self-test (writes PNGs)

then from the repo root:

    python tools/check_qr.py

Every PNG in Builds/qr-selftest is decoded with ZXing and compared against the text
recorded in manifest.json. This is the only check that proves the *logic* of the port:
tables can be perfect while the Reed-Solomon division or the data zigzag is wrong, and a
symbol with correct finder patterns and a garbage payload still looks plausible on screen.

Requires: zxing-cpp, numpy, opencv-python-headless
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, os.pardir))
FOLDER = os.path.join(ROOT, "Builds", "qr-selftest")
MANIFEST = os.path.join(FOLDER, "manifest.json")


def main():
    try:
        import cv2
        import zxingcpp
    except ImportError as error:
        print("SKIP: decoder libraries not installed (%s)" % error)
        print("      pip install zxing-cpp numpy opencv-python-headless")
        return 0

    if not os.path.isfile(MANIFEST):
        print("SKIP: no manifest at %s" % MANIFEST)
        print("      Run the editor menu item 'Karmaan > Run QR self-test (writes PNGs)' first.")
        return 0

    with open(MANIFEST, encoding="utf-8") as handle:
        cases = json.load(handle)

    if not cases:
        print("FAIL: manifest is empty")
        return 1

    failures = 0
    for case in cases:
        path = os.path.join(FOLDER, case["file"])
        if not os.path.isfile(path):
            print("FAIL %-18s missing file" % case["file"])
            failures += 1
            continue

        image = cv2.imread(path, cv2.IMREAD_GRAYSCALE)
        if image is None:
            print("FAIL %-18s could not read PNG" % case["file"])
            failures += 1
            continue

        result = zxingcpp.read_barcode(image)
        got = result.text if result else None

        if got == case["text"]:
            print("ok   %-18s v%-2d mask %d  %d chars"
                  % (case["file"], case["version"], case["mask"], case["length"]))
        else:
            failures += 1
            if got is None:
                print("FAIL %-18s not detected (v%d, %d chars)"
                      % (case["file"], case["version"], case["length"]))
            else:
                print("FAIL %-18s decoded %d chars, expected %d"
                      % (case["file"], len(got), len(case["text"])))
                print("       got: %r" % got[:80])
                print("       exp: %r" % case["text"][:80])

    print()
    if failures:
        print("RESULT: FAIL (%d of %d symbols unreadable)" % (failures, len(cases)))
        return 1

    print("RESULT: PASS (%d symbols round-trip through ZXing)" % len(cases))
    return 0


if __name__ == "__main__":
    sys.exit(main())
