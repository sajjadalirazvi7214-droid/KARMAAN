# Khan Suraksha AR

**AR-based vocational safety simulator for Jharkhand mining, steel and mica workers.**
SIH 26041 · Govt. of Jharkhand · Dept. of Higher & Technical Education · Team JKSS

Point a mid-range Android phone at a real room. The phone paints fake fire, gas and hazards into
that room. The worker has to *act correctly, in the right order* before the app will issue a
signed QR certificate. No headset, no internet, Hindi and Santali.

---

## Read this first

| Document | What it's for |
|---|---|
| **[`PROCEDURE.md`](PROCEDURE.md)** | **The plan. Phases, tasks, gates, blockers. Start here.** |
| [`docs/GAME_DESIGN_SPEC.md`](docs/GAME_DESIGN_SPEC.md) | The actual game rules — 5 lessons, must-pass steps, scoring, the 6 killer myths |
| [`docs/TECH_ARCHITECTURE.md`](docs/TECH_ARCHITECTURE.md) | Unity structure, script inventory, data flow, trust boundary |
| [`docs/BUILD_AND_SHIP.md`](docs/BUILD_AND_SHIP.md) | Android setup, build gates, the 7-minute judge demo |

---

## Current state — be honest about it

| Layer | State |
|---|---|
| Build procedure | ✅ Written and locked |
| Game design (5 lessons) | ✅ Specified, mapped to MVTR schedules |
| Unity project scaffold | ✅ Manifest, folder tree, build script |
| Core rules engine (C#) | ✅ Written — runner, scoring, guardrails, localisation, cert, store |
| Lesson packs (Fire, Gas) | ✅ JSON, validated |
| Hindi strings | ✅ 162 keys, 0 missing |
| Santali strings | ⚠️ **0% — stubs only, needs native QA in Dumka/Pakur** |
| AR layer | ❌ Not started (Phase 3) |
| UI layer | ❌ Not started (Phase 6) |
| Android APK | ❌ **Blocked — Android Build Support not installed** |
| Dashboard / server | ❌ Not started (Phase 6.5) |

The game does **not** run yet. What exists is the part that decides pass/fail, plus the plan.

---

## Blocker (fix this before anything else)

Unity 6000.3.6f1 is installed, but **Android Build Support is not**. Without it there is no APK,
and the APK is the submission.

```
Unity Hub → Installs → 6000.3.6f1 → ⚙ → Add modules
  ☑ Android Build Support
  ☑ Android SDK & NDK Tools
  ☑ OpenJDK
```

Verify: `C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK`

---

## First run

```bash
cd E:\Games\ar
git init && git add -A && git commit -m "scaffold: procedure, rules engine, lesson packs"

# open in Unity 6000.3.6f1
"C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe" -projectPath "E:\Games\ar"
```

On first open Unity will:
1. Resolve AR Foundation 6.7.0 / ARCore 6.7.0 / Newtonsoft from `Packages/manifest.json`
2. Ask to enable the **Input System** backend → say **Yes** (editor restarts)
3. Generate `Library/` and the missing `ProjectSettings/*.asset` defaults

Then follow `PROCEDURE.md` Phase 1.

---

## Localisation gate

Every lesson key must resolve, or a worker hits a blank step mid-lesson.

```bash
python tools/check_localisation.py            # check
python tools/check_localisation.py --scaffold  # add stubs for new keys
```

Current output: Hindi complete, Santali 0/122. **The Santali table must be filled by a native
speaker, not machine-translated.** The `TODO-SAT:` prefixes are deliberate — they make it
impossible to accidentally ship untranslated safety instructions as if they were real.

---

## What "done" means

Not "the code compiles". Done means these pass (`PROCEDURE.md` §3, Phase 8):

- Installs on a clean phone, cold start < 5 s
- Airplane mode: full Fire lesson → pass → QR, no network at any point
- Airplane mode: full Gas lesson → pass → QR
- QR scan returns green / red / yellow / grey correctly
- Santali voice + Ol Chiki toggle with zero missing glyphs
- Lens covered, room dark → 3D fallback still runs the full test
- 30 phones sync with zero loss and zero duplicates
- Gate scan with gloves on: under 10 seconds

---

*Jai Jharkhand, Safe Jharkhand.*
