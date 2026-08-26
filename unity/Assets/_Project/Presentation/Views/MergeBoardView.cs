// SPDX-License-Identifier: MIT
// Chroma Vale — MergeBoardView: Unity presentation layer for merge-2 + color-mixing mode.
// Wires MergeLevelRepository → BoardController → sprite rendering + touch input.
// This is a STARTING STUB — verify, fix, and complete against the card spec.
//
// ═══════════════════════════════════════════════════════════════════════════
// DESIGN CONSTANTS (from @game-designer — lock these, do not guess)
// ═══════════════════════════════════════════════════════════════════════════
//
// Animation timing:
//   Merge:  8 frames × 80ms = 640ms total (squash → flash → settle)
//   Spawn:  8 frames × 60ms = 480ms total (scale-in + overshoot settle)
//   Snap-back: 4 frames × 60ms = 240ms total (quick "nope" — shrink+fade back)
//   Target idle pulse: 4 frames × 120ms = 480ms loop (scale 1.0→1.05→1.0→0.98→loop)
//   Target lock flash: 4 frames × 50ms = 200ms total (green ring expand+fade)
//   Flash color = RESULT orb color, not input
//
// Touch input model (drag-to-draw):
//   Player DRAGS from one orb to an adjacent orb to attempt a merge.
//   - Dragged orb follows finger (visual only — board state unchanged until release)
//   - Target orb highlights on hover (adjacent cells only)
//   - Invalid merge (no rule match): snap-back animation to original position
//   - Valid merge: drag orb animates into target → merge strip plays → new orb spawns
//
// Board feedback:
//   Restoration targets pulse subtly (idle state) so player sees where orbs need to go.
//   When a correct (color, tier) orb is placed on a target: target locks with green flash.
//
// UI animation strips (from @game-artist — 3 strips, not per-color):
//   snap_back_strip.png          — 256×1024, 4 frames, neutral white
//   target_idle_pulse_strip.png  — 256×1024, 4 frames, loopable, neutral white
//   target_lock_flash_strip.png  — 256×1024, 4 frames, green-tinted
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using UnityEngine;
using UnityEngine.InputSystem;
using ChromaVale.Domain.Progression;
using ChromaVale.Infrastructure.Audio;
using ChromaVale.Infrastructure.LevelData;
using TMPro;
using UnityEngine.UI;
using ChromaVale.Presentation.UI;
using DG.Tweening;
using ChromaVale.Presentation.Views.Components;

namespace ChromaVale.Presentation.Views
{
    /// <summary>
    /// Unity view for merge-mode gameplay. Replaces PuzzleBoardView (pipe-routing)
    /// for Chroma Vale's merge-2 + color-mixing puzzle.
    /// </summary>
    public class MergeBoardView : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private float _tileSize = 1.2f;
        [SerializeField] private int _startLevel = 1;

        [Header("Prefabs")]
        [SerializeField] private GameObject _orbPrefab; // Orb_T1.prefab — SpriteRenderer

        // Sprite cache: artist orb art per (color, tier). Loaded lazily and cached;
        // null entries mean "no art for this combo" → procedural circle fallback.
        private readonly Dictionary<(OrbColor, int), Sprite> _orbSpriteCache = new();

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI _hudText;
        private Button _resetButton;

        // ── Design Constants (locked by @game-designer) ──
        private const float MergeAnimDuration   = 0.640f; // 8 frames × 80ms
        private const float SpawnAnimDuration   = 0.480f; // 8 frames × 60ms
        private const float SnapBackDuration    = 0.240f; // 4 frames × 60ms
        private const float TargetPulseDuration = 0.480f; // 4 frames × 120ms (loop)
        private const float TargetLockDuration  = 0.200f; // 4 frames × 50ms
        private const float HoverScaleFactor    = 1.15f;  // hover highlight scale multiplier

        // ── Dependencies (set in Awake/Start) ──
        private MergeLevelRepository _levelRepo;
        private IBoardController _board;

        // ── Grid state ──
        private LevelData _level;
        private int _levelNumber;
        private DuskfallVignette _duskfallVignette;
        private int _maxLevel;
        private float _boardOffsetX;
        private float _boardOffsetY;
        private GameObject[,] _gridTiles;

        // ── Orb visuals ──
        // Maps grid position → OrbVisual instance for each orb on the board
        // (GlassOrb shader: MeshRenderer + MaterialPropertyBlock — no material instances)
        private Dictionary<(int x, int y), OrbVisual> _orbVisuals = new();

        // ── Input state ──
        private bool _wasPointerDown;          // Pointer pressed last frame (edge detection)
        private Vector2 _lastPointerScreenPos; // Latest pointer position (screen space)
        private (int x, int y)? _hoverCell;    // Cell currently highlighted by hover
        private Coroutine _snapBackCoroutine;
        private GameObject _snapBackOrb;        // survives nulling of _draggedOrbVisual (Bug 2: double-click fade)
        private Vector3 _draggedBaseScale = Vector3.one;
        private Sprite _lockFlashSprite;

        // ── Restoration payoff ──
        // The region background starts desaturated (the vale has lost its color) and
        // blooms back to full saturation when the level completes.
        private ParticleFxService _particleFx;
        private SpriteRenderer _regionBackground;
        private Sprite _regionBgSprite;
        private const float RestorationDesatDuration = 0.35f;
        private const float RestorationBloomDuration = 1.5f;
        private static readonly Color RegionBgDesaturated = new Color(0.85f, 0.82f, 0.75f, 1f);
        private Sprite[] _snapBackFrames;
        private Sprite[] _lockFlashFrames;
        private readonly Dictionary<(int x, int y), Coroutine> _targetPulseRoutines = new();

        // ── Onboarding cue (level-specific first-time hints) ──
        private TextMeshProUGUI _onboardingOverlay;
        private GameObject _onboardingCueRoot;   // chip root (bg + text) — pulse target
        private Tween _onboardingPulseTween;     // killed on hide to avoid leaks
        private bool _onboardingCueShown;

        // ── Colors for orbs (should match DESIGN_CANON CMY primaries) ──
        private static readonly Dictionary<OrbColor, Color> OrbColors = new()
        {
            { OrbColor.Cyan,      new Color(0f, 0.898f, 1f) },     // #00E5FF
            { OrbColor.Magenta,   new Color(1f, 0f, 0.898f) },     // #FF00E5
            { OrbColor.Yellow,    new Color(1f, 0.937f, 0f) },     // #FFEF00
            { OrbColor.Purple,    new Color(0.541f, 0.169f, 0.886f) }, // #8A2BE2
            { OrbColor.Green,     new Color(0.098f, 0.8f, 0.098f) }, // #19CC19
            { OrbColor.Orange,    new Color(1f, 0.498f, 0f) },     // #FF7F00
            { OrbColor.Brown,     new Color(0.4f, 0.2f, 0.05f) },  // #66330D
        };

        // ── Lifecycle ──

        private void Awake()
        {
            // Initialize in Awake (not Start) so this works even when the
            // GameObject starts inactive — LevelSelectView activates it and
            // calls LoadLevel() before Start() would fire.
            _levelRepo = new MergeLevelRepository(new ResourcesLevelJsonProvider());
            _maxLevel = _levelRepo.LevelCount;

            // Restoration burst service (self-initializes in its own Awake).
            var fxGo = new GameObject("ParticleFxService");
            fxGo.transform.SetParent(transform, false);
            _particleFx = fxGo.AddComponent<ParticleFxService>();
        }

        private void Start()
        {
            _levelNumber = Mathf.Clamp(_startLevel, 1, _maxLevel);

            // Parent canvas for the reset button. If no HUD text is wired in the
            // inspector, the fallback HUD canvas + SafeArea below become its parent.
            // If a HUD IS wired, no fallback canvas exists and we build a dedicated
            // one further down. (fix: t_7bc48bac)
            Transform resetButtonParent = null;

            // If no HUD text is wired in the inspector, build a minimal screen-space
            // overlay at runtime so level/moves/par are always visible.
            if (_hudText == null)
            {
                var canvasGo = new GameObject("HUDCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<GraphicRaycaster>(); // needed for Reset button clicks
                ConfigureCanvasScaler(canvas);

                // Notch-safe container: HUD content stays inside Screen.safeArea.
                var safeGo = new GameObject("SafeArea");
                safeGo.transform.SetParent(canvasGo.transform, false);
                safeGo.AddComponent<SafeAreaFitter>();
                resetButtonParent = safeGo.transform;

                var tmpGo = new GameObject("HUDText");
                tmpGo.transform.SetParent(safeGo.transform, false);
                _hudText = tmpGo.AddComponent<TextMeshProUGUI>();
                _hudText.fontSize = 24;
                _hudText.alignment = TextAlignmentOptions.Top;
                _hudText.color = new Color(0.08f, 0.06f, 0.04f, 1f); // dark warm-brown — readable on warm-cream bg
                var rect = _hudText.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -20f);
                rect.sizeDelta = new Vector2(400f, 60f);
            }

            // If a HUD canvas was NOT built above (i.e. _hudText is wired in the
            // inspector), the reset button still needs a parent — create a minimal
            // screen-space canvas so the button ALWAYS exists.
            if (resetButtonParent == null)
            {
                var canvasGo = new GameObject("ResetButtonCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // high enough to stay clickable
                canvasGo.AddComponent<GraphicRaycaster>(); // needed to receive clicks
                ConfigureCanvasScaler(canvas);

                var safeGo = new GameObject("SafeArea");
                safeGo.transform.SetParent(canvasGo.transform, false);
                safeGo.AddComponent<SafeAreaFitter>();
                resetButtonParent = safeGo.transform;
            }

            // Reset button — top-right corner so the player can restart
            // when stuck (e.g. tier-up'd all orbs and can't reach target tier).
            var resetGo = new GameObject("ResetButton");
            resetGo.transform.SetParent(resetButtonParent, false);
            _resetButton = resetGo.AddComponent<Button>();
            var resetImg = resetGo.AddComponent<Image>();
            resetImg.color = new Color(0.2f, 0.3f, 0.4f, 0.8f);
            resetImg.raycastTarget = true; // receives clicks
            var resetRect = resetGo.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(1f, 1f);
            resetRect.anchorMax = new Vector2(1f, 1f);
            resetRect.pivot = new Vector2(1f, 1f);
            resetRect.anchoredPosition = new Vector2(-10f, -10f);
            resetRect.sizeDelta = new Vector2(80f, 40f);
            var resetLabel = new GameObject("Label");
            resetLabel.transform.SetParent(resetGo.transform, false);
            var resetLabelText = resetLabel.AddComponent<TextMeshProUGUI>();
            resetLabelText.text = "Reset";
            resetLabelText.fontSize = 18;
            resetLabelText.alignment = TextAlignmentOptions.Center;
            var resetLabelRect = resetLabel.GetComponent<RectTransform>();
            resetLabelRect.anchorMin = Vector2.zero;
            resetLabelRect.anchorMax = Vector2.one;
            resetLabelRect.pivot = new Vector2(0.5f, 0.5f);
            resetLabelRect.sizeDelta = Vector2.zero;
            _resetButton.onClick.AddListener(() => LoadLevel(_levelNumber));

            // Only auto-load in Start if no level was loaded externally
            // (LevelSelectView calls LoadLevel directly before Start fires)
            if (_level == null)
                LoadLevel(_levelNumber);
        }

        public void LoadLevel(int levelNumber)
        {
            // Clear previous board state
            ClearBoard();

            // Track which level we're actually on. LoadLevel is the single
            // entry point for level-select replays AND in-game next/replay,
            // but only Start() and NextLevel() updated _levelNumber before —
            // a replay from level select left it stale, corrupting
            // RecordLevelComplete, the HUD, region restoration and the
            // Replay button target. (fix: t_47eb8003 b3)
            _levelNumber = levelNumber;

            _level = _levelRepo.GetMergeLevel(levelNumber);

            // Create BoardController and initialize
            _board = new BoardController();
            // TEMP-DIAG (t_e36536c3): surface domain diagnostics in the Unity console
            ((BoardController)_board).OnDiagnostic += msg => Debug.Log($"[BoardDiag] {msg}");
            _board.Initialize(_level);

            // Duskfall vignette (spec l8_duskfall_blackout_spec.md): bind the
            // level's dusk system so screen-edge darkness tracks the countdown.
            if (_duskfallVignette == null)
                _duskfallVignette = gameObject.AddComponent<DuskfallVignette>();
            if (((BoardController)_board).Duskfall != null && ((BoardController)_board).Duskfall.Enabled)
                _duskfallVignette.Bind(((BoardController)_board).Duskfall);
            else
                _duskfallVignette.Unbind();

            // Subscribe to board events
            _board.OnBoardChanged += HandleBoardChanged;
            _board.OnLevelComplete += HandleLevelComplete;

            // Build visual grid
            BuildGrid();

            // Instantiate initial orbs
            SpawnInitialOrbs();

            Debug.Log($"[MergeBoardView] Level {levelNumber} loaded: " +
                      $"{_level.Width}x{_level.Height} grid, " +
                      $"{_level.MergeOrbs?.Length ?? 0} orbs, " +
                      $"{_level.RestorationTargets?.Length ?? 0} targets, " +
                      $"par={_level.ParMoves}");
            UpdateHUD();
            ShowLevelIntroBanner(levelNumber);
            // Each level gets its own first-time cue: reset the flag so the L4/L8
            // cues can still appear after L1's cue was dismissed earlier in the session.
            _onboardingCueShown = false;
            ShowOnboardingCue(levelNumber);

            // Audio: level loaded
            if (AudioServiceInstaller.Instance != null)
                AudioServiceInstaller.Instance.PlaySound("level_start");
        }

        // ── Grid Building ──

        // Target cell visuals (idle pulse + lock flash)
        private Dictionary<(int x, int y), SpriteRenderer> _targetVisuals = new();
        private HashSet<(int x, int y)> _lockedTargets = new();

        private void BuildGrid()
        {
            // Calculate board centering offset
            _boardOffsetX = -_level.Width * _tileSize / 2f;
            _boardOffsetY = -_level.Height * _tileSize / 2f;

            // Create restoration target visuals (idle pulse)
            if (_level.RestorationTargets != null)
            {
                foreach (var target in _level.RestorationTargets)
                {
                    CreateTargetVisual(target.X, target.Y, target.Color, target.Tier);
                }
            }

            // Create grid background tiles
            _gridTiles = new GameObject[_level.Width, _level.Height];
            for (int x = 0; x < _level.Width; x++)
            {
                for (int y = 0; y < _level.Height; y++)
                {
                    Vector3 pos = GridToWorld(x, y);
                    // NOTE: Card snippet used CreatePrimitive(Quad), but Unity forbids
                    // SpriteRenderer on a GameObject that already has a MeshFilter
                    // (conflicts with the Quad's MeshFilter), and the default quad
                    // material would render white. Plain GO + SpriteRenderer + white
                    // sprite gives the intended dark tile that tints via sr.color.
                    var tile = new GameObject($"Tile_{x}_{y}");
                    tile.transform.SetParent(transform, false);
                    tile.transform.position = pos;
                    tile.transform.localScale = Vector3.one * _tileSize * 0.95f;
                    var sr = tile.AddComponent<SpriteRenderer>();
                    // Check if this cell is an obstacle
                    bool isObstacle = false;
                    if (_level.Obstacles != null)
                    {
                        foreach (var obs in _level.Obstacles)
                        {
                            if (obs.X == x && obs.Y == y) { isObstacle = true; break; }
                        }
                    }
                    if (isObstacle)
                    {
                        // Render the board-mounted obstacle fixture art instead of a
                        // flat near-black placeholder square (which read as broken pixels).
                        sr.sprite = LoadObstacleSprite();
                        sr.color = Color.white; // preserve the sprite's own tint
                    }
                    else
                    {
                        sr.sprite = CreateWhiteSprite();
                        sr.color = ChromaPalette.PCB_Substrate;
                    }
                    sr.sortingOrder = -2; // behind everything
                    _gridTiles[x, y] = tile;
                }
            }
            SetupCamera();
            CreateRegionBackground();
        }

        /// <summary>
        /// Creates a restoration target cell visual.  Shows a GHOST ORB PREVIEW
        /// (faded full-color orb at correct tier scale) with a pulsing dashed ring
        /// outline so the player sees exactly what to create and where.  When a
        /// correct (color, tier) orb is placed, the target locks with a green flash.
        /// </summary>
        private void CreateTargetVisual(int x, int y, OrbColor color, OrbTier tier)
        {
            Vector3 worldPos = GridToWorld(x, y);
            var go = new GameObject($"Target_{x}_{y}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            // Ghost orb preview — faded full-color orb at correct tier scale
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDefaultSprite(color);
            Color ghostColor = GetOrbColor(color, tier);
            sr.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0.35f);
            sr.sortingOrder = -1; // behind orbs
            // Scale ghost to match the tier (so T2 target reads larger than T1)
            go.transform.localScale = Vector3.one * TierScale(tier);

            _targetVisuals[(x, y)] = sr;

            // Ring outline — dashed circle around the target slot
            var ringGo = new GameObject($"TargetRing_{x}_{y}");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localPosition = Vector3.zero;
            ringGo.transform.localScale = Vector3.one * 1.15f;
            var ringSr = ringGo.AddComponent<SpriteRenderer>();
            ringSr.sprite = CreateRingSprite();
            ringSr.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0.5f);
            ringSr.sortingOrder = 0; // above ghost, below real orbs

            // Tier pip overlay on ghost (so player knows which tier to create)
            AttachPipOverlay(go, tier);
            // Fade the pips too
            var pipSr = go.transform.Find("PipOverlay")?.GetComponent<SpriteRenderer>();
            if (pipSr != null)
            {
                var pc = pipSr.color;
                pc.a = 0.4f;
                pipSr.color = pc;
            }

            // Idle pulse (procedural — breathing glow on ring + ghost)
            if (!gameObject.activeInHierarchy) return;
            _targetPulseRoutines[(x, y)] = StartCoroutine(TargetIdlePulseRoutine(go.transform, sr, ringSr));
        }

        /// <summary>
        /// Called when a board change might satisfy a target.  Checks if the cell
        /// now holds the correct (color, tier) orb and plays the lock flash.
        /// </summary>
        private void CheckTargetLock(int x, int y, OrbData orb)
        {
            if (!_targetVisuals.ContainsKey((x, y))) return;
            if (_lockedTargets.Contains((x, y))) return;

            if (orb != null)
            {
                // Check if this orb matches the target at this cell
                foreach (var t in _level.RestorationTargets ?? Array.Empty<RestorationTarget>())
                {
                    if (t.X == x && t.Y == y && t.Color == orb.Color && t.Tier == orb.Tier)
                    {
                        _lockedTargets.Add((x, y));

                        // Stop idle pulse; the target is now locked
                        if (_targetPulseRoutines.TryGetValue((x, y), out var pulse))
                        {
                            if (pulse != null) StopCoroutine(pulse);
                            _targetPulseRoutines.Remove((x, y));
                        }
                        if (_targetVisuals.TryGetValue((x, y), out var tsr) && tsr != null)
                        {
                            tsr.transform.localScale = Vector3.one;
                        }

                        // Lock flash (procedural fallback until target_lock_flash_strip.png arrives)
                        PlayTargetLockFlash(x, y);
                        if (AudioServiceInstaller.Instance != null)
                            AudioServiceInstaller.Instance.PlaySound("lock_flash");
                        Debug.Log($"[MergeBoardView] Target locked at ({x},{y}) — {orb.Color} T{orb.Tier}");
                        break;
                    }
                }
            }
        }

        // ── Target animations (procedural fallback until strips arrive) ──

        /// <summary>
        /// Idle pulse loop: breathing glow on ghost orb + ring outline.
        /// Ghost alpha oscillates 0.25↔0.45, ring scale oscillates 1.0↔1.1,
        /// ring alpha oscillates 0.35↔0.55 — synced sine wave, 480ms cycle.
        /// </summary>
        private System.Collections.IEnumerator TargetIdlePulseRoutine(
            Transform targetTransform, SpriteRenderer ghostSr, SpriteRenderer ringSr)
        {
            Color ghostBase = ghostSr.color;
            Color ringBase = ringSr.color;
            Vector3 ringBaseScale = ringSr.transform.localScale;

            while (targetTransform != null && ghostSr != null && ringSr != null)
            {
                float phase = (Time.time % TargetPulseDuration) / TargetPulseDuration;
                float wave = Mathf.Sin(phase * Mathf.PI * 2f); // -1 → +1 → -1

                // Ghost breathing: alpha 0.25 ↔ 0.45
                Color gc = ghostBase;
                gc.a = Mathf.Lerp(0.25f, 0.45f, (wave + 1f) * 0.5f);
                ghostSr.color = gc;

                // Ring breathing: scale 1.0 ↔ 1.1, alpha 0.35 ↔ 0.55
                float ringPulse = Mathf.Lerp(1.0f, 1.1f, (wave + 1f) * 0.5f);
                ringSr.transform.localScale = ringBaseScale * ringPulse;
                Color rc = ringBase;
                rc.a = Mathf.Lerp(0.35f, 0.55f, (wave + 1f) * 0.5f);
                ringSr.color = rc;

                yield return null;
            }
        }

        /// <summary>
        /// Green ring expand+fade on target lock.  4 frames × 50ms = 200ms.
        /// Uses target_lock_flash_strip.png when available; falls back to procedural ring.
        /// </summary>
        private void PlayTargetLockFlash(int x, int y)
        {
            if (_lockFlashFrames == null)
                _lockFlashFrames = LoadStripFrames("target_lock_flash_strip");

            var worldPos = GridToWorld(x, y);
            var go = new GameObject($"LockFlash_{x}_{y}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            if (_lockFlashFrames != null)
            {
                sr.sprite = _lockFlashFrames[0];
                sr.color = new Color(0.20f, 1f, 0.40f, 1f); // green tint; strip bakes the fade
            }
            else
            {
                if (_lockFlashSprite == null) _lockFlashSprite = CreateRingSprite();
                sr.sprite = _lockFlashSprite;
                sr.color = new Color(0.20f, 1f, 0.40f, 0.95f); // green ring
            }
            sr.sortingOrder = 1; // above orbs

            StartCoroutine(TargetLockFlashRoutine(go));
        }

        private System.Collections.IEnumerator TargetLockFlashRoutine(GameObject flashGo)
        {
            float t = 0f;
            var sr = flashGo.GetComponent<SpriteRenderer>();
            Color startColor = sr.color;
            Vector3 startScale = flashGo.transform.localScale;
            bool useStrip = _lockFlashFrames != null;

            float frameDuration = TargetLockDuration / 4f; // 50ms
            float frameTimer = 0f;
            int frame = 0;

            while (t < TargetLockDuration)
            {
                t += Time.deltaTime;

                if (useStrip)
                {
                    // Strip path: advance frames — expansion + fade are baked into the art.
                    frameTimer += Time.deltaTime;
                    if (frameTimer >= frameDuration)
                    {
                        frameTimer = 0f;
                        frame = (frame + 1) % _lockFlashFrames.Length;
                        sr.sprite = _lockFlashFrames[frame];
                    }
                }
                else
                {
                    float k = Mathf.Clamp01(t / TargetLockDuration);
                    float e = k * k * (3f - 2f * k);
                    flashGo.transform.localScale = startScale * Mathf.Lerp(0.8f, 1.6f, e);
                    Color c = startColor;
                    c.a = Mathf.Lerp(startColor.a, 0f, e);
                    sr.color = c;
                }
                yield return null;
            }

            Destroy(flashGo);
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ChromaPalette.PCB_Substrate;
            float boardWidth = _level.Width * _tileSize;
            float boardHeight = _level.Height * _tileSize;
            float pad = _tileSize * 0.8f;
            float heightSize = (boardHeight / 2f) + pad;
            float aspect = Screen.width > 0 ? (float)Screen.width / Screen.height : 1.7778f;
            float widthSize = ((boardWidth / 2f) + pad) / aspect;
            cam.orthographicSize = Mathf.Max(heightSize, widthSize);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        // ── Restoration Payoff (region background grayscale → color on level complete) ──

        /// <summary>Maps the 10 merge levels to the four named vale regions.</summary>
        private static string RegionNameForLevel(int level)
        {
            if (level <= 3) return "forest";
            if (level <= 6) return "garden";
            if (level <= 8) return "lake";
            return "village";
        }

        /// <summary>Procedural fallback tint when the artist region PNG can't load.</summary>
        private static Color RegionAccentColor(string region)
        {
            switch (region)
            {
                case "forest":  return new Color(0.06f, 0.38f, 0.14f);
                case "garden":  return new Color(0.10f, 0.44f, 0.32f);
                case "lake":    return new Color(0.08f, 0.26f, 0.56f);
                case "village": return new Color(0.46f, 0.33f, 0.11f);
                default:        return new Color(0.15f, 0.20f, 0.32f);
            }
        }

        /// <summary>Target tint for the "fully restored" state: white when artist art
        /// is showing (the PNG carries its own color), otherwise the region accent.</summary>
        private Color RegionBgFullColor
        {
            get
            {
                if (_regionBgSprite != null) return Color.white;
                return RegionAccentColor(RegionNameForLevel(_levelNumber));
            }
        }

        /// <summary>Creates (or re-initializes) the region background behind the grid.
        /// Starts desaturated — the vale has lost its color; VisualizeRestoration()
        /// blooms it back on level complete.</summary>
        private void CreateRegionBackground()
        {
            if (_regionBackground == null)
            {
                var go = new GameObject("RegionBackground");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(0f, 0f, 1f);
                _regionBackground = go.AddComponent<SpriteRenderer>();
                _regionBackground.sortingOrder = -10; // behind grid tiles (-2) and orbs (0)
            }

            _regionBackground.DOKill();
            _regionBackground.sprite = LoadRegionBackgroundSprite() ?? CreateWhiteSprite();
            SizeRegionBackground();
            _regionBackground.color = RegionBgDesaturated;
        }

        /// <summary>Scales the background to fit the board EXACTLY — no bleed past
        /// the tile edges, so no second panel edge reads as "two boards" (t_ae96c91e).
        /// The camera (SetupCamera) frames the board; the backdrop matches it.</summary>
        private void SizeRegionBackground()
        {
            if (_regionBackground == null || _regionBackground.sprite == null) return;

            // Board extent in world units, centered on the origin (board offsets
            // are -W*tileSize/2 .. +W*tileSize/2).
            float boardWorldW = _level.Width * _tileSize;
            float boardWorldH = _level.Height * _tileSize;

            Vector2 spriteUnits = new Vector2(
                _regionBackground.sprite.rect.width / Mathf.Max(_regionBackground.sprite.pixelsPerUnit, 0.01f),
                _regionBackground.sprite.rect.height / Mathf.Max(_regionBackground.sprite.pixelsPerUnit, 0.01f));
            _regionBackground.transform.localScale = new Vector3(
                boardWorldW / Mathf.Max(spriteUnits.x, 0.01f),
                boardWorldH / Mathf.Max(spriteUnits.y, 0.01f),
                1f);
            // Keep centered on the board (CreateRegionBackground places it at origin, z=1).
            _regionBackground.transform.position = new Vector3(0f, 0f, 1f);
        }

        /// <summary>Loads the current region's artist background PNG. The PNGs live
        /// outside Resources, so editor runs load them via AssetDatabase; builds fall
        /// back to the procedural region-tinted plane.</summary>
        private Sprite LoadRegionBackgroundSprite()
        {
            var region = RegionNameForLevel(_levelNumber);
            if (string.IsNullOrEmpty(region)) return null;
            var expectedName = $"region_{region}_bg_1080";
            if (_regionBgSprite != null && _regionBgSprite.name == expectedName) return _regionBgSprite;
            _regionBgSprite = null;
#if UNITY_EDITOR
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/_Project/Sprites/Backgrounds/{expectedName}.png")
                      ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/_Project/Sprites/Regions/{expectedName}.png");
            if (tex != null)
            {
                _regionBgSprite = Sprite.Create(tex,
                    new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                _regionBgSprite.name = expectedName;
            }
#endif
            return _regionBgSprite;
        }

        /// <summary>
        /// Restoration payoff (called from HandleLevelComplete):
        /// 1) desaturates the region background (DOTween DOColor → grayscale),
        /// 2) blooms it back to full saturation over 1.5s,
        /// 3) fires the restoration particle burst at the board center.
        /// </summary>
        public void VisualizeRestoration()
        {
            if (_regionBackground == null) CreateRegionBackground();
            if (_regionBackground == null) return;

            // 1) Drain color: quick desaturate dip so the bloom reads as a payoff.
            _regionBackground.DOKill();
            _regionBackground.DOColor(RegionBgDesaturated, RestorationDesatDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    if (_regionBackground == null) return;
                    // 2) Bloom back to full saturation over 1.5s — the vale wakes up.
                    _regionBackground.DOColor(RegionBgFullColor, RestorationBloomDuration)
                        .SetEase(Ease.OutCubic);
                });

            // 3) Particle burst at board center (spark → ignition → ring → sustain).
            if (_particleFx != null) _particleFx.RestorationPulse(Vector3.zero);
        }

        // ── Orb Spawning ──

        private void SpawnInitialOrbs()
        {
            if (_level.MergeOrbs == null) return;

            foreach (var placement in _level.MergeOrbs)
            {
                SpawnOrbVisual(placement.X, placement.Y, placement.Color, placement.Tier);
            }
        }

        private void SpawnOrbVisual(int x, int y, OrbColor color, OrbTier tier)
        {
            Vector3 worldPos = GridToWorld(x, y);

            // GlassOrb shader path: quad + MeshRenderer + MaterialPropertyBlock.
            // No SpriteRenderer, no PNG sprites — the shader renders the orb
            // procedurally from _BaseColor + _TierLevel.
            GameObject orbGo = new GameObject($"Orb_{x}_{y}");
            orbGo.transform.SetParent(transform, false);
            orbGo.transform.position = worldPos;

            var orbVisual = orbGo.AddComponent<OrbVisual>();
            Color orbColor = GetOrbColor(color, tier);
            orbVisual.Configure(orbColor, (int)tier);

            // Designer spec: multiplicative 20% per tier (T1=1.0x → T5=2.07x)
            orbGo.transform.localScale = Vector3.one * TierScale(tier);

            // Designer spec: tier pip overlay (1–5 dots, bottom-center of orb)
            AttachPipOverlay(orbGo, tier);

            _orbVisuals[(x, y)] = orbVisual;
        }

        // ── Orb Sprite Loading ──

        /// <summary>
        /// Loads the artist sprite for (color, tier) from the orb sprite collection
        /// under Assets/_Project/Sprites/Orbs/. Path shape varies by tier:
        ///   T1/T3/T4/T5 → T{tier}/{color}/{color}_T{tier}_{suffix}.png
        ///   T2           → T2/{color}_T2_idle.png (flat)
        /// Suffixes: T1/T2=_idle, T3=_faceted, T4=_runed, T5=_prism (brown=_cracked).
        /// Tertiary colors removed (canon 6-color compliance). Callers keep
        /// the procedural circle fallback for any missing art. Results are
        /// cached; a null result is cached too so missing art is only probed once.
        /// </summary>
        private Sprite LoadOrbSprite(OrbColor color, int tier)
        {
            var key = (color, tier);
            if (_orbSpriteCache.TryGetValue(key, out var cached)) return cached;

            var sprite = TryLoadOrbSpriteResources(color, tier);
#if UNITY_EDITOR
            if (sprite == null) sprite = TryLoadOrbSpriteAssetDatabase(color, tier);
#endif
            _orbSpriteCache[key] = sprite;
            return sprite;
        }

        private static string OrbSpriteFileName(OrbColor color, int tier)
        {
            var name = color.ToString().ToLowerInvariant();
            switch (tier)
            {
                case 1: return $"{name}_T1_idle";
                case 2: return $"{name}_T2_idle";
                case 3: return $"{name}_T3_faceted";
                case 4: return $"{name}_T4_runed";
                case 5: return color == OrbColor.Brown ? "brown_T5_cracked" : $"{name}_T5_prism";
                default: return null;
            }
        }

        private static string OrbSpriteFolder(OrbColor color, int tier)
        {
            // T2 idle art is flat in the T2 folder; all other tiers use a color subfolder.
            return tier == 2 ? $"T{tier}" : $"T{tier}/{color.ToString().ToLowerInvariant()}";
        }

        private Sprite TryLoadOrbSpriteResources(OrbColor color, int tier)
        {
            var file = OrbSpriteFileName(color, tier);
            if (file == null) return null;
            return Resources.Load<Sprite>($"Orbs/{OrbSpriteFolder(color, tier)}/{file}");
        }

#if UNITY_EDITOR
        private Sprite TryLoadOrbSpriteAssetDatabase(OrbColor color, int tier)
        {
            var file = OrbSpriteFileName(color, tier);
            if (file == null) return null;
            var path = $"Assets/_Project/Sprites/Orbs/{OrbSpriteFolder(color, tier)}/{file}.png";
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
#endif

        private void RemoveOrbVisual(int x, int y)
        {
            if (_orbVisuals.TryGetValue((x, y), out var sr))
            {
                if (sr != null) Destroy(sr.gameObject);
                _orbVisuals.Remove((x, y));
            }
        }

        // ── Input Handling (drag-to-draw model) ──
        // Player DRAGS from one orb to an adjacent orb.  Visual only until release.
        // On release: attempt merge, play snap-back if invalid.

        private bool _isDragging;
        private (int x, int y) _dragSource;
        private Vector3 _dragOriginalPos;
        private GameObject _draggedOrbVisual;

        private void Update()
        {
            if (_board == null || _board.IsLevelComplete) return;

            // Touch/mouse input — new Input System (project activeInputHandler = 1;
            // legacy UnityEngine.Input throws InvalidOperationException at runtime)
            bool pointerDown = IsPointerPressed();
            _lastPointerScreenPos = ReadPointerScreenPosition();
            bool downThisFrame = pointerDown && !_wasPointerDown;
            bool upThisFrame = !pointerDown && _wasPointerDown;
            _wasPointerDown = pointerDown;

            if (downThisFrame)
            {
                Vector3 worldPos = Camera.main != null ? Camera.main.ScreenToWorldPoint(_lastPointerScreenPos) : Vector3.zero;
                var gridPos = WorldToGrid(worldPos);
                if (gridPos.HasValue)
                {
                    // Start drag only if cell has an orb
                    var orb = _board.GetOrbAt(new GridPosition(gridPos.Value.x, gridPos.Value.y));
                    if (orb != null)
                    {
                        StopSnapBack();
                        _isDragging = true;
                        _dragSource = gridPos.Value;
                        _dragOriginalPos = GridToWorld(gridPos.Value.x, gridPos.Value.y);
                        // Grab the visual to follow finger
                        if (_orbVisuals.TryGetValue(gridPos.Value, out var sr) && sr != null)
                        {
                            _draggedOrbVisual = sr.gameObject;
                            _draggedBaseScale = sr.transform.localScale;
                        }
                    }
                }
            }

            if (_isDragging && pointerDown)
            {
                // Dragged orb follows finger
                Vector3 worldPos = Camera.main != null ? Camera.main.ScreenToWorldPoint(_lastPointerScreenPos) : Vector3.zero;
                worldPos.z = 0f;
                if (_draggedOrbVisual != null)
                {
                    _draggedOrbVisual.transform.position = worldPos;
                }

                // Highlight adjacent target orb on hover
                UpdateHoverHighlight(worldPos);
            }

            if (_isDragging && upThisFrame)
            {
                Vector3 worldPos = Camera.main != null ? Camera.main.ScreenToWorldPoint(_lastPointerScreenPos) : Vector3.zero;
                FinishDrag(worldPos);
            }
        }

        private bool IsPointerPressed()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private Vector2 ReadPointerScreenPosition()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return _lastPointerScreenPos;
        }

        /// <summary>
        /// Called on pointer release.  Attempts the merge; plays snap-back when invalid.
        /// On success, HandleBoardChanged syncs visuals (orb removed, transformed, etc.).
        /// </summary>
        private void FinishDrag(Vector3 worldPos)
        {
            _isDragging = false;
            ClearHoverHighlight();

            var gridPos = WorldToGrid(worldPos);

            if (gridPos.HasValue && gridPos.Value != _dragSource)
            {
                // Distance check: don't merge if the release is too far from the
                // target cell center (prevents accidental merges on near-miss drags).
                Vector3 targetCenter = GridToWorld(gridPos.Value.x, gridPos.Value.y);
                float distToTarget = Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.y),
                    new Vector2(targetCenter.x, targetCenter.y));
                float mergeThreshold = _tileSize * 0.6f; // must be within 60% of cell size

                if (distToTarget > mergeThreshold)
                {
                    // Too far — snap back, no merge attempt
                    if (_draggedOrbVisual != null)
                        StartSnapBack(_draggedOrbVisual, _dragOriginalPos, _draggedBaseScale);
                    _draggedOrbVisual = null;
                    return;
                }

                // Attempt merge: drag source → release target
                var source = new GridPosition(_dragSource.x, _dragSource.y);
                var target = new GridPosition(gridPos.Value.x, gridPos.Value.y);

                bool success = _board.TryMergeAt(source, target);
                if (!success && _draggedOrbVisual != null)
                {
                    // Invalid merge — snap-back animation
                    // TODO: play snap_back_strip.png (240ms) when artist delivers
                    StartSnapBack(_draggedOrbVisual, _dragOriginalPos, _draggedBaseScale);
                }
            }
            else if (_draggedOrbVisual != null)
            {
                // Released on same cell or empty — snap back
                // TODO: play snap_back_strip.png (240ms) when artist delivers
                StartSnapBack(_draggedOrbVisual, _dragOriginalPos, _draggedBaseScale);
            }

            _draggedOrbVisual = null;
        }

        // ── Hover highlight (adjacent target cell) ──

        private void UpdateHoverHighlight(Vector3 worldPos)
        {
            var gridPos = WorldToGrid(worldPos);
            if (gridPos.HasValue && gridPos.Value != _dragSource)
            {
                var orb = _board.GetOrbAt(new GridPosition(gridPos.Value.x, gridPos.Value.y));
                if (orb != null)
                {
                    if (!_hoverCell.HasValue || _hoverCell.Value != gridPos.Value)
                    {
                        ClearHoverHighlight();
                        _hoverCell = gridPos.Value;
                        HighlightCell(gridPos.Value.x, gridPos.Value.y, true);
                    }
                    return;
                }
            }

            ClearHoverHighlight();
        }

        private void ClearHoverHighlight()
        {
            if (_hoverCell.HasValue)
            {
                HighlightCell(_hoverCell.Value.x, _hoverCell.Value.y, false);
                _hoverCell = null;
            }
        }

        // ── Snap-back animation (procedural fallback until snap_back_strip.png) ──

        private void StartSnapBack(GameObject orbVisual, Vector3 origin, Vector3 baseScale)
        {
            if (_snapBackFrames == null) _snapBackFrames = LoadStripFrames("snap_back_strip");
            StopSnapBack();
            _snapBackOrb = orbVisual;
            _snapBackCoroutine = StartCoroutine(SnapBackRoutine(orbVisual, origin, baseScale));
        }

        private void StopSnapBack()
        {
            if (_snapBackCoroutine != null)
            {
                // Restore the orb visual BEFORE killing the coroutine: StopCoroutine
                // skips the routine's cleanup tail, so a mid-flight snap-back would
                // otherwise leave the orb stuck faded/shrunk at an interpolated spot.
                if (_snapBackOrb != null)
                {
                    _snapBackOrb.transform.position = _dragOriginalPos;
                    _snapBackOrb.transform.localScale = _draggedBaseScale;
                    var orbVis = _snapBackOrb.GetComponent<OrbVisual>();
                    if (orbVis != null) orbVis.SetAlpha(1f);
                }
                StopCoroutine(_snapBackCoroutine);
                _snapBackCoroutine = null;
                _snapBackOrb = null;
            }
        }

        /// <summary>
        /// Loads the 4 frames of a UI animation strip from Resources.
        /// Strips live at Assets/_Project/Resources/UI/AnimationStrips/.
        /// Returns null when the strip is missing (caller falls back to procedural).
        /// </summary>
        private Sprite[] LoadStripFrames(string stripName)
        {
            var frames = Resources.LoadAll<Sprite>($"UI/AnimationStrips/{stripName}");
            return frames != null && frames.Length >= 4 ? frames : null;
        }

        private System.Collections.IEnumerator SnapBackRoutine(GameObject orbVisual, Vector3 origin, Vector3 baseScale)
        {
            // Strip-first: snap_back_strip.png (4 frames × 60ms = 240ms) — pulsing glow
            // overlaid on the orb while it returns to origin.  Fallback: shrink + fade.
            var orbVis = orbVisual.GetComponent<OrbVisual>();
            Color startColor = orbVis != null ? orbVis.GetColor() : Color.white;
            bool useStrip = false; // GlassOrb shader — no sprite swapping

            float t = 0f;
            Vector3 startPos = orbVisual.transform.position;
            float frameDuration = SnapBackDuration / 4f; // 60ms
            float frameTimer = 0f;
            int frame = 0;

            while (t < SnapBackDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / SnapBackDuration);
                float e = k * k * (3f - 2f * k); // smoothstep
                orbVisual.transform.position = Vector3.Lerp(startPos, origin, e);

                // Shrink toward origin but NEVER fade to invisible — if interrupted,
                // the orb must remain visible.
                orbVisual.transform.localScale = baseScale * (1f - 0.3f * e);
                if (orbVis != null)
                {
                    orbVis.SetAlpha(Mathf.Lerp(startColor.a, 0.3f, e)); // min 30% alpha
                }

                yield return null;
            }

            // Restore original state
            orbVisual.transform.position = origin;
            orbVisual.transform.localScale = baseScale;
            if (orbVis != null) orbVis.SetColor(startColor);
            _snapBackCoroutine = null;
        }

        // ── Board Event Handling ──

        private void HandleBoardChanged(BoardChange change)
        {
            switch (change.Type)
            {
                case ChangeType.OrbAdded:
                    if (change.NewOrb != null)
                    {
                        SpawnOrbVisual(change.Position.X, change.Position.Y,
                                       change.NewOrb.Color, change.NewOrb.Tier);
                        PlaySpawnAnimation(change.Position.X, change.Position.Y);
                        if (AudioServiceInstaller.Instance != null)
                            AudioServiceInstaller.Instance.PlaySound("spawn");
                        CheckTargetLock(change.Position.X, change.Position.Y, change.NewOrb);
                    }
                    break;

                case ChangeType.OrbRemoved:
                    RemoveOrbVisual(change.Position.X, change.Position.Y);
                    // L8 teaches Brown+Brown clearing. A BrownClear emits TWO
                    // OrbRemoved changes and NO OrbTransformed (no orb is produced),
                    // so the taught action must be detected here. Brown can only
                    // merge with Brown, so any Brown removal IS the taught action.
                    if (_levelNumber == 8 && change.OldOrb != null && change.OldOrb.Color == OrbColor.Brown)
                        HideOnboardingCue();
                    break;

                case ChangeType.OrbTransformed:
                    // Onboarding cue persists until the CORRECT taught action:
                    //   L1: first same-color merge (cyan T1+T1 → cyan T2)
                    //   L4: first cross-color mix producing Purple (cyan+magenta)
                    //   L8: Brown+Brown clear — detected in OrbRemoved above (no NewOrb)
                    //   Other levels: hide on any merge (legacy behavior)
                    if (change.NewOrb != null)
                    {
                        bool isTaughtAction = _levelNumber switch
                        {
                            1 => change.NewOrb.Tier == OrbTier.T2,
                            4 => change.NewOrb.Color == OrbColor.Purple,
                            8 => false, // BrownClear never emits OrbTransformed
                            _ => true,
                        };
                        if (isTaughtAction)
                            HideOnboardingCue();
                    }
                    // Bug 3 fix: source orb shrinks out instead of vanishing instantly.
                    // Shrink-fade must happen BEFORE SpawnOrbVisual, and the dict key
                    // must be removed before spawn so the new orb can claim it.
                    if (_orbVisuals.TryGetValue((change.Position.X, change.Position.Y), out var sourceOrb) && sourceOrb != null)
                    {
                        var sourceGo = sourceOrb.gameObject;
                        sourceGo.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuad)
                            .SetLink(sourceGo)
                            .OnComplete(() =>
                            {
                                if (sourceGo != null) Destroy(sourceGo);
                            });
                        _orbVisuals.Remove((change.Position.X, change.Position.Y));
                    }
                    else
                    {
                        RemoveOrbVisual(change.Position.X, change.Position.Y);
                    }
                    if (change.NewOrb != null)
                    {
                        SpawnOrbVisual(change.Position.X, change.Position.Y,
                                       change.NewOrb.Color, change.NewOrb.Tier);

                        // Merge animation: punch + glow + settle.
                        // Phase 1: instant shrink to 30%
                        // Phase 2: grow with overshoot punch (0.4s total — readable)
                        // Phase 3: visible glow flash in the orb's own hue (NOT
                        //          white * 1.5, which clamps back to white = invisible)
                        if (_orbVisuals.TryGetValue((change.Position.X, change.Position.Y), out var newOrb) && newOrb != null)
                        {
                            var go = newOrb.gameObject;
                            var targetScale = go.transform.localScale;

                            // Phase 1: instant shrink to 30%
                            go.transform.localScale = targetScale * 0.3f;

                            // Phase 2: grow with overshoot punch (0.2s out + 0.2s settle)
                            go.transform.DOScale(targetScale * 1.2f, 0.2f).SetEase(Ease.OutQuad)
                                .SetLink(go)
                                .OnComplete(() =>
                                {
                                    if (go != null)
                                        go.transform.DOScale(targetScale, 0.2f).SetEase(Ease.OutBack).SetLink(go);
                                });

                            // Phase 3: visible glow flash — boost the orb's own color
                            var orbColor = GetOrbColor(change.NewOrb.Color, change.NewOrb.Tier);
                            var flashColor = new Color(
                                Mathf.Min(1f, orbColor.r * 1.8f + 0.3f),
                                Mathf.Min(1f, orbColor.g * 1.8f + 0.3f),
                                Mathf.Min(1f, orbColor.b * 1.8f + 0.3f),
                                1f);
                            var baseColor = newOrb.GetColor();
                            newOrb.SetColor(flashColor);
                            DOTween.To(() => newOrb.GetColor(), c => newOrb.SetColor(c), baseColor, 0.4f)
                                .SetDelay(0.15f).SetLink(go);
                        }

                        if (AudioServiceInstaller.Instance != null)
                            AudioServiceInstaller.Instance.PlaySound("merge");
                        CheckTargetLock(change.Position.X, change.Position.Y, change.NewOrb);
                    }
                    break;
            }
            UpdateHUD();
        }

        // ── Merge/Spawn Animations (design constants: merge 640ms, spawn 480ms) ──

        private void PlaySpawnAnimation(int x, int y)
        {
            if (!_orbVisuals.TryGetValue((x, y), out var orbVis) || orbVis == null) return;
            StartCoroutine(SpawnRoutine(orbVis, orbVis.transform.localScale));
        }

        private void PlayMergeAnimation(int x, int y, Color flashColor)
        {
            if (!_orbVisuals.TryGetValue((x, y), out var orbVis) || orbVis == null) return;
            StartCoroutine(MergeRoutine(orbVis, orbVis.transform.localScale, flashColor));
        }

        private System.Collections.IEnumerator SpawnRoutine(OrbVisual orbVis, Vector3 baseScale)
        {
            // 8 frames × 60ms = 480ms: scale-in + overshoot settle (easeOutBack)
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float t = 0f;
            while (t < SpawnAnimDuration)
            {
                // Guard: orbVis may be destroyed by a later change in the same batch
                if (orbVis == null) yield break;
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / SpawnAnimDuration);
                // easeOutBack: 0 → overshoot past 1 → settle at 1
                float f = k - 1f;
                float s = 1f + c3 * f * f * f + c1 * f * f;
                orbVis.transform.localScale = baseScale * s;
                yield return null;
            }
            if (orbVis == null) yield break;
            orbVis.transform.localScale = baseScale;
        }

        private System.Collections.IEnumerator MergeRoutine(OrbVisual orbVis, Vector3 baseScale, Color flashColor)
        {
            // 8 frames × 80ms = 640ms: squash → flash → settle
            // Flash color = RESULT orb color (already set on orbVis), brightened.
            float t = 0f;
            while (t < MergeAnimDuration)
            {
                // Guard: orbVis may be destroyed by a later change in the same batch
                if (orbVis == null) yield break;
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / MergeAnimDuration);

                if (k < 0.4f)
                {
                    // Phase 1 (0–40%): squash — flatten vertically, bulge horizontally
                    float sq = Mathf.Sin((k / 0.4f) * Mathf.PI);
                    orbVis.transform.localScale = new Vector3(
                        baseScale.x * (1f + 0.35f * sq),
                        baseScale.y * (1f - 0.35f * sq),
                        baseScale.z);
                }
                else if (k < 0.75f)
                {
                    // Phase 2 (40–75%): flash — brighten toward white (result hue kept)
                    float fk = (k - 0.4f) / 0.35f;
                    orbVis.SetColor(Color.Lerp(flashColor, Color.white, fk * 0.75f));
                    float pop = 1f + 0.18f * Mathf.Sin(fk * Mathf.PI);
                    orbVis.transform.localScale = baseScale * pop;
                }
                else
                {
                    // Phase 3 (75–100%): settle back to base scale/color
                    float sk = (k - 0.75f) / 0.25f;
                    float e = sk * sk * (3f - 2f * sk); // smoothstep
                    orbVis.transform.localScale = Vector3.Lerp(baseScale * 1.18f, baseScale, e);
                    orbVis.SetColor(Color.Lerp(Color.white, flashColor, e));
                }
                yield return null;
            }
            if (orbVis == null) yield break;
            orbVis.transform.localScale = baseScale;
            orbVis.SetColor(flashColor);
        }

        private void HandleLevelComplete(LevelResult result)
        {
            // Dismiss onboarding hint if it was still showing
            HideOnboardingCue();

            // Dismiss intro banner if still showing
            if (_introBanner != null)
            {
                Destroy(_introBanner);
                _introBanner = null;
            }

            // Audio: level complete fanfare
            if (AudioServiceInstaller.Instance != null)
                AudioServiceInstaller.Instance.PlaySound("win_fanfare");

            // Restoration payoff: region background grayscale → full color + particle burst.
            VisualizeRestoration();

            Debug.Log($"[MergeBoardView] Level Complete! Moves: {result.MovesUsed}, " +
                      $"Par: {result.Par}, Stars: {result.Stars}");

            UpdateHUD();
            if (_hudText != null) _hudText.text += $"    Stars: {result.Stars}";

            // Record stars in save data
            var saveManager = SaveGameManager.Instance;
            if (saveManager != null)
            {
                saveManager.RecordLevelComplete(_levelNumber, result.Stars);
                Debug.Log($"[MergeBoardView] Recorded {_levelNumber} -> {result.Stars} stars " +
                          $"(total: {saveManager.TotalChromaStars})");
            }

            // Show win popup with buttons (not auto-advance)
            ShowWinPopup(result.Stars);
        }

        private void UpdateHUD()
        {
            if (_hudText == null) return;
            int moves = _board?.MoveCount ?? 0;
            int par = _level?.ParMoves ?? 0;
            int targetsTotal = _level?.RestorationTargets?.Length ?? 0;
            int targetsDone = 0;
            if (_board != null && _level?.RestorationTargets != null)
            {
                foreach (var t in _level.RestorationTargets)
                {
                    bool found = false;
                    for (int y = 0; y < _level.Height; y++)
                    {
                        for (int x = 0; x < _level.Width; x++)
                        {
                            var orb = _board.GetOrbAt(new GridPosition(x, y));
                            if (orb != null && orb.Color == t.Color && orb.Tier == t.Tier)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                    if (found) targetsDone++;
                }
            }
            var targetDesc = BuildTargetDescription();
            _hudText.text = $"Level {_levelNumber}    Moves: {moves}/{par}    Targets: {targetsDone}/{targetsTotal}{targetDesc}";
        }

        private string BuildTargetDescription()
        {
            if (_level?.RestorationTargets == null || _level.RestorationTargets.Length == 0)
                return "";
            var parts = new System.Text.StringBuilder("    Create: ");
            var groups = new Dictionary<(OrbColor, OrbTier), int>();
            foreach (var t in _level.RestorationTargets)
            {
                var key = (t.Color, t.Tier);
                groups[key] = groups.TryGetValue(key, out var v) ? v + 1 : 1;
            }
            bool first = true;
            foreach (var kvp in groups)
            {
                if (!first) parts.Append(", ");
                parts.Append($"{kvp.Value}x {kvp.Key.Item1} T{(int)kvp.Key.Item2}");
                first = false;
            }
            return parts.ToString();
        }

        // ── Level Intro Banner (animated slide-in on level load) ──

        private GameObject _introBanner;

        /// <summary>Configures a screen-space overlay canvas for mobile: scale with
        /// screen size at a 1080×1920 portrait reference resolution.</summary>
        private static void ConfigureCanvasScaler(Canvas canvas)
        {
            if (canvas == null) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void ShowLevelIntroBanner(int levelNumber)
        {
            if (_introBanner != null) Destroy(_introBanner);

            var bannerGo = new GameObject("LevelIntroBanner");
            var canvas = bannerGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            bannerGo.AddComponent<GraphicRaycaster>();
            ConfigureCanvasScaler(canvas);

            // Semi-transparent panel
            var panelGo = new GameObject("BannerPanel");
            panelGo.transform.SetParent(bannerGo.transform, false);
            var panel = panelGo.AddComponent<Image>();
            panel.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            panel.raycastTarget = false;
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600f, 120f);
            panelRect.anchoredPosition = Vector2.zero;

            // Title text — "Level N — Match the targets!"
            var titleGo = new GameObject("BannerTitle");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = $"Level {levelNumber} — Match the targets!";
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.84f, 0.28f, 1f); // warm gold
            titleText.raycastTarget = false;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(580f, 80f);
            titleRect.anchoredPosition = Vector2.zero;

            _introBanner = bannerGo;
            if (!gameObject.activeInHierarchy) return;
            StartCoroutine(IntroBannerAnimation(panelRect));
        }

        private System.Collections.IEnumerator IntroBannerAnimation(RectTransform panelRect)
        {
            // Slide in from above: y offset +300 → 0 over 300ms (easeOutCubic)
            float duration = 0.3f;
            float t = 0f;
            Vector2 startPos = new Vector2(0f, 300f);
            Vector2 endPos = Vector2.zero;
            panelRect.anchoredPosition = startPos;

            while (t < duration)
            {
                if (panelRect == null) yield break; // banner destroyed by ClearBoard
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                // easeOutCubic: 1 - (1-k)^3
                float e = 1f - Mathf.Pow(1f - k, 3f);
                panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, e);
                yield return null;
            }
            if (panelRect == null) yield break; // destroyed mid-animation
            panelRect.anchoredPosition = endPos;

            // Hold for 1.5s
            yield return new WaitForSeconds(1.5f);

            // Fade out over 400ms
            // NOTE: Use explicit == null (Unity's overloaded operator), NOT ?.
            // A destroyed GameObject is not C#-null, so ?. would still call into it.
            if (_introBanner == null) yield break;
            var images = _introBanner.GetComponentsInChildren<Image>();
            var texts = _introBanner.GetComponentsInChildren<TextMeshProUGUI>();
            float fadeT = 0f;
            float fadeDur = 0.4f;
            // Capture initial alphas
            var imgAlphas = images?.Select(img => img.color.a).ToArray() ?? Array.Empty<float>();
            var txtAlphas = texts?.Select(txt => txt.color.a).ToArray() ?? Array.Empty<float>();

            while (fadeT < fadeDur)
            {
                fadeT += Time.deltaTime;
                float k = Mathf.Clamp01(fadeT / fadeDur);
                float a = Mathf.Lerp(1f, 0f, k);
                if (images != null)
                    for (int i = 0; i < images.Length; i++)
                    {
                        if (images[i] != null)
                        {
                            var c = images[i].color;
                            c.a = imgAlphas[i] * a;
                            images[i].color = c;
                        }
                    }
                if (texts != null)
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i] != null)
                        {
                            var c = texts[i].color;
                            c.a = txtAlphas[i] * a;
                            texts[i].color = c;
                        }
                    }
                yield return null;
            }

            if (_introBanner != null) Destroy(_introBanner);
        }

        // ── Onboarding hints (level-specific first-time cues) ──

        private static readonly Dictionary<int, string> OnboardingTexts = new()
        {
            { 1, "Drag two same-color Lumies together!" },
            { 4, "Mix colors! Cyan + Magenta = Purple" },
            { 8, "Two Browns = cleared!" },
        };

        private void ShowOnboardingCue(int levelNumber)
        {
            if (!OnboardingTexts.TryGetValue(levelNumber, out var cueText)) return;
            if (_onboardingCueShown) return;

            // Skip if player already beat this level (they know the mechanic)
            var saveManager = SaveGameManager.Instance;
            if (saveManager != null && saveManager.GetStarsForLevel(levelNumber) > 0)
                return;

            if (_onboardingOverlay == null)
                _onboardingOverlay = BuildOnboardingOverlay(cueText);
            else
                _onboardingOverlay.text = cueText;  // Reuse overlay, swap text

            if (_onboardingCueRoot != null)
                _onboardingCueRoot.SetActive(true);
            else if (_onboardingOverlay != null)
                _onboardingOverlay.gameObject.SetActive(true);

            StartOnboardingPulse();
        }

        private void HideOnboardingCue()
        {
            StopOnboardingPulse();
            if (_onboardingCueRoot != null)
                _onboardingCueRoot.SetActive(false);
            else if (_onboardingOverlay != null)
                _onboardingOverlay.gameObject.SetActive(false);
            _onboardingCueShown = true;
        }

        // Subtle idle pulse (scale 1.0 ↔ 1.05 yoyo loop) so the cue draws the
        // eye without covering the board — same design language as target pulse.
        private void StartOnboardingPulse()
        {
            StopOnboardingPulse();
            if (_onboardingCueRoot == null) return;
            _onboardingCueRoot.transform.localScale = Vector3.one;
            _onboardingPulseTween = _onboardingCueRoot.transform
                .DOScale(1.05f, 0.7f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void StopOnboardingPulse()
        {
            if (_onboardingPulseTween != null)
            {
                _onboardingPulseTween.Kill();
                _onboardingPulseTween = null;
            }
            if (_onboardingCueRoot != null)
                _onboardingCueRoot.transform.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            // Kill the idle pulse so DOTween doesn't keep a stale tween running.
            StopOnboardingPulse();
        }

        private TextMeshProUGUI BuildOnboardingOverlay(string cueText = null)
        {
            cueText ??= "Merge two matching orbs to restore color!";

            var parent = _hudText != null ? _hudText.transform.parent : null;
            var canvasGo = parent != null ? parent.gameObject : null;
            if (canvasGo == null)
            {
                canvasGo = new GameObject("HUDCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ConfigureCanvasScaler(canvas);
            }

            // Chip root (background + text). The idle pulse tweens this whole node.
            var rootGo = new GameObject("OnboardingCue", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            _onboardingCueRoot = rootGo;

            // Semi-transparent dark chip so the cue reads over any board color.
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(rootGo.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(-24f, -12f);   // bleed slightly past text
            bgRect.offsetMax = new Vector2(24f, 12f);

            // Text fills the chip, wrapped and centered.
            var go = new GameObject("Text");
            go.transform.SetParent(rootGo.transform, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = cueText;
            tmp.fontSize = 28;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 1f, 1f);
            tmp.raycastTarget = false;

            var rect = tmp.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            // Center-bottom of the screen (thumb-reach zone, clear of the grid).
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.15f);
            rootRect.anchorMax = new Vector2(0.5f, 0.15f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(800f, 80f);

            return tmp;
        }

        private void NextLevel()
        {
            _levelNumber++;
            if (_levelNumber > _maxLevel)
            {
                Debug.Log("[MergeBoardView] All levels complete!");
                ReturnToLevelSelect();
                return;
            }
            LoadLevel(_levelNumber);
        }

        // ── Win Popup ──

        private GameObject _winPopup;

        private void ShowWinPopup(int stars)
        {
            // Don't create duplicate popups
            if (_winPopup != null) Destroy(_winPopup);

            // [WinPopupDiag] Root-cause probe: is the UI event pipeline alive on device?
            Debug.Log($"[WinPopupDiag] EventSystem.current={(UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.name : "NULL")}, module={(UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentInputModule != null ? UnityEngine.EventSystems.EventSystem.current.currentInputModule.GetType().Name : "NULL")}");
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("WinPopupEventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }

            var popupGo = new GameObject("WinPopup");
            var canvas = popupGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            popupGo.AddComponent<GraphicRaycaster>();
            ConfigureCanvasScaler(canvas);

            // Semi-transparent backdrop
            var backdropGo = new GameObject("Backdrop");
            backdropGo.transform.SetParent(popupGo.transform, false);
            var backdrop = backdropGo.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.6f);
            backdrop.raycastTarget = true;
            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.sizeDelta = Vector2.zero;

            // Panel
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(popupGo.transform, false);
            var panel = panelGo.AddComponent<Image>();
            panel.color = new Color(0.15f, 0.17f, 0.22f, 0.95f);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500f, 400f);
            panelRect.anchoredPosition = Vector2.zero;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "Level Complete!";
            titleText.fontSize = 36;
            titleText.alignment = TextAlignmentOptions.Center;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);
            titleRect.sizeDelta = new Vector2(450f, 60f);

            // Stars display
            for (int i = 0; i < 3; i++)
            {
                var starGo = new GameObject($"Star_{i}");
                starGo.transform.SetParent(panelGo.transform, false);
                var starImg = starGo.AddComponent<Image>();
                starImg.color = i < stars ? new Color(1f, 0.84f, 0.28f) : new Color(0.2f, 0.2f, 0.2f);
                var starRect = starGo.GetComponent<RectTransform>();
                starRect.anchorMin = new Vector2(0.5f, 0.5f);
                starRect.anchorMax = new Vector2(0.5f, 0.5f);
                starRect.pivot = new Vector2(0.5f, 0.5f);
                starRect.sizeDelta = new Vector2(60f, 60f);
                starRect.anchoredPosition = new Vector2((i - 1) * 80f, 30f);
            }

            // Buttons
            CreatePopupButton(panelGo, "Next Level", new Vector2(0f, -80f), () =>
            {
                Destroy(_winPopup);
                _winPopup = null;
                NextLevel();
            });

            CreatePopupButton(panelGo, "Replay", new Vector2(0f, -150f), () =>
            {
                Destroy(_winPopup);
                _winPopup = null;
                LoadLevel(_levelNumber);
            });

            CreatePopupButton(panelGo, "Level Select", new Vector2(0f, -220f), () =>
            {
                Destroy(_winPopup);
                _winPopup = null;
                ReturnToLevelSelect();
            });

            _winPopup = popupGo;
        }

        private void CreatePopupButton(GameObject parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject($"Btn_{label}");
            btnGo.transform.SetParent(parent.transform, false);
            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.3f, 0.4f);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(300f, 50f);
            btnRect.anchoredPosition = pos;

            var btnLabel = new GameObject("Label");
            btnLabel.transform.SetParent(btnGo.transform, false);
            var labelText = btnLabel.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 24;
            labelText.alignment = TextAlignmentOptions.Center;
            var labelRect = btnLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.onClick.AddListener(onClick);
            // Raw-touch fallback: fires even if the EventSystem/UIInputModule pipeline is dead on device.
            btnGo.AddComponent<PopupTapFallback>().Init(btnRect, onClick);
        }

        /// <summary>Manual tap detector independent of the UI event pipeline (device fix v10004).</summary>
        private class PopupTapFallback : MonoBehaviour
        {
            private RectTransform _rect;
            private UnityEngine.Events.UnityAction _onClick;
            private bool _wasDown;
            private float _lastFire = -10f;

            public void Init(RectTransform rect, UnityEngine.Events.UnityAction onClick)
            {
                _rect = rect;
                _onClick = onClick;
            }

            private void Update()
            {
                var ts = UnityEngine.InputSystem.Touchscreen.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                bool down = ts != null && ts.primaryTouch.press.wasPressedThisFrame;
                bool up = ts != null && ts.primaryTouch.press.wasReleasedThisFrame;
                if (!down && !up && mouse == null) return;

                Vector2 pos = ts != null ? ts.primaryTouch.position.ReadValue()
                            : mouse != null ? mouse.position.ReadValue() : Vector2.zero;

                if (down) _wasDown = true;
                if (up && _wasDown && Time.unscaledTime - _lastFire > 0.5f
                    && RectTransformUtility.RectangleContainsScreenPoint(_rect, pos, null))
                {
                    _lastFire = Time.unscaledTime;
                    var btn = GetComponent<Button>();
                    if (btn != null) btn.interactable = false; // dedupe vs EventSystem double-fire
                    Debug.Log($"[PopupTapFallback] fired on {name}");
                    _onClick?.Invoke();
                }
            }
        }

        private void ReturnToLevelSelect()
        {
            // Find LevelSelectView and return to it
            var levelSelect = FindAnyObjectByType<LevelSelectView>();
            if (levelSelect != null)
            {
                levelSelect.ReturnToSelect();
            }
            else
            {
                // Fallback: just hide the board
                gameObject.SetActive(false);
                Debug.LogWarning("[MergeBoardView] No LevelSelectView found — board hidden.");
            }
        }

        // ── Helpers ──

        private Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(
                x * _tileSize + _boardOffsetX + _tileSize / 2f,
                y * _tileSize + _boardOffsetY + _tileSize / 2f,
                0f
            );
        }

        private (int x, int y)? WorldToGrid(Vector3 world)
        {
            float gx = (world.x - _boardOffsetX - _tileSize / 2f) / _tileSize;
            float gy = (world.y - _boardOffsetY - _tileSize / 2f) / _tileSize;

            int x = Mathf.RoundToInt(gx);
            int y = Mathf.RoundToInt(gy);

            if (x < 0 || x >= _level.Width || y < 0 || y >= _level.Height)
                return null;

            return (x, y);
        }

        private void HighlightCell(int x, int y, bool on)
        {
            // TODO: Add visual highlight (outline, scale pulse, glow)
            // For now, scale the orb slightly (base tier scale × hover factor)
            if (_orbVisuals.TryGetValue((x, y), out var sr) && sr != null)
            {
                float baseScale = BaseOrbScale(x, y);
                float scale = on ? baseScale * HoverScaleFactor : baseScale;
                sr.transform.localScale = Vector3.one * scale;
            }
        }

        private float BaseOrbScale(int x, int y)
        {
            var orb = _board != null ? _board.GetOrbAt(new GridPosition(x, y)) : null;
            return orb == null ? 1f : TierScale(orb.Tier);
        }

        /// <summary>
        /// Designer-locked tier scale: multiplicative 20% per tier
        /// (T1=1.0x, T2=1.2x, T3=1.44x, T4=1.73x, T5=2.07x) applied to the
        /// T1 base of 0.6 so T1 fills ~50% of the 1.2 tile (mobile best
        /// practice — collectible, not space-filling). T5 overflows slightly,
        /// which is intended for the "ultimate" tier.
        /// </summary>
        private float TierScale(OrbTier tier)
        {
            // Canon v2.1.0 §3.1 — tier v2 spec (2026-08-22).
            // T1 0.85 / T2 1.00 / T3 1.25 / T4 1.50 / T5 1.70
            // Supersedes the 2026-08-21 formula: 0.6f * 1.2^(tier-1)
            return (int)tier switch
            {
                1 => 0.85f,
                2 => 1.00f,
                3 => 1.25f,
                4 => 1.50f,
                5 => 1.70f,
                _ => 1.00f,
            };
        }

        private void ClearBoard()
        {
            // Unsubscribe from previous board events
            if (_board != null)
            {
                _board.OnBoardChanged -= HandleBoardChanged;
                _board.OnLevelComplete -= HandleLevelComplete;
            }

            // Destroy all grid tiles
            if (_gridTiles != null)
            {
                foreach (var tile in _gridTiles)
                {
                    if (tile != null) Destroy(tile);
                }
                _gridTiles = null;
            }

            // Destroy all orb visuals
            foreach (var kvp in _orbVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _orbVisuals.Clear();

            // Destroy all target visuals
            foreach (var kvp in _targetVisuals)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _targetVisuals.Clear();
            _lockedTargets.Clear();

            // Stop animations
            StopSnapBack();
            foreach (var kvp in _targetPulseRoutines)
            {
                if (kvp.Value != null) StopCoroutine(kvp.Value);
            }
            _targetPulseRoutines.Clear();
            _hoverCell = null;

            _isDragging = false;
            _draggedOrbVisual = null;

            // Stop all running coroutines (intro banner animation, etc.)
            // before destroying the GameObjects they animate.
            StopAllCoroutines();

            // Destroy intro banner if still visible
            if (_introBanner != null)
            {
                Destroy(_introBanner);
                _introBanner = null;
            }
        }

        private Color GetOrbColor(OrbColor color)
        {
            return OrbColors.TryGetValue(color, out var c) ? c : Color.white;
        }

        // Programmatic tier ramp: keep the base hue/saturation/lightness, then
        // raise saturation per tier (T1 = 0.80 sat → T5 = 1.00 sat) via HSV, so
        // every OrbColor in the enum resolves without a hand-maintained ramp.
        private Color GetOrbColor(OrbColor color, OrbTier tier)
        {
            var baseColor = GetOrbColor(color);
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            s = Mathf.Clamp01(s * (0.80f + 0.05f * ((int)tier - 1)));
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>
        /// Pip overlay (1–5 tier pips, bottom-center of orb). The pip PNGs are
        /// artist-owned; they currently import as Default texture, so Sprite.Create
        /// only succeeds when the texture is readable. Falls back silently otherwise.
        /// </summary>
        private void AttachPipOverlay(GameObject orbVisual, OrbTier tier)
        {
            if (orbVisual == null) return;

            var path = $"UI/PipOverlays/tier_pips_T{(int)tier}";
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                // Imported as Default (textureType 0): wrap in Sprite.Create if readable.
                var tex = Resources.Load<Texture2D>(path);
                if (tex == null || !tex.isReadable) return;
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            if (sprite == null) return;

            var go = new GameObject("PipOverlay");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5; // above orb
            sr.color = new Color(1f, 1f, 1f, 0.7f); // semi-transparent
            go.transform.SetParent(orbVisual.transform, false);
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = new Vector3(0f, -0.5f, 0f); // bottom-center, adjust to orb size
        }

        private static Sprite _whiteSprite;

        private Sprite CreateWhiteSprite()
        {
            if (_whiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
            }
            return _whiteSprite;
        }

        // Loads the board-mounted obstacle fixture art from Resources/Board/,
        // choosing the variant by the level's theme.  Falls back to a readable
        // stone tile if the themed variant isn't present.  Never null.
        private Sprite LoadObstacleSprite()
        {
            var theme = _level != null ? _level.DisplayName ?? "" : "";
            var lower = theme.ToLowerInvariant();
            string variant = "obstacle_stone_256";
            if (lower.Contains("ice")) variant = "obstacle_ice_256";
            else if (lower.Contains("vine") || lower.Contains("garden") || lower.Contains("forest")) variant = "obstacle_vines_256";

            var spr = Resources.Load<Sprite>($"Board/{variant}");
            if (spr != null) return spr;

            // Themed sprite missing — fall back to the stone fixture so obstacles
            // never collapse back to the near-black placeholder square.
            spr = Resources.Load<Sprite>("Board/obstacle_stone_256");
            if (spr != null) return spr;

            return CreateWhiteSprite();
        }

        private Sprite CreateDefaultSprite(OrbColor color)
        {
            // Create a simple circle sprite procedurally. Resolution/PPU match the
            // artist's 256px orb art so fallback orbs are the same world size.
            int resolution = 256;
            var tex = new Texture2D(resolution, resolution);
            var pixels = new Color[resolution * resolution];
            float center = resolution / 2f;
            float radius = resolution / 2f - 2f;
            for (int py = 0; py < resolution; py++)
            {
                for (int px = 0; px < resolution; px++)
                {
                    float dist = Mathf.Sqrt((px - center) * (px - center) + (py - center) * (py - center));
                    pixels[py * resolution + px] = dist <= radius ? GetOrbColor(color) : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 256f);
        }

        private Sprite CreateRingSprite()
        {
            // Procedural ring (annulus) for the lock flash until the strip arrives.
            int resolution = 64;
            var tex = new Texture2D(resolution, resolution);
            var pixels = new Color[resolution * resolution];
            float center = resolution / 2f;
            float outerR = resolution / 2f - 2f;
            float innerR = outerR * 0.62f;
            for (int py = 0; py < resolution; py++)
            {
                for (int px = 0; px < resolution; px++)
                {
                    float dist = Mathf.Sqrt((px - center) * (px - center) + (py - center) * (py - center));
                    float alpha = 0f;
                    if (dist <= outerR && dist >= innerR)
                    {
                        // Soft edges: ramp alpha over the 1px band at each edge
                        float edge = Mathf.Min(dist - (innerR - 1f), (outerR + 1f) - dist);
                        alpha = Mathf.Clamp01(edge);
                    }
                    pixels[py * resolution + px] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
