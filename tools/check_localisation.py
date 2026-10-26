#!/usr/bin/env python3
"""
Build gate: localisation integrity for Khan Suraksha AR.

Checks three things, because a missing key anywhere means a worker sees a blank screen
in the middle of a safety lesson:

  1. Every key referenced by a lesson pack exists in the Hindi table.
  2. Every key referenced from C# (loc.Get("..."), loc.GetOrEmpty("...")) exists.
  3. Every name-tag key passed to RoomBuilder.Tag(...) exists.

Then reports Santali coverage. Untranslated entries carry a "TODO-SAT: " prefix so nobody
mistakes machine text for a native speaker's words.

Exit code 0 = shippable localisation. Non-zero = do not submit.

Usage:
    python tools/check_localisation.py             # check
    python tools/check_localisation.py --scaffold   # add stubs for new keys
"""

import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "Assets", "_Project", "Resources")
LESSONS = os.path.join(RES, "Lessons")
LOCALES = os.path.join(RES, "Localization")
SCRIPTS = os.path.join(ROOT, "Assets")

TODO_PREFIX = "TODO-SAT: "

# Keys built by string concatenation, so a regex cannot see them.
DYNAMIC_FAMILIES = [("gesture.p", 4)]

RE_LOC_GET = re.compile(r'\.Get(?:OrEmpty)?\(\s*"([a-zA-Z0-9_.]+)"\s*\)')
RE_TAG = re.compile(
    r'Tag\(\s*[^,]+,\s*room,\s*"[^"]+",\s*"[^"]+",\s*"([a-zA-Z0-9_.]+)"\s*\)'
)


def load(path):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def keys_used_by_lesson(lesson):
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


def keys_used_by_csharp():
    found = set()
    tag_keys = set()

    for dirpath, _, files in os.walk(SCRIPTS):
        for name in files:
            if not name.endswith(".cs"):
                continue

            path = os.path.join(dirpath, name)
            with open(path, encoding="utf-8") as fh:
                text = fh.read()

            for match in RE_LOC_GET.finditer(text):
                found.add(match.group(1))

            for match in RE_TAG.finditer(text):
                tag_keys.add(match.group(1))

    for prefix, count in DYNAMIC_FAMILIES:
        for i in range(1, count + 1):
            found.add(prefix + str(i))

    return found, tag_keys


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

    lesson_keys = set()
    lesson_files = sorted(f for f in os.listdir(LESSONS) if f.endswith(".json"))
    if not lesson_files:
        print("FAIL: no lesson packs found")
        return 2

    for name in lesson_files:
        lesson = load(os.path.join(LESSONS, name))
        keys = keys_used_by_lesson(lesson)
        lesson_keys |= keys
        print(f"  {name:<12} {len(keys):>3} keys")

    code_keys, tag_keys = keys_used_by_csharp()
    print(f"  C# lookups   {len(code_keys):>3} keys")
    print(f"  Name tags    {len(tag_keys):>3} keys")

    used = lesson_keys | code_keys | tag_keys
    missing_hi = sorted(k for k in used if k not in hi)

    print()
    print(f"Hindi table : {len(hi)} entries")
    print(f"Keys needed : {len(used)}")
    print(f"Missing (hi): {len(missing_hi)}")

    if missing_hi:
        print("\nMISSING IN HINDI — a worker would see a blank step:")
        for k in missing_hi:
            print("   -", k)

    sat_path = os.path.join(LOCALES, "sat-olchiki.json")
    if scaffold:
        sat = load(sat_path) if os.path.isfile(sat_path) else {}

        added = 0
        for key, value in hi.items():
            if key not in sat:
                sat[key] = TODO_PREFIX + value
                added += 1

        with open(sat_path, "w", encoding="utf-8") as fh:
            json.dump(sat, fh, ensure_ascii=False, indent=2, sort_keys=True)
            fh.write("\n")

        print(f"\nScaffolded sat-olchiki.json (+{added} stubs)")

    if os.path.isfile(sat_path):
        sat = load(sat_path)
        pending = [k for k in used if sat.get(k, "").startswith(TODO_PREFIX) or k not in sat]
        done = len(used) - len(pending)
        pct = (done / len(used) * 100.0) if used else 100.0

        print(f"\nSantali coverage: {done}/{len(used)} keys ({pct:.0f}%) "
              f"— native QA in Dumka/Pakur required")

        if pending:
            print("  Pending translation (first 8):")
            for k in pending[:8]:
                print("   -", k)
            if len(pending) > 8:
                print(f"   ... and {len(pending) - 8} more")
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
