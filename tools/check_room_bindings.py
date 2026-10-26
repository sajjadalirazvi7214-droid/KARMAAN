#!/usr/bin/env python3
"""
Build gate: lesson packs vs. the built room.

A lesson step is only answerable if the room actually contains a tappable object bound to
every option id that step can accept. Get that wrong and the worker stands in a room with
no way to pass — the worst possible bug in a training app, and one that never shows up in
a compile.

This cross-checks:
    Assets/_Project/Resources/Lessons/*.json     (what the rules engine accepts)
    Assets/_Project/Scripts/Game/RoomBuilder.cs  (what the room actually provides)

Exit 0 = every step is reachable. Exit 1 = a step is a dead end.
"""

import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LESSONS = os.path.join(ROOT, "Assets", "_Project", "Resources", "Lessons")
ROOM = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Game", "RoomBuilder.cs")

RE_TAG = re.compile(
    r'Tag\(\s*[^,]+,\s*room,\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)"\s*\)'
)

# Steps answered by touching a single object rather than picking from a list.
SINGLE_TARGET_KINDS = {"Reaction", "Gesture"}


def room_bindings():
    text = open(ROOM, encoding="utf-8").read()
    return {(m.group(1), m.group(2)) for m in RE_TAG.finditer(text)}


def main():
    if not os.path.isfile(ROOM):
        print("FAIL: RoomBuilder.cs not found")
        return 2

    bindings = room_bindings()
    steps_by_id = {}
    for step, option in bindings:
        steps_by_id.setdefault(step, set()).add(option)

    print(f"Room provides {len(bindings)} bindings across {len(steps_by_id)} steps:")
    for step in sorted(steps_by_id):
        print(f"  {step:<18} {sorted(steps_by_id[step])}")

    problems = []
    checked = 0

    for name in sorted(f for f in os.listdir(LESSONS) if f.endswith(".json")):
        lesson = json.load(open(os.path.join(LESSONS, name), encoding="utf-8"))
        print(f"\n{name}  ({lesson['lessonId']})")

        for step in lesson.get("steps", []):
            checked += 1
            sid = step["id"]
            kind = step["kind"]
            provided = steps_by_id.get(sid, set())

            if kind in SINGLE_TARGET_KINDS:
                if not provided:
                    problems.append(f"{lesson['lessonId']}/{sid}: no object bound at all")
                    print(f"  {sid:<18} FAIL  no object")
                else:
                    print(f"  {sid:<18} ok    single target ({sorted(provided)[0]})")
                continue

            expected = {o["id"] for o in step.get("options", [])}
            missing = expected - provided
            extra = provided - expected

            if missing:
                problems.append(f"{lesson['lessonId']}/{sid}: no object for {sorted(missing)}")
                print(f"  {sid:<18} FAIL  missing {sorted(missing)}")
            elif extra:
                problems.append(f"{lesson['lessonId']}/{sid}: room binds unknown option {sorted(extra)}")
                print(f"  {sid:<18} FAIL  unknown {sorted(extra)}")
            else:
                print(f"  {sid:<18} ok    {len(expected)} options bound")

    print(f"\nChecked {checked} steps.")
    if problems:
        print("\nPROBLEMS:")
        for p in problems:
            print("  -", p)
        print("\nRESULT: FAIL — a worker could reach a step they cannot answer.")
        return 1

    print("RESULT: PASS — every step is reachable in the room.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
