# TECH ARCHITECTURE — KARMAAN

## 1. Assembly split (Phase 1 task P1.5)

```
Karmaan.Core        ← no Unity rendering, no AR. Models, LessonRunner, ScoreEngine,
                        GuardrailRules, Localization. THE TESTABLE HEART.
Karmaan.Content     ← lesson pack loading, validation, hash check
Karmaan.Cert        ← cert payload, HMAC sign/verify, QR string
Karmaan.Sync        ← attempt queue, sync client
Karmaan.AR          ← AR Foundation + ARCore. Presentation only.
Karmaan.UI          ← screens, voice playback, big-key components
```

**Hard rule:** `Karmaan.Core` must never reference `Karmaan.AR`. If it does, the 3D fallback
mode becomes impossible and the app is no longer usable on half the phones in Jharkhand.
The asmdef makes this a compile error, not a code-review opinion.

> **Do not hand-write the `.asmdef` files blind.** Create them in the Editor (right-click →
> Create → Assembly Definition) so reference names are picked from a real list. Two gotchas:
> - The Newtonsoft package (`com.unity.nuget.newtonsoft-json`) ships a precompiled
>   `Newtonsoft.Json.dll`. If an asmdef can't see it, set **Override References = true** and add
>   `Newtonsoft.Json.dll` to **Assembly References → Precompiled References**.
> - `Karmaan.AR` must reference `Unity.XR.ARFoundation`, `Unity.XR.ARCore`,
>   `Unity.XR.CoreUtils`, `Unity.XR.Management`. Those names must be picked from the list,
>   not typed — a typo silently drops the assembly and you get "type not found" later.

The scaffold ships **without** asmdefs so the first open compiles cleanly as one assembly.
Add them at P1.5, then never let `Core` reference `AR`.

---

## 2. Scene flow

```
00_Boot   → GameRoot (persistent) → language check → 
01_Menu   → lesson cards, trainer PIN entry →
02_Lesson → AR rig OR 3D fallback rig → LessonRunner drives everything →
03_Result → score breakdown, weak steps, QR cert, retry
```

`GameRoot` owns: `SettingsService`, `LocalizationService`, `AttemptStore`, `CertService`,
`LessonRunner`. Scenes are thin. All state lives in `GameRoot` so a scene reload never loses an
attempt.

---

## 3. Script inventory (what exists vs. what's next)

### Built — core (pure C#, no AR dependency)

| File | Responsibility |
|---|---|
| `Core/LessonModels.cs` | `LessonDef`, `StepDef`, `OptionDef`, `StepKind`, `AttemptRecord`, `StepResult` |
| `Core/LessonRunner.cs` | State machine + event stream |
| `Core/ScoreEngine.cs` | Scoring + pass/fail decision |
| `Core/GuardrailRules.cs` | The six killer myths |
| `Core/Localization.cs` | Key → string, script toggle, pending-locale flag |
| `Cert/CertService.cs` | Payload, canonical JSON, HMAC-SHA256, verify, expiry state |
| `Sync/AttemptStore.cs` | Append-only JSONL queue with UUID dedupe |
| `Content/LessonRepository.cs` | Pack loading + safety validation |
| `Bootstrap/GameRoot.cs` | Wiring |
| `Bootstrap/AppLauncher.cs` | Boot-scene entry point |

### Built — the playable build (`Scripts/Game`)

| File | Responsibility |
|---|---|
| `AppFlow.cs` | Menu → lesson → quiz → result state machine |
| `LessonPresenter.cs` | Runner ↔ room ↔ HUD bridge; per-step input rules |
| `RoomBuilder.cs` | Procedural grey-box rooms, 45 prims each, bound to option ids |
| `PlayerRig.cs` | First-person camera, reticle raycast, step filter |
| `GameUI.cs` | Every screen, built at runtime, no prefabs |
| `UiKit.cs` | uGUI helpers + buttons that hit-test themselves |
| `Interactable.cs` | Tappable prop bound to (stepId, optionId) |
| `NameTag.cs` | Billboarded world-space labels |
| `Prim.cs` | Primitive + material helpers |
| `Flicker.cs` | Fire/light animation, no ParticleSystem |
| `ToneBank.cs` | Generated audio — alarm, beep, correct, wrong |
| `InputBridge.cs` | Legacy / new Input System abstraction |

### Built — AR (`Scripts/AR`)

| File | Responsibility |
|---|---|
| `ArSessionHost.cs` | Builds `ARSession` + `ARInputManager` + `XROrigin` + `ARCameraManager` + `ARCameraBackground` + `TrackedPoseDriver` + `ARRaycastManager` + `ARPlaneManager` in code. Probes availability, times out at 6 s, reports a reason code. |
| `ArPlacement.cs` | Keeps the placed room glued to the plane it was dropped on, by remembering the offset in the plane's frame — survives the plane being subsumed. |
| `ArViewRig.cs` | `IViewRig` for AR: the device camera is the viewer, a tap on the screen picks a prop. |
| `Game/IViewRig.cs` | The seam. `PlayerRig` (3D) and `ArViewRig` (AR) both implement it; nothing above it knows which is running. |

### Next (Phase 3+)

| File | Responsibility | Phase |
|---|---|---|
| `AR/MarkerAnchor.cs` | Printed A4 marker anchoring for dark sheds where plane detection fails | 3 |
| `UI/TrainerPanel.cs` | PIN, unlock, batch weak map | 6 |

**The AR seam:** `IViewRig` answers two questions — where is the viewer, and what are they
pointing at. `PlayerRig` answers with a mouse-look camera and a centre reticle. `ArViewRig` answers
with the device camera and the tap position. The room, the runner, the scoring and the HUD never
learn which one is running, which is why the 3D fallback was built first rather than last.

**Why AR is a diorama, not a walk-in room.** At `ArRoomScale = 0.28` the 9 × 7 m workshop becomes
a 2.5 × 2 m hologram on the floor. Two reasons: a walk-in room needs about 7 m of clear ground that
a training centre does not have, and — the deciding one — from eye height a 0.9 m near wall one
metre away hides everything closer than 2.5 m, which is most of the room. So AR builds the shell in
`RoomView.Cutaway`: no ceiling, no near wall, roll-call board moved to the west wall. The 3D
fallback keeps the room enclosed because you are standing inside it.

---

## 4. Lesson pack format

Lessons are **data**, not scenes. One JSON per lesson in `Assets/_Project/Data/Lessons/`.

```jsonc
{
  "schemaVersion": 1,
  "lessonId": "fire",
  "displayOrder": 1,
  "titleKey": "lesson.fire.title",
  "passScore": 70,
  "timeLimitSec": 300,
  "steps": [
    {
      "id": "fire_alarm",
      "kind": "Reaction",           // Reaction | Choose | Sequence | Gesture | Locate
      "mustPass": true,
      "timeLimitSec": 10,
      "promptKey": "fire.alarm.prompt",
      "voiceKey":  "fire.alarm.voice",
      "failHintKey": "fire.alarm.hint",
      "options": [],                 // OptionDef[] for Choose
      "correctOptionId": "",
      "guardrailIds": ["G1"]         // guardrails armed on this step
    }
  ],
  "quiz": [
    { "id": "q1", "promptKey": "fire.q1", "optionKeys": ["a","b","c"], "correctIndex": 1 }
  ]
}
```

**Why data-driven:** the pilot needs new hazards without a new APK. Ship a pack delta, not a build.
It also means a safety officer can review the lesson as a text file instead of playing a game.

---

## 5. Data flow / trust boundary

```
┌─────────────────────── PHONE (offline-first) ───────────────────────┐
│ camera frame ──▶ AR Foundation ──▶ hazard rig                      │
│ worker tap   ──▶ Interactable ──▶ LessonRunner ──▶ ScoreEngine     │
│                                        │                            │
│                                        ▼                            │
│                                  AttemptStore (JSONL)               │
│                                        │                            │
│                                        ▼                            │
│                                  CertService (HMAC) ──▶ QR          │
│                                                                      │
│  NEVER LEAVES: camera frames, face video, view path, sensor data    │
└──────────────────────────────────┬──────────────────────────────────┘
                                   │ sync: id, lesson, score, misses,
                                   │ time, tries, cert  (< 1 MB/worker)
                                   ▼
┌─────────────────────── SERVER (optional) ───────────────────────────┐
│ Postgres: registry, attempts, certs, revoke list, audit             │
│ React: batch pass map, weak-step heat map, QR verify                │
└─────────────────────────────────────────────────────────────────────┘
```

**Cert verify precedence:** signature → expiry → revoke list. Offline uses signature + expiry only.
Online adds revoke. A cert older than 1 year is **yellow** (retest due), not red.

---

## 6. Performance budget

| Metric | Target |
|---|---|
| Tris per lesson scene | < 50,000 |
| Draw calls | < 60 |
| Frame rate | 30 fps on Redmi Note 10 |
| Cold start | < 5 s |
| Lesson pack | 60–80 MB |
| Sync payload | < 1 MB / worker |
| APK (ARM64 only) | < 150 MB |

If a frame budget is blown, cut geometry — never cut the guardrails or the must-pass gates.

---

## 7. Failure modes we design for

| Failure | Behaviour |
|---|---|
| No ARCore on device | Silently open 3D fallback, same lesson, same test |
| Tracking lost mid-lesson | Freeze step timer, show "look at the floor", resume — never fail the worker for the phone's problem |
| Dark room | Torch prompt + marker mode |
| Low battery | AR off, quiz alive |
| No network ever | Everything works; queue grows |
| App killed mid-attempt | Attempt restored from JSONL on next launch |
| Wrong language selected | One-tap switch mid-lesson, voice re-plays current step |

That last one matters more than it looks. A worker who picked the wrong language on the first
screen must not have to abandon the attempt.
