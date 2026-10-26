# BUILD & SHIP — ANDROID

## 1. One-time setup (Phase 0 / 1)

### 1.1 Install the Android module (BLOCKER — nothing works without this)

Unity Hub → **Installs** → `6000.3.6f1` → ⚙ → **Add modules**:

- ✅ Android Build Support
- ✅ Android SDK & NDK Tools
- ✅ OpenJDK

Verify:
```
C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK
```
must exist. If it doesn't, the module didn't install.

### 1.2 Project settings

| Setting | Value | Where |
|---|---|---|
| Platform | Android | Build Profiles |
| Package name | `in.jh.suraksha.ar` | Player → Other Settings |
| Minimum API Level | **29** (Android 10 — the PS floor) | Player → Other Settings |
| Target API Level | Highest installed | Player → Other Settings |
| Scripting backend | **IL2CPP** | Player → Other Settings |
| Target architecture | **ARM64 only** (untick ARMv7) | Player → Other Settings |
| Graphics API | **Vulkan + OpenGLES3** (remove Metal) | Player → Other Settings |
| Active input handling | **Both** | Player → Active Input Handling |
| Internet Access | Auto (for sync only) | Player → Other Settings |
| Camera permission | required, declared by ARCore | auto |

### 1.3 XR

`ProjectSetup.ConfigureXr()` assigns the ARCore loader automatically on first open — it creates
`Assets/XR/XRGeneralSettingsPerBuildTarget.asset`, registers it in `EditorBuildSettings`, turns on
**Initialize XR on Startup**, and assigns `ARCoreLoader` for Android.

Verify by hand: Edit → Project Settings → **XR Plug-in Management** → **Android tab** → **ARCore**
ticked.

If the tab is missing, the ARCore package failed to resolve — check `Packages/manifest.json` and
the Package Manager console.

**Why this matters more than it looks.** With no loader assigned, `ARSession` never gets a
subsystem. `ARSession.state` stays `None`, `ArSessionHost` times out after six seconds, and every
phone quietly falls back to 3D. That is indistinguishable from "ARCore isn't supported here", so
it is the single easiest way to ship an AR app that never shows AR. The build gate below catches it.

---

## 2. Build gates (must pass before we call anything "done")

| Gate | Test | Pass |
|---|---|---|
| G1 | Install on a clean phone, no dev tools | Launches, cold start < 5 s |
| G2 | Airplane mode, full Fire lesson | Pass + QR generated offline |
| G3 | Airplane mode, full Gas lesson | Pass + QR generated offline |
| G4 | QR scan: true / fake / expired / revoked | green / red / yellow / grey |
| G5 | Santali voice, Ol Chiki toggle | zero missing glyphs, zero English fallback |
| G6 | Cover the lens, dim the room | 3D fallback keeps the full test |
| G7 | 30 phones sync at night | zero loss, zero duplicates |
| G8 | 20-minute continuous session | no thermal throttle, no OOM |
| G9 | Trainer PIN unlock after 3 fails | unlocks, logs the event |
| G10 | Gate scan with gloves on | < 10 s per worker |

---

## 3. Build command (headless, for CI)

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe" \
  -quit -batchmode -nographics \
  -projectPath "E:/Games/ar" \
  -executeMethod Suraksha.Build.BuildPipeline.BuildAndroid \
  -logFile "E:/Games/ar/Builds/build.log"
```

`Assets/Editor/BuildPipeline.cs` is already scaffolded. It refuses to build if the Android
module is missing, forces ARM64 + API 29, and exits non-zero on failure so CI can catch it.
Output → `E:/Games/ar/Builds/`.

---

## 4. Signing

- Debug keystore for dev. Release keystore generated once, **stored outside the repo**.
- `keystore.properties` is gitignored. Never commit `*.keystore` / `*.jks`.
- The HMAC signing secret for certs lives in `Assets/_Project/Settings/DevKeys/` and is
  **gitignored**. The demo build ships a demo secret; production rotates it.

---

## 5. Submission pack (SIH)

| Item | Location |
|---|---|
| APK | `Builds/KhanSurakshaAR-v1.0.apk` |
| Demo film (3 min) | external link |
| Public GitHub | repo root |
| Judge one-pager | `docs/GAME_DESIGN_SPEC.md` §1–3 |
| Test matrix results | fill from §2 gates above |

---

## 6. The 7-minute judge demo (rehearse twice, timed)

| Time | Judge sees | Must land |
|---|---|---|
| 0:00–1:00 | Pick **Santali**. Point at a real room. Voice: "go low." | Voice plays, icons big |
| 1:00–3:00 | Smoke. Main door **red**. Side arrow **green**. Walk low. | Finds the second exit |
| 3:00–5:00 | Wire fire. Three bottles. Taps **blue CO₂**. Does PASS at base. | No water on wire |
| 5:00–6:00 | Gas beep. Red ring. Mask set. Taps **Mohan**. Fan off, meter high → taps **NO-ENTRY**. | Buddy + no-entry |
| 6:00–7:00 | Miss once → hear the soft fix → pass → QR → guard scans **green** → dashboard shows the batch's weak step. | Scan < 10 s |

**Rehearsal rule:** if the demo runs over 7 minutes, cut a step from the script — never rush the
explanation of *why* no-entry is a pass. That sentence is the pitch.
