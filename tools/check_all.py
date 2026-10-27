"""Run every repository gate. Exit non-zero if any of them fails.

    python tools/check_all.py

Gates that need optional libraries or editor output (the QR decode gate) report SKIP rather than
failing, so this is safe to run on a bare checkout.
"""

import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

GATES = [
    ("localisation", "check_localisation.py", "every UI / lesson key resolves"),
    ("room bindings", "check_room_bindings.py", "every step has a tappable object"),
    ("qr tables", "check_qr_tables.py", "C# QR constants match the validated reference"),
    ("csharp refs", "check_csharp_refs.py", "Type.Member resolves, call arity matches"),
    ("qr decode", "check_qr.py", "editor PNGs decode back to the token (needs Unity)"),
]


def main():
    failures = 0
    skipped = 0

    for label, script, description in GATES:
        path = os.path.join(HERE, script)
        if not os.path.isfile(path):
            print("%-14s MISSING (%s)" % (label, script))
            failures += 1
            continue

        result = subprocess.run(
            [sys.executable, path],
            capture_output=True, text=True, cwd=os.path.dirname(HERE))

        output = (result.stdout or "") + (result.stderr or "")
        status = "ok"
        if "SKIP" in output and result.returncode == 0:
            status = "skip"
            skipped += 1
        elif result.returncode != 0:
            status = "FAIL"
            failures += 1

        print("%-14s %-5s %s" % (label, status, description))

        if status == "FAIL":
            for line in output.strip().splitlines():
                print("    " + line)

    print()
    if failures:
        print("RESULT: FAIL (%d gate(s) failed)" % failures)
        return 1

    print("RESULT: PASS%s" % (" (%d skipped)" % skipped if skipped else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
