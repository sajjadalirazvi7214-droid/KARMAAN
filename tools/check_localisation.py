#!/usr/bin/env python3
"""
Build gate: localisation integrity for Khan Suraksha AR.

Two jobs:
  1. Verify every key referenced by a lesson pack exists in the Hindi table.
     A missing key means a worker hits a blank screen mid-lesson.
  2. Report Santali coverage. Untranslated entries are prefixed "TODO-SAT:" so
     nobody mistakes machine text for a native speaker's words.

Exit code 0 = shippable localisation. Non-zero = do not submit.

Usage:
    python tools/check_localisation.py            # check
    python tools/check_localisation.py --scaffold # create/refresh sat-olchiki.json stubs
"""

import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "Assets", "_Project", "Resources")
LESSONS = os.path.join(RES, "Lessons")
LOCALES = os.path.join(RES, "Localization")
TODO_PREFIX = "TODO-SAT: "

KEY_FIELDS = ("titleKey", "promptKey", "voiceKey", "failHintKey", "labelKey", "promptKey")


def load(path):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def keys_used_by_lesson(lesson):
    """Every localisation key a lesson pack needs at runtime."""
    used = set()

    def add(k):
        if isinstance(k, str) and k.strip():
            used.add(k.strip())

    add(lesson.get("titleKey"))

    for step in lesson.get("steps", []):
        add(step.get("promptKey"))
        add(step.get("voiceKey"))
        add(step.get("failHintKey"))
        for opt in step.get("options", []):
            add(opt.get("labelKey"))
            add(opt.get("voiceKey"))

    for q in lesson.get("quiz", []):
        add(q.get("promptKey"))
        for k in q.get("optionKeys", []):
            add(k)

    return used


def main():
    scaffold = "--scaffold" in sys.argv

    if not os.path.isdir(LESSONS):
        print("FAIL: lesson folder not found:", LESSONS)
        return 2

    hi_path = os.path.join(LOCALES, "hi.json")
    if not os.path.isfile(hi_path):
        print("FAIL: hi.json not found")
        return 2

    hi = load(hi_path)

    used = set()
    lesson_files = sorted(f for f in os.listdir(LESSONS) if f.endswith(".json"))
    if not lesson_files:
        print("FAIL: no lesson packs found")
        return 2

    for name in lesson_files:
        lesson = load(os.path.join(LESSONS, name))
        lesson_used = keys_used_by_lesson(lesson)
        used |= lesson_used
        print(f"  {name:<14} {len(lesson_used):>3} keys")

    missing_hi = sorted(k for k in used if k not in hi)

    print()
    print(f"Hindi table : {len(hi)} entries")
    print(f"Keys needed : {len(used)}")
    print(f"Missing (hi): {len(missing_hi)}")

    if missing_hi:
        print("\nMISSING IN HINDI — a worker would see a blank step:")
        for k in missing_hi:
            print("   -", k)

    # ---- Santali scaffold ----
    sat_path = os.path.join(LOCALES, "sat-olchiki.json")
    if scaffold:
        sat = {}
        if os.path.isfile(sat_path):
            sat = load(sat_path)

        added = 0
        for key, value in hi.items():
            if key not in sat:
                sat[key] = TODO_PREFIX + value
                added += 1

        with open(sat_path, "w", encoding="utf-8") as fh:
            json.dump(sat, fh, ensure_ascii=False, indent=2, sort_keys=True)
            fh.write("\n")

        print(f"\nScaffolded sat-olchiki.json (+{added} stubs)")

    # ---- Santali coverage ----
    if os.path.isfile(sat_path):
        sat = load(sat_path)
        pending = [k for k in used if sat.get(k, "").startswith(TODO_PREFIX) or k not in sat]
        done = len(used) - len(pending)
        pct = (done / len(used) * 100.0) if used else 100.0

        print(f"\nSantali coverage: {done}/{len(used)} keys ({pct:.0f}%) — native QA in Dumka/Pakur required")

        if pending:
            print("  Pending translation (first 12):")
            for k in pending[:12]:
                print("   -", k)
            if len(pending) > 12:
                print(f"   ... and {len(pending) - 12} more")
    else:
        print("\nsat-olchiki.json missing — run with --scaffold to create stubs")

    print()
    if missing_hi:
        print("RESULT: FAIL (Hindi incomplete)")
        return 1

    print("RESULT: PASS (Hindi complete; Santali tracked separately)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
