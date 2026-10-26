# KHAN SURAKSHA AR — BUILD PROCEDURE (LOCKED)

**Project:** AR-Based Vocational Training Simulator for Industrial Safety in Jharkhand
**Problem Statement:** SIH 26041 · Govt. of Jharkhand · Dept. of Higher & Technical Education
**Project root:** `E:\Games\ar` — **this is the only location. Nothing outside this folder.**
**Engine:** Unity 6000.3.6f1 (installed) · Built-in Render Pipeline · C#
**Status:** Phase 0 blocked (Android Build Support missing). Everything else scaffolded.

---

## 0. WHAT WE ARE ACTUALLY BUILDING

Not a "game" in the entertainment sense. It is a **scored simulator**: a worker points a mid-range
Android phone at a real room, the phone paints fake hazards into that room, and the app refuses to
give a certificate until the worker *does the right actions in the right order*.

The whole product is one loop:

```
point camera → hazard appears → worker acts → rules engine scores → pass/fail → QR cert
```

If a feature does not serve that loop, it does not go in the demo build.

### The five non-negotiables (from the PS — cannot be cut)

| # | Requirement | Proof in demo |
|---|---|---|
| 1 | ≥ 2 full AR modules in the APK | Fire + Gas, both playable end to end |
| 2 | Assessment engine | Task checks + must-pass gates + 70% pass line |
| 3 | QR certificate: make **and** verify | Made offline, scanned offline, green/red/yellow |
| 4 | Hindi + Santali | Voice + Ol Chiki / Devanagari script toggle |
| 5 | Offline mode | Full lesson + test + QR with airplane mode ON |

Plus the deliverable pack: **APK + admin dashboard + demo film + public GitHub**.

### What we are explicitly NOT doing in the SIH build

- No VR headsets. Ever.
- No cloud dependency for the learning loop. Server is for sync and records only.
- No AI deciding pass/fail. Rules decide. Models only advise. (PS line: *"No life choice by black box."*)
- No 3D twin of a real mine. Low-poly stylised rooms only.
- No Modules 3–5 in the demo APK. They are pilot scope. Demo = Fire + Gas, polished.

---

## 1. ENVIRONMENT AUDIT (done — this is the starting truth)

| Item | Status | Detail |
|---|---|---|
| Unity Editor | ✅ Installed | `6000.3.6f1` at `C:\Program Files\Unity\Hub\Editor\6000.3.6f1` |
| Windows Standalone | ✅ Installed | — |
| WebGL Support | ✅ Installed | — |
| **Android Build Support** | ❌ **MISSING** | **HARD BLOCKER — no APK without this** |
| Android SDK / NDK / OpenJDK | ❌ Missing | Bundled with the Android module |
| Unity license | ✅ Entitlement present | `UnityEntitlementLicense.xml` |
| Network → Unity registry | ✅ Reachable | `packages.unity.com` returns 200 |
| Git | ✅ `2.55.0` | — |
| Disk (E:) | ✅ 72 GB free | — |
| Test Android device | ❓ Unknown | Must confirm ARCore support |

**This is why Phase 0 exists and why nothing else can be called "done" before it.**

---

## 2. LOCKED ARCHITECTURE DECISIONS

Decided now so we don't relitigate them mid-build.

| Decision | Choice | Why |
|---|---|---|
| Render pipeline | **Built-in RP** | Fewer moving parts. URP adds a Graphics settings + URP asset failure mode (pink materials) for zero demo benefit on low-poly content. Revisit only if we need post-processing. |
| AR stack | **AR Foundation 6.7.0 + ARCore XR Plugin 6.7.0** | AR Foundation is the PS-named stack. ARCore is the only phone AR runtime that hits Android 10 mid-range. |
| AR fallback | **Mandatory gyro-driven 3D mode** | PS requirement: plain phones must still learn. Same lesson data, same test, no ARCore. |
| Input | Legacy Input + Unity UI | AR Foundation pulls Input System in as a dependency anyway; we enable **Both** backends and write against legacy to keep UI simple. |
| Serialization | Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`) | `JsonUtility` cannot do dictionaries or polymorphism. Lessons are data, not scenes. |
| Lesson content format | **JSON packs in `Assets/_Project/Data/Lessons/`** | Content authored as data → lessons can be updated without touching code → pilot can ship new hazards as pack deltas. |
| Local persistence | **JSONL append-only store** (not SQLite yet) | Zero native plugin, zero Android ABI risk. Swappable behind `IAttemptStore` if we outgrow it. |
| QR | Payload built + HMAC-SHA256 signed in C#; QR *matrix* rendered by ZXing | Signing is ours (trust boundary). Rendering is a solved library problem. |
| Voice | Recorded human audio (Hindi, Santali) + Android TTS fallback | PS is explicit: real human voices, TTS only as fallback. |
| Dashboard | Node + Postgres + React (separate repo folder `server/`) | Not Unity's problem. Built in parallel, Phase 6. |

---

## 3. THE PROCEDURE — PHASES AND TASKS

Every task has an ID and a **done-criteria**. A task is not done because code exists; it is done
because the criteria pass.

---

### PHASE 0 — UNBLOCK (blocking everything)

| ID | Task | Done when |
|---|---|---|
| **P0.1** | Install **Android Build Support** via Unity Hub → Installs → 6000.3.6f1 → Add Modules. Tick: *Android Build Support*, *Android SDK & NDK Tools*, *OpenJDK*. | `Editor\Data\PlaybackEngines\AndroidPlayer\SDK` exists |
| **P0.2** | Confirm a physical test device with ARCore support (Redmi Note 10 / Samsung M32 class per PS). Enable Developer Options + USB debugging. | `adb devices` lists it |
| **P0.3** | `git init` in `E:\Games\ar`, first commit of the scaffold. | `git log` shows 1 commit |
| **P0.4** | Open the project in Unity once, let it import, confirm **zero console errors**. | Console clean; `Library/` generated |

> ⚠️ **If P0.1 fails, stop.** Do not write another line of AR code. A Windows-only build cannot
> demonstrate an Android APK, and the APK *is* the submission.

---

### PHASE 1 — PROJECT SKELETON (target: end of day 1)

| ID | Task | Done when |
|---|---|---|
| **P1.1** | First Unity open: accept Input System backend prompt (**Yes** → editor restarts). | No "input backends" warning |
| **P1.2** | Switch platform to **Android**. Player Settings: package `in.jh.suraksha.ar`, min API **29** (Android 10), target API = highest installed, ARM64 only, IL2CPP. | Build Settings shows Android + ARM64 |
| **P1.3** | XR Plug-in Management → Android tab → enable **ARCore**. | ARCore checkbox ticked |
| **P1.4** | Create 4 scenes: `00_Boot`, `01_Menu`, `02_Lesson`, `03_Result`. Add to Build Settings in that order. | 4 scenes, index 0 = Boot |
| **P1.5** | Add assembly definitions: `Suraksha.Core`, `Suraksha.Content`, `Suraksha.Cert`, `Suraksha.Sync`, `Suraksha.AR`, `Suraksha.UI`. Core must not reference AR. | Compiles; Core has no AR dependency |
| **P1.6** | `GameRoot` singleton in `00_Boot`, `DontDestroyOnLoad`. Loads settings, language, attempt store. | Survives scene load |

**Why asmdefs on day 1:** it physically prevents the rules engine from accidentally depending on
AR Foundation, which is what keeps the 3D fallback mode honest.

---

### PHASE 2 — CORE RULES ENGINE (target: day 2–4)

This is the part that decides pass/fail. It is pure C#, no Unity rendering, no AR. It must be
testable in isolation — that is how we prove "no black box".

| ID | Task | Done when |
|---|---|---|
| **P2.1** | Data model: `LessonDef`, `StepDef`, `OptionDef`, `QuizItem`, `AttemptRecord`. | Loads `lesson_fire.json` with no exceptions |
| **P2.2** | `LessonRunner` state machine: `Idle → Intro → Step → Feedback → … → Passed/Failed/Locked`. | Unit test walks all 5 fire steps |
| **P2.3** | `ScoreEngine`: base 100, −10/miss, −time penalty, +5 clean PPE, pass = **≥70 AND all must-pass green**. | Table-driven tests for pass, fail-on-score, fail-on-must-pass |
| **P2.4** | `GuardrailRules`: block the 6 killer myths, in-language, never shaming. | Each myth returns its specific correction line |
| **P2.5** | Three-strikes lock → trainer unlock. | 3rd fail sets `Locked`, requires PIN |
| **P2.6** | `AttemptStore`: append-only JSONL, UUID dedupe, offline queue. | 100 writes → 100 lines, replay-safe |
| **P2.7** | `Localization`: key → Hindi / Santali string, script toggle, Ol Chiki font swap. | Every visible string resolves; missing key is a loud error |

**Content of the rules is already written** — see `docs/GAME_DESIGN_SPEC.md`. Do not invent steps
in code; the spec is the source of truth and maps to MVTR schedules.

---

### PHASE 3 — LESSON 1: FIRE (target: day 3–7) — *demo critical*

| ID | Task | Done when |
|---|---|---|
| **P3.1** | Blender: low-poly hostel/shed room, 3 extinguisher bottles (red ABC, blue CO2, water), blocked main door, green side exit, white muster ring. **< 50k tris total.** | glTF exports, imports under 50k tris |
| **P3.2** | AR placement: plane detection + tap-to-place room rig. Retry-safe. | Room lands on a real table/floor |
| **P3.3** | Marker fallback: printed A4 marker for dark/dusty sheds. | Marker anchors the rig reliably |
| **P3.4** | Interaction: raycast tap on doors/bottles, walk-to-exit check, PASS sweep gesture. | Each of the 5 steps can be completed by hand |
| **P3.5** | Hazard VFX: smoke, flame, spark — cheap particles, mobile-safe. | Holds 30 fps on Redmi Note 10 |
| **P3.6** | Voice: 5 step prompts + 5 corrections, Hindi + Santali, each **< 8 s**. | Every line plays, no dead audio |
| **P3.7** | 3D fallback mode for the same lesson (gyro look, no ARCore). | Full fire test passes with AR disabled |

Fire must-pass gates (from spec): alarm in 10 s · correct bottle (blue CO2) · PASS at base ·
second exit · muster order.

---

### PHASE 4 — LESSON 2: GAS (target: day 6–10) — *demo critical*

| ID | Task | Done when |
|---|---|---|
| **P4.1** | Blender: tank lane, red danger ring, PPE table (helmet, mask, lamp, gas meter, loose towel decoy), fan, permit board, buddy avatar (Mohan). | Imports, < 50k tris |
| **P4.2** | Beep + zone logic: proximity to red ring triggers audio + visual warning. | Beep rate rises on approach |
| **P4.3** | PPE selection: exactly 3 correct, towel is a trap. | Wrong pick → specific fix line |
| **P4.4** | Buddy tap + fan check + permit check as one must-pass cluster. | Cannot proceed solo |
| **P4.5** | High-meter branch → **NO-ENTRY is a pass**, not a fail. | Choosing no-entry scores as success |
| **P4.6** | Voice + VFX parity with Fire. | — |

> The no-entry branch is the single most important design decision in this app. A trainer who
> teaches "brave means going in" is manufacturing casualties. "Brave means saying no" is in the
> pitch — the code must agree.

---

### PHASE 5 — CERTIFICATE + QR (target: day 8–11)

| ID | Task | Done when |
|---|---|---|
| **P5.1** | `CertPayload`: worker ID, lesson, score, date, centre code, attempt UUID, schema version. | Payload builds deterministically |
| **P5.2** | HMAC-SHA256 signing over canonical JSON. Key in `Settings/DevKeys/` (**gitignored**). | Tamper a byte → verify fails |
| **P5.3** | QR render via ZXing; encode signed payload compactly (`base64url`). | Scannable from a 5 cm print |
| **P5.4** | Offline verify: embedded public key/secret, expiry check, revoke list (cached). | Green / red / yellow / grey all reachable |
| **P5.5** | Validity 1 year → auto **yellow** after expiry. | Clock-forward test passes |
| **P5.6** | Scan time < 10 s at gate, gloves on. | Measured, not assumed |

---

### PHASE 6 — UI, LANGUAGE, DASHBOARD (target: day 9–13, parallel)

| ID | Task | Done when |
|---|---|---|
| **P6.1** | Voice-first UI rules: 2 cm touch targets, green go / red stop / yellow wait, icon-led, no step requires reading. | Usability pass with a low-literacy tester |
| **P6.2** | Language picker with **audio preview** (tap to hear) — not text-only. | A Santali speaker can pick without reading |
| **P6.3** | Ol Chiki + Devanagari font swap, no missing glyphs. | Zero tofu boxes |
| **P6.4** | Trainer mode: PIN-gated, batch view, weak-step heat map, mirror-to-projector. | Trainer unlocks a locked worker |
| **P6.5** | `server/` — Node + Postgres + React dashboard: batch pass map, weak steps, QR verify endpoint, audit log. | Dashboard renders real synced attempts |

---

### PHASE 7 — OFFLINE + RESILIENCE (target: day 11–14)

| ID | Task | Done when |
|---|---|---|
| **P7.1** | Airplane-mode full run: lesson → test → pass → QR → verify. | Passes with no SIM and no Wi-Fi |
| **P7.2** | Queue sync on reconnect, UUID dedupe, no loss / no dupes across 30 phones. | 30-phone sync test clean |
| **P7.3** | Low-battery mode: AR off, quiz alive (PS "24 ideas" #4). | At 5% battery the test still runs |
| **P7.4** | Torch toggle + high-contrast mode for dark sheds. | Readable in a dark room |
| **P7.5** | Thermal guard: pause after sustained use. | No device overheat on 20-min run |

---

### PHASE 8 — SHIP THE SUBMISSION (target: day 12–15)

| ID | Task | Done when |
|---|---|---|
| **P8.1** | Signed release APK, ARM64, installs clean on a fresh phone. | Installs + launches, cold start < 5 s |
| **P8.2** | Demo film (3 min): real hostel room turning into a fire class. | Exported 1080p |
| **P8.3** | Public GitHub: code, lesson packs, docs, README with the demo script. | Link opens for a stranger |
| **P8.4** | Judge script rehearsal, **under 7 minutes**, six taps. | Timed run, twice |
| **P8.5** | Print checklist (Annexure 2 of the evidence pack) walked line by line. | Every line ticked |

---

### PHASE 9 — PILOT HOOKS (post-SIH, do not build now)

Modules 3–5 (Machines/LOTO, Electric, Roof+Blast+First Aid), own-photo hazard hunts, 7-day recall
nudge, buddy phone link, SOP helper, twin card, health gate, SOS. All designed in the evidence
pack. None of it ships in the demo APK. **Resist scope creep — the demo wins on polish, not count.**

---

## 4. CONTENT PIPELINE (Blender → Unity)

```
Blender (low-poly, 1 unit = 1 m)
   → glTF 2.0 (.glb)  [no FBX round-trip, no scale surprises]
   → Assets/_Project/Art/Models/
   → Unity import: scale 1, no animation bake, generate colliders manually
   → Prefab per hazard object (bottle, door, mask, tank, belt)
```

**Budgets (hard limits — these come from the PS "3 GB RAM" claim):**

| Budget | Limit |
|---|---|
| Triangles per lesson scene | **< 50,000** |
| Materials per scene | < 12, one shared atlas |
| Texture size | 1024² max, ETC2/ASTC compressed |
| Lesson pack size (3D + audio) | **60–80 MB** |
| Draw calls | < 60 |
| Target | 30 fps on Redmi Note 10 / Samsung M32 |

Every asset must survive being looked at through a **dusty, scratched lens in a dark shed**.
High contrast beats detail. Silhouette beats texture.

---

## 5. DATA, OFFLINE, AND TRUST MODEL

**What stays on the phone (never leaves):** camera frames, face video, view path, raw sensor data.

**What travels (tiny):** worker ID, lesson ID, score, misses, time, tries, retest date, signed cert.

```
Phone (offline)                        Server (online, optional)
─────────────                          ─────────────────────────
lesson packs (hash-checked)            registry / batches
attempt JSONL queue      ──sync──▶     attempts, certs, revoke list
QR payload + HMAC sign                 dashboard, audit log
QR verify (embedded key)               QR verify endpoint
```

Sync budget: **< 1 MB per worker**. One night sync per batch. If the net never comes, the phone
still works forever — the queue just grows.

**Trust boundary:** the signing key ships inside the APK. That is acceptable for *offline* proof
(a fake needs a reverse-engineered APK), and it is why online verify also checks the **revoke list
and registry**, not just the signature. Say this plainly to judges; don't oversell the crypto.

---

## 6. RISK REGISTER

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | Android module never installed → no APK | **High** | **Fatal** | P0.1 is task one. Nothing starts before it. |
| R2 | ARCore unavailable on test phone | Medium | High | 3D fallback is mandatory, not optional — build it in Phase 3, not "later" |
| R3 | Dusty/dark lens kills tracking | High | High | Torch + printed marker + fallback mode |
| R4 | Santali voice quality rejected by native speakers | Medium | High | Native QA in Dumka/Pakur before demo; keep TTS only as fallback |
| R5 | Scope creep into Modules 3–5 | **High** | High | Phase 9 is explicitly post-SIH. Demo = Fire + Gas. |
| R6 | 50k-tri budget blown by Blender artist | Medium | Medium | Enforce at export; hard check in P3.1/P4.1 |
| R7 | Certificate forgery | Low | High | HMAC + revoke list + 1-year expiry |
| R8 | Content is medically/safety wrong | Low | **Fatal** | Every step traces to MVTR schedule or DGMS sheet; nodal officer signs off |
| R9 | 30-phone sync duplicates/loses attempts | Medium | Medium | UUID dedupe + append-only store + test in P7.2 |

**R8 is the one that ends the project if we get it wrong.** Safety content is not creative
writing. Every must-pass step must be traceable to a citation in `docs/GAME_DESIGN_SPEC.md`.

---

## 7. TIMELINE (15 working days to submission)

| Day | Focus | Exit gate |
|---|---|---|
| 0 | **P0 unblock** | Android module installed, device on adb |
| 1 | P1 skeleton | 4 scenes, ARCore enabled, clean console |
| 2–4 | P2 rules engine | Fire lesson passes headless unit tests |
| 3–7 | P3 Fire lesson | Full AR fire run on device |
| 6–10 | P4 Gas lesson | Full AR gas run on device |
| 8–11 | P5 Cert + QR | Green/red/yellow offline |
| 9–13 | P6 UI + dashboard | Trainer unlocks, dashboard live |
| 11–14 | P7 Offline | Airplane-mode full pass |
| 12–15 | P8 Ship | APK + film + GitHub + 7-min demo |

Phases overlap deliberately. P3 and P4 are the long pole — start Blender assets on day 2,
before the rules engine is finished, or the art becomes the critical path.

---

## 8. WHAT IS ALREADY IN THIS FOLDER

```
E:\Games\ar\
├─ PROCEDURE.md                  ← this file
├─ README.md                     ← quick start, controls, known limits
├─ docs\
│  ├─ GAME_DESIGN_SPEC.md        ← the 5 lessons, exact steps, must-pass, scoring, guardrails
│  ├─ TECH_ARCHITECTURE.md       ← Unity structure, script inventory, data flow
│  └─ BUILD_AND_SHIP.md          ← Android setup, build settings, ship checklist
├─ Packages\manifest.json        ← AR Foundation 6.7.0, ARCore 6.7.0, XR Mgmt, ugui, Newtonsoft
├─ ProjectSettings\ProjectVersion.txt
├─ tools\
│  ├─ check_localisation.py      ← gate: every key resolves (packs + C# + name tags)
│  ├─ check_room_bindings.py     ← gate: every step has a tappable object in the room
│  └─ extract_pdfs.py            ← source-material text extraction
├─ Assets\Editor\
│  ├─ BuildPipeline.cs           ← headless APK build, fails loudly
│  └─ ProjectSetup.cs            ← one-click setup, auto-creates the boot scene
└─ Assets\_Project\
   ├─ Scripts\Core\              ← LessonRunner, ScoreEngine, GuardrailRules, Localization, models
   ├─ Scripts\Cert\              ← HMAC-signed cert payload + verify
   ├─ Scripts\Sync\              ← append-only attempt queue
   ├─ Scripts\Content\           ← lesson pack loader + safety validator
   ├─ Scripts\Bootstrap\         ← GameRoot, AppLauncher
   ├─ Scripts\Game\              ← THE PLAYABLE BUILD
   │   ├─ AppFlow.cs             ← menu → lesson → quiz → result
   │   ├─ LessonPresenter.cs     ← runner ↔ room ↔ HUD bridge
   │   ├─ RoomBuilder.cs         ← procedural grey-box rooms (45 prims each)
   │   ├─ PlayerRig.cs           ← first-person camera + reticle raycast
   │   ├─ GameUI.cs              ← all screens, built at runtime
   │   ├─ UiKit.cs               ← uGUI helpers + self-hit-testing buttons
   │   ├─ Interactable.cs        ← tappable prop, bound to a step + option id
   │   ├─ NameTag.cs             ← billboarded world-space labels
   │   ├─ Prim.cs                ← primitive + material helpers
   │   ├─ Flicker.cs             ← fire/light animation
   │   ├─ ToneBank.cs            ← generated audio, no assets
   │   └─ InputBridge.cs         ← legacy / new Input System abstraction
   ├─ Scripts\AR\                ← (empty — Phase 3)
   ├─ Resources\Lessons\         ← fire.json, gas.json
   ├─ Resources\Localization\    ← hi.json (complete), sat-olchiki.json (0% — stubs)
   ├─ Resources\Fonts\           ← Noto Sans Devanagari + Ol Chiki (OFL)
   ├─ Resources\Settings\        ← cert_secret.txt (dev placeholder)
   └─ Art\ Audio\ Prefabs\ Scenes\ Settings\
```

**Phase status:** P0 blocked (Android module). P1/P2/P3-partial/P6-partial done — the game plays
in 3D mode with both required modules. AR placement, recorded voice, QR image and the dashboard
remain.

---

## 9. NEXT ACTION (do this first, in this order)

1. **P0.1** — Unity Hub → Installs → `6000.3.6f1` → ⚙ → Add modules → Android Build Support
   (+ SDK/NDK + OpenJDK). ~15 min, ~6 GB.
2. **P0.3** — `cd E:\Games\ar && git init && git add -A && git commit -m "scaffold"`
3. **P0.4** — Open the project. Fix any console error before writing code.
4. Then Phase 1.

Everything downstream is blocked on step 1. That is the whole point of writing this down.
