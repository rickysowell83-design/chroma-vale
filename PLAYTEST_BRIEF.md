# Chroma Vale — Playtest Brief (L1–L10) — BUILD v10004

**Build:** `ChromaVale_playtest_release_signed_v10005_artwin.aab` (Android App Bundle, **upload-signed**, Play-ready)
**Path:** `C:/Users/rsowe/AppData/Local/hermes/profiles/game-director/workspace/chroma-vale-repo/builds/android/ChromaVale_playtest_release_signed_v10005_artwin.aab`
**Version:** bundleVersionCode **10005** (versionName 1.0.1) — bumped 2026-08-27 after Play rejected reused 10004. **Includes BOTH the win-condition fix `3af7593` AND the orb-art wiring fix `e2cc6b6`.** Verified: manifest versionCode=10005 (aapt byte-parse), upload-signed CN=Manahunter4.
**⚠️ Do NOT use any earlier AAB (`v10004.aab`, `v10004_WIN.git`, `v10004_unsigned`).** Those either lack the win fix (no win popup) or lack the orb-art fix (plain dots), AND reuse versionCode 10004 (Play rejects). This `v10005_artwin` file is the correct one.
**Levels:** 1–10 (vertical slice, redesigned per DESIGN_CANON v2.3.0)
**Audience:** blind / outside critics — fresh eyes, no design context needed
**Goal:** tell us where the game is *fun*, where it *drags*, and where it is *unclear*.

> This brief supersedes the earlier Aug-25 brief. L8 is **no longer** the "Duskfall Blackout" — per the shipped redesign it is a calm **"Still Waters"** breather level. Do not look for darkness/Brown on L8.

---

## 1. Install (step-by-step)

This is an **AAB** for Google Play **Internal Testing** (recommended path):

1. Upload `ChromaVale_playtest_release_signed_v10004.aab` to **Play Console → Testing → Internal testing → Create release**.
2. Roll out, then send the Internal Testing invite link to your test Google account.
3. Open the link on the device, opt in, install from the Play Store.

**Alternative (sideload APK):** a debug-signed `ChromaVale_playtest.apk` also exists in `builds/android/`; `adb install -r builds/android/ChromaVale_playtest.apk`. (APK is debug-signed — fine for local sideload, not for Play.)

**First launch:** tap **Chroma Vale** → **level select grid** (10 levels; later ones lock until earlier cleared) → tap **Level 1**.

---

## 2. Core controls

Chroma Vale is a **drag-to-merge** puzzle. No menus inside a level.

| Action | How |
|--------|-----|
| **Merge** | Press and **drag one orb onto a compatible orb**, release on top. |
| **What merges** | Two orbs of the **same color & same tier** → next tier (T1+T1→T2). Same color only. |
| **Color mixing** | From **L7+**, drag a **primary onto its complementary primary** to mix: Cyan+Magenta→Blue, Cyan+Yellow→Green, Magenta+Yellow→Orange. (Orange+Orange or triple-mix → Brown "spoiled" pigment — but **not used in L1–10**, deferred to Act IV.) |
| **Reset** | Top-right **RESET** restarts the current level (progress on it wiped; earned stars stay). |
| **Win** | Collapse the board so the **target orbs** exist (e.g. L1: one T3; L8: three T2). A **CIRCUIT RESTORED!** popup appears with **Next / Replay**. |
| **Next / Replay** | Tap the popup buttons. (Raw-touch fallback added — if a tap seems ignored, tap again; that's a watched rough edge.) |

**Mental model:** a "sort + merge" toy. No fail timer. Clear clutter by collapsing many small orbs into fewer, higher-tier ones. Take your time.

---

## 3. What to evaluate (the fun bar)

We rebuilt L1–10 around one question: **does each level create a "hook moment" — a point where you go "oh, that's cool" and want to keep going?** Judge against these four lenses.

### 3.1 Per-level hook moments (current L1–10)
- **L1 (Clear the Clutter):** first merge. Did merging feel satisfying on its own?
- **L2 (The Cascade):** a T3 appears from chained merges. Did the bigger orb "pop" into existence?
- **L3 (Size & Scale):** a T4 boss orb. Did its size read as progress?
- **L4 (Color Born):** mix *different colors at T1* for the first time — unlearn the "same-color" instinct. Did the mix read clearly?
- **L5 (Tier-5 Summit):** the biggest orb in the game. Was reaching it a payoff?
- **L6 (The Gauntlet):** final pure-merge test (many orbs → one T3). Climax or grind?
- **L7 (First Light — Color Mixing):** mix two primaries into a secondary. **Biggest new-mechanic moment.** Did you understand *why* those two merged?
- **L8 (Still Waters):** a **calm breather** — consolidate 3× T2. No darkness, no Brown. Does the pacing breather land, or feel like a non-event?
- **L9 (Prism Pressure):** mix-heavy with a tight target. Fair or frustrating?
- **L10 (Chromatic Mastery):** capstone — everything at once. Finale feel?

**For each level:** Did you know what to do *without* instructions? Anything surprising (good/bad)? Where did you hesitate?

### 3.2 Juice & feedback feel
We added: merge **particle bursts**, orb **scale-pop** tween, **screen shake** on big merges/wins, **haptics** (vibration) on merge + win, **win confetti**. Judge:
- Does merging *feel* good or flat?
- Scale-pop / burst noticeable but not overwhelming?
- Shake helpful or annoying?
- Did the phone vibrate on merge? Welcome or intrusive?
- On win: confetti + "CIRCUIT RESTORED!" — reward or meh?

### 3.3 UI clarity
- **Top bar:** left = **TRACES** (move count), center = **LEVEL N**, right = **RESET**. Readable at a glance?
- **Win popup:** stars, move count, "Signal clean: YES/NO". Confusing or redundant?
- **Level select:** stars + lock state — clearly showing progress?
- Any moment you didn't know what a control did?

### 3.4 Colourblind aid — NOW REACHABLE
A **colourblind toggle** exists: when ON, each orb shows a **tier glyph / number overlay (T1–T5)** so tiers are distinguishable without hue. (Earlier builds had no in-game switch — **this build exposes it** via the options/settings area; if you can't find it, tell us and we'll surface it more prominently.)
- Play normally (OFF) and tell us: how hard is it to tell tiers/colors apart by hue alone?
- If you find the toggle: do the T1–T5 glyphs make tiers clear?

---

## 4. Known rough edges (so you don't think they're your fault)
- **Win popup buttons** may need a second tap on device (raw-touch fallback added, not fully hardware-verified). Tap again if ignored; tell us.
- **No sound effects yet** for merge/mix/win in this build — vibration + visuals carry feedback. Tell us if the silence hurts clarity. *(Audio pass is the next planned iteration if critics want it.)*
- **L8 Still Waters** is intentionally low-drama; your read on whether the breather works matters most.
- **Intro animation** replays each level entry; tell us if it gets old.

---

## 5. Feedback questionnaire (5 min, honest please)
1. **First 30s:** immediate impression? Did you "get it" quickly?
2. **Merge act (L1–L3):** did dragging two orbs together feel good? Rate 1–5.
3. **Big reveal (L2 cascade / L5 T5 / L7 mix):** strongest "oh, nice" moment? Which gave nothing?
4. **L7 color mixing:** did you understand *why* Cyan+Magenta→Blue? Read clearly or like a bug?
5. **L8 Still Waters:** did the calm breather work, or feel like a non-event? (1 = dead, 5 = perfect pacing)
6. **Juice:** which feedback helped most (particles / scale-pop / shake / vibration / confetti)? Any annoying?
7. **Colourblind / clarity:** how clear were tiers/colors? How should the toggle be exposed?
8. **UI:** anything on top bar / win popup / level select unclear or redundant?
9. **Difficulty curve:** any cliff or grind? Which level?
10. **Would you play the next 10 levels?** Yes / Maybe / No — and why.

---

## 6. How to send feedback
- Drop notes in the shared critic thread / email back. Screenshots or a 10s screen recording of a confusing moment are *extremely* useful.
- Bugs: **level number**, **what you did**, **what happened (or didn't)**.

Thank you — this build is rough in places by design; we'd rather hear "this part is unclear" than have you struggle in silence.
