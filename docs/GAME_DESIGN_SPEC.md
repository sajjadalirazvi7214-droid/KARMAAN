# GAME DESIGN SPEC — KARMAAN

**Rule:** this document is the source of truth. Code implements this. If a step is not here, it does
not exist. If a step here is not traceable to a citation, it must be fixed or deleted.

**Citation base:** Mines Vocational Training Rules 1966 — Second Schedule (below-ground, 6 days),
Third Schedule (gassy mines, 6 days), Fourth Schedule (recall). Cross-checked against the PS text.
Field use must re-check the current DGMS notes (OSH Code 2020 rules, G.S.R. 345(E), 8 May 2026).

---

## 1. THE LOOP (every lesson, no exceptions)

```
IDLE
 └─▶ INTRO          voice greeting + "point at the room", < 8 s
      └─▶ STEP n    present hazard → worker acts
           ├─ correct → STEP n+1
           └─ wrong   → FEEDBACK (kind, in own language, names the fix) → retry same step
                └─ 3 fails → LOCKED (trainer PIN required)
      └─▶ SCORE     ≥70 AND all must-pass green → PASS
                     else → FAIL → retry with the specific weak step repeated
           └─▶ CERT   signed QR, valid 1 year
```

**Tone rule:** a wrong answer is never shamed. The correction names the *action*, not the person.
`"No water on wire fire. Take the blue bottle. Try again."` — never `"Wrong."`

---

## 2. SCORING

| Component | Value |
|---|---|
| Base | 100 |
| Per missed task | −10 |
| Per wrong quiz answer | −5 |
| Time over benchmark | −1 per 5 s (capped −15) |
| Clean PPE (no wrong picks in the PPE step) | +5 |
| **Pass condition** | **score ≥ 70 AND every `mustPass` step green** |
| Attempts before lock | 3 → trainer PIN |

**Task weight beats quiz weight.** You cannot tick your way to a certificate; you have to act.
A worker who scores 95 on quiz but fails the buddy step **fails the lesson**. This is the entire
point of the product.

---

## 3. GUARDRAILS — THE SIX KILLER MYTHS

Hard-coded. These block progress with a specific correction and are never overridable by score.

| # | Myth | Blocked action | Correction (concept) |
|---|---|---|---|
| G1 | Water on **electrical** fire | water bottle picked on wire fire | Use CO₂ / dry sand. Water conducts. |
| G2 | Water on **oil** fire | water picked on fuel fire | Foam or CO₂. Water spreads oil fire. |
| G3 | Switch or phone in gas cloud | any spark action in gas zone | No switches, no phones. Step out, then call. |
| G4 | Loose cloth near rotating machine | cleaning with loose sleeve/towel | Tie it back or it pulls you in. |
| G5 | Tape on a cracked helmet | "repair" a damaged helmet | A cracked helmet is scrap. Replace it. |
| G6 | Run in burning clothes | run action on fire-on-person | Stop, drop, roll. Running feeds the flame. |

These six map directly to the "SOPs skipped most in drills" list in the evidence pack. They are the
highest-value content in the app because each one is a specific, memorable, life-or-death rule.

---

## 4. LESSON 1 — FIRE & BLAST *(SIH demo, must be flawless)*

**Scene:** hostel/shed room. Wire board sparking. Smoke. Main door blocked (red). Side exit (green).
Three bottles on a table. White muster ring on the floor.

| # | Step | Kind | Must-pass | Check | Time |
|---|---|---|---|---|---|
| 1 | Alarm — react to the alarm | Reaction | ✅ | tap within 10 s, order logged | 10 s |
| 2 | Find the **second exit** (main stays red) | Locate | ✅ | correct zone tapped | 20 s |
| 3 | Pick the right bottle for a **wire** fire | Choose | ✅ | blue CO₂ (G1 blocks water) | 15 s |
| 4 | **PASS** at the base of the fire | Gesture | ✅ | pull → aim → squeeze → sweep, in order | 25 s |
| 5 | Reach muster ring, tap roll-call order | Sequence | ✅ | correct order | 20 s |

**Decoys:** red ABC powder (correct for solid fires, wrong here), water bucket (G1).
**Voice lines (Hindi/Santali):** *"Alarm. Go low."* / *"Main door is blocked. Find the other way."* /
*"Wire fire. Which bottle?"* / *"Pull. Aim at the base. Squeeze. Sweep."* / *"Muster. Count heads."*

**Why must-pass:** a worker who finds the bottle but runs through the smoke without the exit has
learned nothing that saves them. Order matters more than knowledge.

---

## 5. LESSON 2 — GAS & CLOSED SPACE *(SIH demo)*

**Scene:** tank lane. Beeping. Red danger ring on the ground. PPE table: helmet, mask, hand lamp,
gas meter, **loose towel (trap)**. Fan switch. Permit board. Buddy avatar "Mohan".

| # | Step | Kind | Must-pass | Check |
|---|---|---|---|---|
| 1 | Spot the beep + red ring | Reaction | ✅ | acknowledge within 8 s |
| 2 | Pick exactly 3 correct PPE, drop the towel | Choose | ✅ | helmet + mask + lamp; towel is wrong |
| 3 | **Buddy check** — tap Mohan | Locate | ✅ | cannot proceed solo |
| 4 | Fan status + permit paper check | Sequence | ✅ | both confirmed before entry |
| 5 | Meter reads **high** → tap **NO-ENTRY** | Choose | ✅ | no-entry = **PASS**, not fail |

**The no-entry branch is the heart of this lesson.** The scoring engine must treat refusal as
success. A trainee who enters a high-gas zone to "finish the level" must be told, kindly and
firmly, that the correct answer was to stop.

**Voice:** *"Beep. Red ring. Do not cross."* / *"Three things only. Helmet, mask, lamp."* /
*"Where is Mohan? Nobody goes alone."* / *"Meter is high. Say no."*

---

## 6. LESSON 3 — MACHINES & LOCKOUT *(pilot scope)*

Belt near a steel shed. Guard cover open.

| # | Step | Must-pass | Check |
|---|---|---|---|
| 1 | Shut the guard cover | ✅ | cover closed before anything else |
| 2 | Hang **own** lock | ✅ | worker's own lock ID |
| 3 | Hang **own** name tag | ✅ | tag matches worker |
| 4 | Zero test reads 0.00 V | ✅ | meter tapped after lock+tag |
| 5 | Clean with snug gloves only | — | G4 blocks loose cloth |

Lock → tag → test → touch. Skip the tag and the app stops you. *(One tester forgot the tag; the
app caught it. That single catch is worth the whole lesson.)*

---

## 7. LESSON 4 — ELECTRIC & SHOCK AID *(pilot scope)*

Rainy shed, Hazaribagh. Wire sparking on the floor. A hurt worker nearby.

| # | Step | Must-pass | Check |
|---|---|---|---|
| 1 | Cut power if safe to reach | ✅ | breaker tapped first |
| 2 | CO₂ or dry sand — **never water** | ✅ | G1 blocks water |
| 3 | Rubber gloves + dry wood to stand on | ✅ | both equipped |
| 4 | Pull the wire with a dry stick — never bare hands | ✅ | G1/bare-hand guard blocks |
| 5 | Call lineman + 108, start shock aid | ✅ | both calls placed |

---

## 8. LESSON 5 — ROOF, BLAST & FIRST AID *(pilot scope)*

Open-cast edge, Pakur. Cracks and drip on the wall.

| # | Step | Must-pass | Check |
|---|---|---|---|
| 1 | Spot cracks + drip + rock sound | ✅ | all three flagged |
| 2 | Shout, bar the path, call your mate | ✅ | sequence in order |
| 3 | Blast horn = **three long hoots** → count them | ✅ | correct count |
| 4 | Run to white shelter, head down, no photos | ✅ | G-rule: no phone camera near blast |
| 5 | Palm press on a bleed + call 101/108 with lane + gate name | ✅ | correct press point |

---

## 9. TEST BANK RULES

- 20 quiz items + 5 task checks per lesson.
- Banks **shuffle** every run; option order shuffles.
- Task checks carry more weight than quiz items.
- Every attempt logs: score, misses, time, tries, retest date, supervisor tick.
- Three failures → `LOCKED`, trainer PIN required to reopen.
- Retest after **7 days** (retention design — the PS evidence says same-day recall proves nothing).

---

## 10. LOCALISATION RULES

| Rule | Detail |
|---|---|
| Voice first, text last | Every step has audio < 8 s |
| No step requires reading to pass | Icons + voice carry the meaning |
| Scripts | Hindi = Devanagari, Santali = **Ol Chiki** (1925 script, keep the pride) |
| Touch targets | **2 cm** minimum — gloves and muddy thumbs |
| Colour code | green = go, red = stop, yellow = wait |
| Voice source | Recorded human voices. TTS is fallback only. |
| Missing key | Must throw loudly in dev, never silently show English |

---

## 11. WHAT THE WORKER SEES, IN ORDER

1. Language picker — **audio preview per language** (tap to hear, don't read)
2. Lesson cards — 5 icons, big, one per hazard
3. "Point at the room" — camera opens
4. Hazard appears
5. Step prompt (voice + icon)
6. Act
7. Kind correction or green tick
8. Score
9. QR certificate (or retry with the weak step repeated)

That is the whole app. There is no menu tree, no settings page, no profile screen in the demo build.
Every extra screen is a place where a nervous 19-year-old gets lost.
