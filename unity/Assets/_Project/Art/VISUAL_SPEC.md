# Chroma Vale — L1–L10 Vertical-Slice Visual Spec (game-artist, t_aa6a0221)

> Canonical style: **Cozy Casual** (DESIGN_CANON v2.3.0 + Art_Style_Guide v2.0.0).
> NOT cyberpunk/neon — that direction in the task brief is superseded by the canon.
> Brown deferred to Act IV (t_4d577980) — no Brown in L1–L10.

## Style Anchors
- Palette: Cream #F8F4E8, Lavender #E8D5F0, White #FFFFFF, Dark Teal #2D6B6B (text),
  Muted Gray-Teal #6A9A9A, Coral #FF6B6B (primary/accent), Bright Teal #4ECDC4 (success),
  Warm Gold #FFD93D (stars), Soft Green #6BCB77.
- Orbs = soft-gel "Lumlings" with faces; tier via scale + eye-state + glow (not hue alone).
- Shadows warm (rgba(0,0,0,0.10)), rounded corners everywhere, no dark modals.

## Asset Manifest (PNG, transparent unless noted)
### Orbs/ (pre-existing, canonical — verified, not regenerated)
6 colors × T1–T5, Brown T1. Tier ramp present (sleepy→star-eyes, glow→prismatic).

### Board/ (5)
- board_bg.png (square, solid gradient) — cream→lavender, faint octagon grid, center glow.
- cell_idle.png / cell_selected.png (teal halo) / cell_validdrop.png (teal fill) — 256² tiles.
- queue_tray.png (wide) — next-orb tray, 3 slots.

### UI/ (8)
- hud_top.png (wide) — level plate + 3 stars + moves/par.
- btn_restart.png / btn_hint.png — round white icon buttons (coral arrow / teal bulb).
- btn_primary.png (wide) — coral CTA pill.
- win_popup.png (tall, transparent) — level-complete card, 3 stars, 2 buttons, confetti.
- icon_star.png / icon_coin.png / icon_leveldot.png — HUD glyphs.

### FX/ (4)
- fx_mergeburst.png — merge radial burst.
- fx_levelcomplete.png — win confetti bloom.
- fx_confetti.png / fx_sparkle.png — particle sheets.

### HookConcepts/ (10) — one per level, Brown-FREE
L1 first-merge · L2 chain · L3 invalid-mix (cyan+purple, red warning) · L4 mix-reveal
(cyan+magenta→purple) · L5 branching · L6 mix+tier · L7 double-chain · L8 Still Waters
(calm teal ripple breather) · L9 scarcity (lonely glowing orb) · L10 convergence (max-juice).

## Feedback Wiring (builder)
- Merge → fx_mergeburst + DOTween scale-pop.
- Invalid → coral warning ring (hook_L3) + shake.
- Win → win_popup (telegraphed) + fx_levelcomplete + 3-star + fly-to-coin.

## Tier Escalation Lock
Scale 0.80→1.30× · emission 0→8 · eye-state sleepy→awake · detail gel→crack→rainbow.
(See Art_Style_Guide §4.2 / §4.2.1.)
