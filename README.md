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
| Core rules engine (C#) | ✅ Runner, scoring, guardrails, cert, store |
| **Playable game — 3D mode** | ✅ **Fire + Gas, full loop, runs in the Editor today** |
| Grey-box rooms | ✅ 45 primitives each, coloured, name-tagged |
| Lesson packs (Fire, Gas) | ✅ JSON, validated |
| Hindi strings | ✅ 171 keys, 0 missing |
| Santali strings | ⚠️ **0% — stubs only, needs native QA in Dumka/Pakur** |
| QR **image** | ❌ Needs ZXing (Phase 5). The signed token and offline verify already work. |
| AR mode (ARCore plane placement) | ❌ Phase 3 — the rig is built to accept it, not yet wired |
| Voice tracks (Hindi/Santali) | ❌ Phase 3 — tones are generated, speech is not recorded |
| Android APK | ❌ **Blocked — Android Build Support not installed** |
| Dashboard / server | ❌ Phase 6 |

**The game plays now, in the Editor, in 3D mode.** It is a grey-box: coloured shapes with
floating name tags, not modelled art. That is the intended build order — prove the loop, then
replace the props.

---

## Play it

1. Open the project in Unity `6000.3.6f1`. On first open it creates `00_Boot.unity` automatically
   (menu: **Suraksha → Set up project** if you want to re-run it).
2. Press **Play**.

| Control | Action |
|---|---|
| Mouse | Look around |
| Left click | Interact with whatever the reticle is on |
| `Esc` | Leave the lesson, back to the menu |

Pick a language, pick a lesson, then work through five steps. The reticle turns green when you
are aiming at something that answers the current step. Get three steps wrong and the lesson
locks for three seconds — the demo stand-in for fetching a trainer.

**Fire lesson:** alarm → second exit → right bottle → PASS → muster.
**Gas lesson:** beep → three PPE → buddy → fan/permit/meter → refuse entry.

Then a six-question quiz, a score, and a signed certificate token you can verify on the spot.

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
python tools/check_localisation.py             # check
python tools/check_localisation.py --scaffold  # add stubs for new keys
python tools/check_room_bindings.py            # every step must be reachable in the room
```

`check_localisation.py` scans lesson packs **and** C# lookups **and** name-tag keys, so a typo in
`_loc.Get("ui.result.weakk")` fails the build instead of blanking a screen.

`check_room_bindings.py` cross-checks the lesson packs against `RoomBuilder.cs`. A step whose
option ids have no tappable object in the room is unanswerable — the worst possible bug in a
training app, and one no compiler will ever catch.

Current output: Hindi complete (142 keys, 0 missing), Santali 0/142. **The Santali table must be
filled by a native speaker, not machine-translated.** The `TODO-SAT:` prefixes are deliberate —
they make it impossible to accidentally ship untranslated safety instructions as if they were real.

---

## Known limits of this build

| Limit | Why it is acceptable now | Fix |
|---|---|---|
| No ARCore placement | The 3D rig, room and runner are the same objects AR will drive | Phase 3 |
| No QR image | The token is real and signed; only the matrix is missing | ZXing, Phase 5 |
| No recorded voice | Tones prove the audio path; prompts are on screen | Native voice talent, Phase 3 |
| Devanagari shaping | Unity's legacy `Text` does no OpenType shaping, so a few conjuncts render imperfectly. Short strings mostly survive. | TMP + pre-shaped atlas |
| Trainer lock is a 3-second hold | Models the workflow without a PIN UI | Phase 6 |
| 5 lessons specified, 2 playable | PS only requires 2 modules in the demo | Phase 9 |

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
