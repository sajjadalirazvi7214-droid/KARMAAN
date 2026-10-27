# Karmaan

**AR-based vocational safety simulator for Jharkhand mining, steel and mica workers.**

Point a mid-range Android phone at a real room. The phone paints fake fire, gas and hazards into that room. The worker has to *act correctly, in the right order* before the app will issue a signed QR certificate. No headset, no internet, Hindi and Santali.

## Quick Start

1. Open the project in Unity `6000.3.6f1`.
2. Press **Play** to run in the Editor (3D mode - works on Windows without ARCore).
3. To build for Android: install **Android Build Support** via Unity Hub → Installs → 6000.3.6f1 → Add modules (Android Build Support, Android SDK & NDK Tools, OpenJDK), then switch build target to Android and connect an Android device with ARCore support.

## Controls

| Control | Action |
|---|---|
| Mouse | Look around |
| Left click | Interact with whatever the reticle is on |
| `Esc` | Leave the lesson, back to the menu |

## Lessons

- **Fire lesson:** alarm → second exit → right bottle → PASS → muster.
- **Gas lesson:** beep → three PPE → buddy → fan/permit/meter → refuse entry.

After completing a lesson, a six-question quiz runs and a signed certificate token is generated.

## AR Mode (phone only)

The menu has an **AR:चालू / बंद** switch. On Android with ARCore, the lesson starts by scanning the floor, tapping to place the room hologram, then the lesson begins. Without ARCore, the identical lesson runs in 3D mode as fallback.

## Build Gates

Run `python tools/check_all.py` to verify:
- All localization keys resolve
- Every step has a tappable object in the room
- QR table constants match the validated reference
- C# type references are correct

## Current State

- 3D mode playable in Editor with both Fire + Gas modules
- AR mode wired but requires a device with ARCore
- Hindi: 184/184 localization keys complete
- Santali: stubs only (needs native QA)
- QR: own encoder, validated in Python
- No recorded voice tracks
- Android APK: blocked until Android Build Support installed