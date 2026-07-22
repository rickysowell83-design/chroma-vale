# Chroma Vale — Comprehensive Game Design & Business Plan

> **Status:** Phase 1–4 Complete | **Date:** July 2026
> **Role:** Lead Game Design Director
> **Target:** Mobile-first (iOS + Android), with desktop stretch goal

---

## Executive Summary

**Chroma Vale** is a color-sorting pipeline puzzle game wrapped in a cozy world-restoration meta-layer. Players route chromatic energy through increasingly complex pipeline networks to restore life and color to a grayscale world. It sits at the intersection of three explosive market trends: **Sort Puzzles** (fastest-growing puzzle subgenre, 13% of Puzzle YoY revenue growth), **Merge-2 narrative meta** (highest ARPDAU in mobile puzzle at $0.31), and **Hybridcasual** (fastest-growing monetization model).

**One-liner:** *Mini Metro meets Hexa Sort meets Stardew Valley's heart.*

**Target metrics (Year 1):**
- 500K–1M installs (organic-first, paid scaling in months 6–12)
- D30 retention: 12–15%
- IAP ARPDAU: $0.08–$0.15 (hybridcasual range)
- Ad ARPDAU: $0.03–$0.06 (rewarded + interstitial)
- Year 1 net revenue target: $150K–$400K

---

## Phase 1: Market Research & Trend Analysis

### 1.1 Macro Market Snapshot (2025–2026)

| Metric | Value | Source |
|--------|-------|--------|
| Global mobile game revenue (2026 projected) | ~$134B | Statista |
| Mobile puzzle IAP revenue (2025) | $10B+ (+14% YoY) | Sensor Tower |
| Puzzle genre downloads (2025) | 9.7B | Sensor Tower |
| Match-3 market size (2025) | $12.8B, projected $28.6B by 2034 (9.3% CAGR) | MarketIntelo |
| Top grossing puzzle game (2025) | Royal Match: $1.4B IAP | Sensor Tower |
| #1 downloaded game globally (Jan 2026) | Block Blast! (HungryStudio) | Sensor Tower |
| India monthly downloads (Jan 2026) | 607M (14.4% of global) | Sensor Tower |
| SEA mobile gaming market | $7B+ by 2027 | Niko Partners |

### 1.2 The Great Puzzle Reordering

This is the single most important trend shaping our strategy. The data from AppMagic (July 2026) is unambiguous:

**The Old Guard is Losing Ground:**
- Match-3 + Merge + Blast dropped from **95% → 80%** of Puzzle IAP revenue share in a decade
- Their combined download share collapsed from **75% → under 25%**
- Match-3 itself contributed **zero** to Puzzle's +14% YoY revenue growth in 2025

**Where Growth Actually Comes From (2025 YoY Revenue Contribution):**
| Subgenre | Contribution | Key Titles |
|----------|-------------|------------|
| **Merge** | 60% | Gossip Harbor ($650M), Seaside Escape |
| **Sort** | 13% | Magic Sort, Pixel Flow, Hexa Sort |
| **Block** | 13% | Color Block Jam, Block Blast |
| **Screw** | 8% | Screwdom, Screw Jam |

**The Sort Explosion:**
- Sort surpassed Blast to become the **3rd largest Puzzle subgenre by IAP revenue** in early 2026
- Each new Sort mechanic creates its *own* market rather than cannibalizing existing ones (Block Jam 3D → Hexa Sort → Pixel Flow all coexist and grow)
- Design space remains wide open — unlike Merge-2 which is consolidating around top titles

### 1.3 Hybridcasual: The Winning Model

- Hybridcasual is the **fastest-growing monetization model** (Sensor Tower 2026)
- Combines hypercasual's low-friction onboarding with casual/midcore's retention and monetization depth
- Key pattern: Simple core mechanic → deep meta-layer → IAP + Ad hybrid monetization
- Former hypercasual publishers (VOODOO, Rollic, Learnings) are pivoting hard into this space

### 1.4 Monetization Benchmark Data

**Top Puzzle Games ARPDAU (2025):**
| Game | IAP ARPDAU | Model |
|------|-----------|-------|
| Gossip Harbor | $0.31 | Pure IAP (Merge + Narrative) |
| Royal Match | $0.17 | Pure IAP (Match-3) |
| Candy Crush Saga | $0.11 | IAP + Ads |

**Key Insight:** Gossip Harbor's narrative layer drives nearly 2x the ARPDAU of pure puzzle games. The meta matters.

**Winning Monetization Strategies (2026):**
1. **Hybrid monetization** (IAP + Rewarded Ads + optional Subscription) — monetizes all segments
2. **Cosmetic-first IAP** — skins, themes, visual customization (no pay-to-win backlash)
3. **Battle pass / seasonal content** — recurring revenue + daily engagement driver
4. **Personalized offers** — player-segmented bundles based on behavior
5. **D2C web shops** — growing 26% YoY, bypassing 30% platform fees

### 1.5 User Acquisition Landscape

- Global CPC average: **~$2.57** (USA: $4.22)
- A 4-person indie studio reported spending **$100K+/month** on UA to stay competitive in F2P (Reddit r/gamedev, 2026)
- **56 of top 100 grossing games** use AI-generated ad creatives — now table stakes
- Organic discovery is brutal: **only ~10%** of 1.4M+ new apps in 2025 captured any user attention
- **The winning indie approach:** Build in public (Reddit, TikTok, X/Twitter), leverage content creators, optimize ASO aggressively, and use small paid tests ($1K–$5K) to validate CPI before scaling

### 1.6 Emerging Market Opportunity

- **India:** #1 in global downloads (607M/month), mobile gaming projected $2.4B by 2029
- **Southeast Asia:** $7B+ mobile gaming market by 2027, mobile-first population
- **Key considerations:** Smaller APK size (<100MB critical), offline-capable, low-end device support, localized pricing

---

## Phase 2: Game Conceptualization — Chroma Vale

### 2.1 The 30-Second Pitch

> The world of Chroma Vale has lost its color. As the last Chroma Keeper, you sort and route living color energy through intricate pipeline puzzles to restore vibrant life to a charming grayscale world. Each solved puzzle blooms a new region into color — revealing characters, stories, and ever-deepening puzzle mechanics.

### 2.2 Core Gameplay Loop

```
┌──────────────────────────────────────────────────┐
│                 CORE LOOP (30 sec)                │
│                                                   │
│   Color orbs spawn ──→ Route through pipelines ──→ Match at target nodes
│         ↑                                              │
│         │                                              ↓
│   New orbs spawn                              Score + Progress
│         │                                              │
│         └────────── Rewards ──────────────────────────┘
│                           │                           │
│                     Coins / Stars / Keys               │
│                           ↓                           │
│              RESTORATION META (5 min loop)             │
│              Spend resources to restore regions        │
│              Unlock characters, story, new mechanics   │
└──────────────────────────────────────────────────┘
```

**Session-level loop:**
1. Player opens app → sees grayscale world map with glowing "restorable" nodes
2. Taps a node → enters pipeline puzzle level
3. Completes puzzle → earns Chroma Stars + Coins
4. Returns to map → spends stars to restore a building/area
5. Restored area blooms into color → reveals story snippet, new character, or unlocks new puzzle type
6. New levels/challenges appear → loop repeats

### 2.3 Core Mechanic: "Chroma Flow" (Pipeline Sort)

The core mechanic is a **spatial color-sorting pipeline puzzle:**

- **Sources:** Color orbs spawn at entry points on a grid-based board
- **Paths:** Player draws/traces pipeline segments connecting sources to matching-color targets
- **Constraints:** Limited pipe pieces, obstacles (rocks, locked gates), color-mixing junctions, timed flows
- **Win condition:** Route all orbs to their matching targets before moves/time run out
- **Scoring:** Efficiency bonuses (fewest moves, fastest time, longest chain)

**Mechanic progression (by world/chapter):**

| World | New Mechanic | Source of Fun |
|-------|-------------|---------------|
| 1: Meadow | Basic color-to-target routing | Simplicity, satisfaction of completion |
| 2: Riverlands | Flow direction (one-way pipes) | Spatial planning |
| 3: Canyon | Color mixing (blue + yellow = green node) | Combinatorial thinking |
| 4: Caverns | Timed gates, moving obstacles | Urgency + precision |
| 5: Sky Isles | Multi-layer boards (3D stacking) | Depth, mastery |
| 6+: Endless | Infinite mode + daily challenges | Replayability, competition |

### 2.4 Art Style & Visual Identity

**Style:** Cozy pixel art with a unique "chromatic awakening" visual hook.

- **Grayscale base:** The world starts in beautiful monochrome pixel art (think: detailed Game Boy aesthetic)
- **Color blooms:** Restored areas burst into vibrant, saturated 16-bit-inspired color
- **Juice:** Particle effects on color matches, screen-shake on combos, smooth color-bleed transitions on the world map
- **Character design:** Charming animal characters (Stardew Valley meets Night in the Woods)
- **UI:** Clean, minimal, heavy use of color as information (colorblind mode essential)

**References:**
- Mini Metro (clean pipeline aesthetic)
- Stardew Valley (cozy pixel art, character charm)
- Gris (color-as-progression visual storytelling)
- Monument Valley (satisfying spatial puzzle feel)

### 2.5 Target Platforms

| Platform | Priority | Rationale |
|----------|----------|-----------|
| **iOS** | Tier 1 | Higher ARPDAU, launch platform |
| **Android** | Tier 1 | Volume (especially India/SEA), simultaneous launch |
| **Desktop (Steam)** | Tier 2 | Stretch goal — puzzle games perform well on Steam; port after mobile validation |
| **Web (HTML5)** | Marketing | Playable ad / demo version for UA campaigns |

### 2.6 Competitive Positioning

**Direct competitors (Sort + Pipeline):**
- *Hexa Sort* — closest comp for core mechanic; we differentiate with narrative meta and pipeline routing depth
- *Mini Metro* — aesthetic and routing inspiration; we add progression and monetization
- *Pixel Flow* — conveyor + art meta; we're more puzzle-forward with deeper mechanics

**Indirect competitors (meta-layer):**
- *Gossip Harbor* — narrative Merge-2 with high ARPDAU; we're lighter, more accessible, different core mechanic
- *Merge Mansion* — mystery narrative meta; we're cozier, less aggressive monetization

**Our moat:** Pipeline routing is mechanically distinct from both Sort (container-based) and Merge (pair-based). The grayscale-to-color visual hook is highly marketable in UA creatives.

---

## Phase 3: Design, Build, and Deploy Plan

### 3.1 Tech Stack

**Primary Engine: Unity 6 (LTS) with Universal Render Pipeline (URP)**

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **Engine** | Unity 6 LTS + URP | Largest mobile ecosystem, best ad SDK support (IronSource/LevelPlay, AdMob), proven 60fps on low-end devices |
| **Language** | C# (.NET 8) | Industry standard for Unity |
| **2D Rendering** | URP 2D Renderer + Sprite Shape | Efficient 2D with GPU-accelerated particles for juice |
| **Architecture** | Layered: UI → Domain → Data (see §3.2) | Testable, maintainable, AI-agent-friendly |
| **DI/IoC** | VContainer (Zenject successor) | Lightweight, AOT-friendly, Unity-native |
| **State Management** | UniRx (Reactive Extensions) | Reactive data binding between domain and UI |
| **Analytics** | PostHog (self-hosted or cloud) | Open-source, game-friendly event taxonomy, session recording |
| **Ad Mediation** | IronSource LevelPlay | Industry standard, rewarded video + interstitial + banner |
| **IAP** | Unity IAP (cross-platform) | Apple + Google store integration |
| **CI/CD** | GitHub Actions + Unity Cloud Build | Automated builds for iOS + Android |
| **Version Control** | Git (GitHub) | With Git LFS for assets |

**Why not Godot 4.6?** Godot is excellent but the ad mediation and analytics ecosystem isn't as mature. For a monetization-dependent mobile game, Unity's SDK support saves weeks of integration work.

**Why not Flutter/React Native?** These are UI frameworks, not game engines. For a puzzle game with particle effects, sprite animations, and smooth 60fps rendering, a real game engine is mandatory.

### 3.2 Architecture: Clean Layered Design

```
Assets/
├── _Project/
│   ├── Core/                  # Engine-agnostic domain logic
│   │   ├── GameLogic/         # Puzzle rules, pipe routing, scoring
│   │   ├── Progression/       # Level system, unlocks, economy
│   │   └── Meta/              # World restoration, story state
│   ├── Domain/                # Unity-specific domain services
│   │   ├── PuzzleBoard/       # Board state, pipe placement, validation
│   │   ├── WorldMap/          # Map node state, region restoration
│   │   └── Player/            # Inventory, currency, settings
│   ├── Infrastructure/        # Data & external services
│   │   ├── Persistence/       # Save/Load (JSON + binary)
│   │   ├── Analytics/         # PostHog event bridge
│   │   ├── Monetization/      # IAP + Ad abstraction
│   │   └── Audio/             # FMOD or Unity Audio mixer
│   ├── Presentation/          # UI & Visual layer
│   │   ├── Views/             # UI screens (MVP pattern)
│   │   ├── Components/        # Reusable UI widgets
│   │   └── Animations/        # DOTween sequences, timeline
│   └── Shared/                # Cross-cutting
│       ├── Extensions/        # C# extension methods
│       ├── Constants/         # Game balance, tuning values
│       └── Utilities/         # Math, helpers
```

**Key architectural rules (for AI coding agents via AGENTS.md):**
1. Core/ has ZERO Unity dependencies — pure C# logic, fully unit-testable
2. Domain/ depends on Core/ and Unity APIs only
3. Presentation/ never touches data directly — always through Domain/ services
4. All IAP and Ad calls go through Monetization/ abstractions (never call SDKs directly)
5. All analytics events go through Analytics/ bridge (never call PostHog directly)

### 3.3 Asset Pipeline

| Asset Type | Tool | Approach |
|-----------|------|----------|
| **Pixel Art (characters, tiles)** | Aseprite | Custom — small scope, high impact. ~30 characters, ~200 tile variants |
| **UI (icons, buttons, frames)** | Figma → Unity UI Toolkit | Custom — consistent design system |
| **Background illustrations** | Photoshop / Procreate | Custom key art + procedural grayscale-to-color shader |
| **Sound Effects** | BFXR + custom recording | 80% generated, 20% foley |
| **Music** | Licensed (PremiumBeat / Artlist) or custom composer | Adaptive layers that bloom with color |
| **Particle Effects** | Unity VFX Graph (URP) | Custom — color burst, sparkle, flow trails |
| **Font** | Google Fonts (open source) | e.g., Nunito for UI, pixel font for in-game |

**Estimated asset scope (MVP):** ~150 unique sprites, ~20 character portraits, ~8 music tracks (layered), ~30 SFX. Fully achievable by 1 artist in 2 months.

### 3.4 Milestone Development Plan

```
MONTH 1-2: CORE MVP — "Is it fun?"
├── Week 1-2:  Project setup, architecture, CI/CD
│   - Unity project + URP configuration
│   - Folder structure + DI container setup
│   - AGENTS.md for AI coding agent rules
│   - GitHub repo + Git LFS + Unity Cloud Build
│   - PostHog SDK integration
├── Week 3-5:  Core mechanic prototype
│   - Grid-based board system
│   - Color orb spawning + basic routing
│   - Pipe placement + validation
│   - Win/lose conditions
│   - 5 hand-crafted tutorial levels
│   - Playtest with 5 friends — is the core loop satisfying?
├── Week 6-8:  First playable
│   - 20 levels across World 1 (Meadow)
│   - Basic UI (level select, HUD, pause)
│   - Placeholder pixel art (programmer art)
│   - Save/load system
│   - Crashlytics + basic error reporting
│   - Playtest with 20 external testers
│   - GATE: D1 retention > 40%, fun rating > 7/10
│
MONTH 3: PROGRESSION + META — "Will they come back?"
├── Week 9-10: World map + restoration meta
│   - Grayscale world map with restorable nodes
│   - Chroma Star economy (earn → spend → restore)
│   - First 3 characters + dialogue system
│   - Color-bloom transitions on map
├── Week 11-12: Progression depth
│   - Worlds 1-2 complete (40 levels)
│   - Unlock system (new mechanics gated by progress)
│   - Daily challenge mode (1 new puzzle/day)
│   - Soft currency + basic shop
│   - GATE: D7 retention > 20%
│
MONTH 4: MONETIZATION + POLISH — "Will it make money?"
├── Week 13-14: Monetization integration
│   - IronSource LevelPlay integration (rewarded + interstitial)
│   - Unity IAP (coin packs, starter bundle, remove ads)
│   - Optional: Battle pass (monthly subscription)
│   - Economy balancing spreadsheet
├── Week 15-16: Polish sprint
│   - Replace all programmer art with final pixel art
│   - Sound effects + music integration
│   - Juice pass: particles, screen shake, tweening
│   - Accessibility: colorblind mode, text sizing, haptic feedback
│   - Performance: target 60fps on iPhone 8 / Galaxy S9
│   - GATE: D14 retention > 15%, crash-free rate > 99.5%
│
MONTH 5: SOFT LAUNCH — "Will the market care?"
├── Week 17-18: Soft launch prep
│   - App Store Connect + Google Play Console setup
│   - ASO: keywords, screenshots, description copy
│   - Privacy policy, terms of service
│   - Localization prep (English + 3 key markets: Portuguese, Hindi, Japanese)
├── Week 19-20: Soft launch (Canada + Philippines)
│   - Limited geo release
│   - Small paid UA test ($2K budget)
│   - Monitor: CPI, D1/D7/D30 retention, IAP conversion, ad LTV
│   - Gather reviews + bug reports
│   - GATE: CPI < $1.50, D7 retention > 15%, bugs under control
│
MONTH 6: GLOBAL LAUNCH
├── Week 21-22: Launch fixes + content
│   - Address soft launch findings
│   - Worlds 3-4 (40 more levels)
│   - Launch event: limited-time "Founder's Festival"
├── Week 23-24: Global launch
│   - Worldwide App Store + Google Play release
│   - PR push + creator outreach
│   - Paid UA scaling (start $5K/month, scale with ROAS)
│   - LiveOps cadence begins: weekly events, biweekly content updates
```

### 3.5 Analytics Plan (PostHog)

**Critical metrics to track from Day 1:**

| Category | Metrics | Why |
|----------|---------|-----|
| **Acquisition** | Installs by source (organic/paid/country), CPI by channel | Know where players come from and what they cost |
| **Engagement** | DAU, MAU, session length, sessions/day | Health of the active player base |
| **Retention** | D1, D7, D14, D30 rolling retention | THE most important metric. D1 < 35% = core loop broken. D7 < 10% = meta not working |
| **Monetization** | IAP ARPDAU, Ad ARPDAU, conversion rate (free→payer), LTV by cohort | Revenue health and cohort quality |
| **Progression** | Level completion rate, level fail rate, churn-by-level, economy velocity (earn vs spend) | Balance tuning and churn detection |
| **Technical** | Crash rate, ANR rate, load times, memory warnings | Quality bar |

**Key events taxonomy:**
```
level_start(level_id, world_id)
level_complete(level_id, moves_used, stars_earned, time_seconds)
level_fail(level_id, fail_reason, moves_remaining)
region_restore(region_id, stars_spent)
purchase_start(product_id, price)
purchase_complete(product_id, revenue)
ad_watch_start(placement)
ad_watch_complete(placement, reward_type)
session_start(day_number, total_sessions)
```

---

## Phase 4: Business Setup, Monetization, and Launch

### 4.1 Business Setup Requirements

| Item | Cost | Timeline | Notes |
|------|------|----------|-------|
| **Apple Developer Program** | $99/year | 1–2 weeks | Required for App Store publishing. D-U-N-S number needed for organization account |
| **Google Play Console** | $25 (one-time) | 1–3 days | Developer account registration |
| **LLC / Business Registration** | $100–$800 | 2–4 weeks | Depends on jurisdiction. Recommended for liability protection and payment processing |
| **Privacy Policy + ToS** | $0–$500 | 1 week | Use generator (Termly, iubenda) or lawyer. MUST comply with GDPR/CCPA |
| **App Store Screenshots** | $0–$300 | 1 week | DIY with Figma or hire ASO specialist |
| **Domain + Landing Page** | $15/year + hosting | 1 day | chromavale.com — essential for press kit and D2C web shop later |

**Total setup cost: ~$250–$1,700**

### 4.2 Monetization Strategy (Hybrid Model)

```
                     PLAYER SEGMENTS
    ┌─────────────────┬──────────────────┬─────────────────┐
    │   FREE (85%)    │  CASUAL (12%)    │   WHALES (3%)   │
    ├─────────────────┼──────────────────┼─────────────────┤
    │ Rewarded Ads    │ Starter Bundle   │ Battle Pass     │
    │ Interstitials   │ Coin Packs       │ Premium Bundles │
    │                 │ Remove Ads IAP   │ Cosmetics       │
    │                 │                  │ Exclusive Skins │
    └─────────────────┴──────────────────┴─────────────────┘
```

**Revenue Streams (priority order):**

1. **Rewarded Video Ads** (foundation)
   - Placement: Extra moves (mid-level), double rewards (end-of-level), daily bonus multiplier, free coin pack
   - Expected: $0.03–$0.06 ARPDAU, ~40–60% of total ad revenue

2. **Interstitial Ads** (secondary)
   - Placement: Between levels (every 3–4 completions), natural breaks only
   - Frequency capped: Max 1 per 3 minutes
   - Expected: $0.01–$0.02 ARPDAU

3. **In-App Purchases** (growth driver)
   - **Starter Bundle** ($4.99): 500 coins + 50 Chroma Stars + exclusive character skin
   - **Coin Packs** ($1.99 / $4.99 / $9.99): Soft currency for boosters and cosmetics
   - **Remove Ads** ($5.99): Permanent ad removal + daily coin bonus
   - **Chroma Stars Pack** ($2.99 / $7.99): Premium currency for faster region restoration
   - **Cosmetic Skins** ($0.99–$2.99 each): Pipe styles, color themes, character outfits

4. **Season Pass / Battle Pass** (recurring revenue)
   - **Free track:** Basic rewards to keep players engaged
   - **Premium track** ($4.99/month): Exclusive cosmetics, bonus stars, unique pipe style
   - Season duration: 4 weeks
   - Target: 3–5% of DAU on premium track

5. **D2C Web Shop** (Phase 2 — after global launch)
   - Sell coin/star bundles directly via web at 10% discount (bypass 30% platform cut)
   - Synced with game account via player ID
   - Expected: 10–15% of total IAP revenue once established

**Economy Balance Principles:**
- All content completable without spending (no hard paywalls)
- Spend accelerates, doesn't gate
- Cosmetic-only purchases for competitive integrity
- Generous free currency economy (feels abundant early, tightens naturally mid-game)

### 4.3 User Acquisition & Marketing Plan

**Phase 1: Pre-launch (Months 1–5) — Build in Public**

| Channel | Activity | Frequency | Goal |
|---------|----------|-----------|------|
| **TikTok** | Dev-log videos: pixel art timelapses, mechanic reveals, "grayscale → color" transitions | 3–4x/week | Build anticipation, test UA creative concepts |
| **Reddit** (r/IndieDev, r/Unity2D, r/PixelArt, r/CozyGames) | Development updates, GIFs of satisfying puzzle solves, art showcases | 2x/week | Community building, early wishlist signups |
| **X/Twitter** | Daily dev-log threads, engage with #indiedev #pixelart #gamedev communities | Daily | Network effects, press connections |
| **YouTube Shorts** | Repurpose TikTok content, 60-second "how it works" videos | 2x/week | Secondary content pipeline |
| **Discord** | Community server for playtesters, feedback, early access | Always-on | Retain superfans, source beta testers |
| **Email List** | Landing page signup via chromavale.com | Continuous | Owned audience for launch day |

**Phase 2: Soft Launch (Month 5)**

| Activity | Budget | Goal |
|----------|--------|------|
| Apple Search Ads (Canada) | $500 | Test CPI + conversion on App Store |
| Google UAC (Philippines) | $500 | Test Android CPI in emerging market |
| TikTok Spark Ads (test creatives) | $500 | Identify best-performing creative angles |
| Reddit Ads (r/CozyGames, r/Puzzle) | $500 | Niche community targeting |

**Total soft launch UA budget: $2,000**

**Phase 3: Global Launch (Month 6+)**

| Channel | Monthly Budget | Strategy |
|---------|---------------|----------|
| Google UAC (tROAS) | $3,000–$10,000 | Scale based on D7 ROAS > 80% |
| Apple Search Ads | $1,000–$3,000 | Keyword defense + discovery |
| TikTok / Meta (AEO) | $1,000–$5,000 | Scale winning creatives from soft launch |
| Content Creator Outreach | $0–$2,000 | Free keys + small sponsorships for cozy game YouTubers/streamers |
| ASO Iteration | $0 | Continuous keyword optimization, screenshot A/B testing |

**Total monthly UA budget at launch: $5,000–$20,000 (scale with ROAS)**

**Key UA Creative Angles (based on market research):**
1. **"Grayscale → Color" transition** — The visual hook. Show a dull board bursting into color. AI-generate variations.
2. **"Satisfying puzzle solve"** — Close-up of a complex pipeline clicking into place. ASMR-style.
3. **"Restore the world"** — Before/after of a region on the world map. Stardew Valley nostalgia.
4. **"Can you solve this?"** — Fail state → challenge the viewer. High engagement bait.
5. **Character moments** — Cute character reactions to color restoration. Emotional hook.

### 4.4 Launch Checklist

**Pre-launch (2 weeks before):**
- [ ] App Store Connect: app record, screenshots, description, keywords
- [ ] Google Play Console: store listing, feature graphic, tags
- [ ] Privacy policy URL live
- [ ] TestFlight / Internal Testing build distributed
- [ ] Press kit at chromavale.com/press
- [ ] Launch trailer (60 sec) finalized
- [ ] Social media posts scheduled for launch week
- [ ] 20 content creators contacted with early access codes
- [ ] PostHog dashboards configured
- [ ] Crash monitoring alert thresholds set
- [ ] Customer support email + FAQ page ready

**Launch day:**
- [ ] Release on App Store + Google Play
- [ ] Social media blast across all channels
- [ ] Reddit post on r/IndieDev, r/CozyGames, r/AndroidGaming, r/iOSGaming
- [ ] Discord @everyone announcement
- [ ] Email blast to mailing list
- [ ] Monitor: crash rate, server load, review velocity

**Post-launch (first 30 days):**
- [ ] Respond to ALL app store reviews (first 48 hours critical)
- [ ] Daily analytics review: retention, monetization, crashes
- [ ] First content update (Week 2): new levels + limited-time event
- [ ] Evaluate UA performance: scale winners, cut losers
- [ ] Plan next content drop based on level completion data (where do players churn?)

### 4.5 Financial Projections (Conservative)

**Year 1 (Months 1–6 dev + Months 7–12 live):**

| Period | Installs | DAU (avg) | IAP Revenue | Ad Revenue | Total Revenue |
|--------|----------|-----------|-------------|------------|---------------|
| Dev (M1-6) | — | — | — | — | -$50K (cost) |
| Launch (M7-8) | 50K | 3,000 | $8K | $3K | $11K |
| Growth (M9-10) | 150K | 8,000 | $25K | $10K | $35K |
| Scale (M11-12) | 300K | 15,000 | $55K | $22K | $77K |
| **Year 1 Total** | **500K** | — | **$88K** | **$35K** | **$123K** |

*Assumptions: $0.10 blended ARPDAU, 5% IAP conversion, CPI ~$1.50, organic/paid split 60/40*

**Breakeven target: Month 10** (at $50K total development cost for a solo/small-team build)

---

## Risk Assessment & Mitigation

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Core mechanic isn't fun | Medium | Critical | Prototype in first 3 weeks. Kill early if playtesters don't smile. |
| UA costs too high | High | High | Organic-first strategy. Build community before spending. Validate CPI in soft launch before scaling. |
| Copycat clones | High | Medium | Strong brand/art identity. First-mover advantage in pipeline-sort niche. Continuous content updates. |
| Apple/Google policy changes | Low | Medium | Stay informed. Diversify to D2C web shop. |
| Retention below target | Medium | High | Iterate on meta-layer. Add social features (friend leaderboards, gifting). Run more events. |
| Burnout (solo dev risk) | High | High | Realistic scope. External accountability. Build community for motivation. |

---

## Appendix: Reference Games & Inspiration

| Game | What We're Stealing (With Pride) |
|------|----------------------------------|
| **Mini Metro** | Clean pipe-drawing UX, minimalist satisfaction |
| **Hexa Sort** | Auto-sort mechanic, board management tension |
| **Gossip Harbor** | Narrative meta driving high ARPDAU |
| **Stardew Valley** | Cozy pixel art, character warmth, progression satisfaction |
| **Gris** | Color-as-progression visual storytelling |
| **Pixel Flow** | Conveyor mechanic, visual distinctiveness |
| **Block Blast!** | Pure-ad-monetization mastery, mass appeal |
| **Monument Valley** | Elegant puzzle design, premium feel |

---

*Document prepared by Director (Hermes Agent, game-director profile) — July 2026*
*All market data sourced from Sensor Tower, AppMagic, Naavik, Singular, and Statista reports as cited.*
