// SPDX-License-Identifier: MIT
// Chroma Vale — OnboardingController: merge-mode tutorial assistance layer.
//
// ONE new file, zero edits to MergeBoardView. Self-attaches to the board view
// at scene load via RuntimeInitializeOnLoadMethod, so it works without any
// scene or prefab wiring. Delivers the Wave-5 onboarding spec:
//
//   1. Level 1 hand demo — TutorialHandPointer physically drags orb A onto
//      orb B (visual only, input never blocked). Runs once at the start of
//      L1, before the player's first action; cancelled if the player touches.
//   2. Reactive merge glow — while the player drags, every VALID merge target
//      pulses a soft glow ring (scale pulse 1.0 → 1.14 → 1.0). Targets come
//      from a lightweight BoardController mirror + MergeRules.CanMerge.
//   3. Wean-off ladder — merge 1 = hand + glow, merge 2 = glow only,
//      merge 3+ = nothing. Merge events are detected by polling the orb
//      visual count (MergeBoardView exposes no public event).
//   4. Hesitation chips — idle >= 5s during L1–L3 shows a one-word chip
//      ("Drag" / "Match" / "Merge"), max 2 per level, never past L3,
//      dismissed on touch.
//
// The controller lives on the SAME GameObject as MergeBoardView, so it is
// automatically disabled while the level-select screen hides the board.

using System;
using System.Collections;
using System.Collections.Generic;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using ChromaVale.Infrastructure.LevelData;
using ChromaVale.Presentation.Views.Components;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChromaVale.Presentation.Views
{
    /// <summary>
    /// Non-invasive onboarding layer for merge mode. Observes the board view
    /// and the player's input; never modifies board state or view internals.
    /// </summary>
    public sealed class OnboardingController : MonoBehaviour
    {
        // ── Design constants (match MergeBoardView canon; do not guess) ──

        /// <summary>Idle time before a hesitation chip appears (spec: 5s).</summary>
        private const float IdleChipThreshold = 5f;

        /// <summary>Max hesitation chips per level (spec: 2).</summary>
        private const int MaxChipsPerLevel = 2;

        /// <summary>Wait after level load so the intro banner (0.3+1.5+0.4s) clears.</summary>
        private const float DemoStartDelay = 2.5f;

        /// <summary>Time the hand bounces on orb A before the drag gesture.</summary>
        private const float HandSettleDelay = 1.4f;

        /// <summary>Duration of the hand's arc transition A → B.</summary>
        private const float HandDragDuration = 1.0f;

        /// <summary>Hold on orb B after the drag before fading out.</summary>
        private const float HandHoldDelay = 1.0f;

        /// <summary>Glow ring pulse speed (rad/s — ~1 pulse per second).</summary>
        private const float GlowPulseSpeed = 6f;

        /// <summary>Glow ring scale boost: 1.0 → 1.14 → 1.0 pulse.</summary>
        private const float GlowScaleBoost = 0.14f;

        /// <summary>Glow ring sits this much larger than its orb.</summary>
        private const float GlowRingOversize = 1.25f;

        /// <summary>How often (s) we re-parse the HUD for the level number.</summary>
        private const float HudPollInterval = 0.2f;

        /// <summary>Chip text per level (index = level number; L4+ → none).</summary>
        private static readonly string[] ChipTextByLevel = { null, "Drag", "Match", "Merge" };

        // ── State ──

        private MergeLevelRepository _levelRepo;
        private BoardController _mirror;
        private LevelData _levelData;
        private int _levelNumber;
        private float _levelLoadTime;

        private TutorialHandPointer _hand;
        private Coroutine _demoRoutine;
        private bool _demoActive;
        private bool _demoPlayedThisLoad;

        private readonly List<GameObject> _glowRings = new(16);
        private (int x, int y)? _dragSourceCell;
        private (int x, int y)? _lastDragSource;
        private (int x, int y)? _lastDragTarget;
        private bool _isDragging;

        private float _lastInputTime;
        private int _chipsShownThisLevel;
        private GameObject _chipRoot;
        private CanvasGroup _chipGroup;

        private int _mergeCount;
        private int _lastOrbCount = -1;
        private int _lastParsedLevel;
        private bool _pendingLevelReset = true;
        private float _nextHudPoll;
        private TextMeshProUGUI _hudText;
        private bool _hudScanned;

        private Sprite _glowSprite;
        private Sprite _chipBgSprite;

        // Derived grid geometry (from orb visuals — robust to scene config).
        private float _tileSize = 1.2f;
        private Vector2 _boardOffset;

        // ── Auto-attach ──
        // The scene has no OnboardingController GO (board-fix card owns scenes),
        // so we self-install onto MergeBoardView's GameObject at scene load.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToBoard()
        {
            var board = FindAnyObjectByType<MergeBoardView>();
            if (board != null && board.GetComponent<OnboardingController>() == null)
                board.gameObject.AddComponent<OnboardingController>();
        }

        private void Awake()
        {
            _levelRepo = new MergeLevelRepository(new ResourcesLevelJsonProvider());
        }

        private void OnEnable()
        {
            // Board (re)activated — the level may have changed since we were off.
            // The poller will do the authoritative reset once the HUD is current.
            _pendingLevelReset = true;
            _lastParsedLevel = -1;
        }

        private void OnDisable()
        {
            StopDemo();
            ClearGlow();
            HideChip();
            _isDragging = false;
            _dragSourceCell = null;
        }

        private void Update()
        {
            TickHudPoll();
            if (_levelNumber <= 0 || _levelData == null || _mirror == null)
            {
                ClearGlow();
                return;
            }

            TickOrbCount();
            TickInput();
            TickDragGlow();
            TickDemo();
            TickChip();
        }

        // ── Level tracking (HUD poll — MergeBoardView exposes no event) ──

        private void TickHudPoll()
        {
            if (Time.time < _nextHudPoll) return;
            _nextHudPoll = Time.time + HudPollInterval;

            int parsed = ParseHudLevel();
            if (_pendingLevelReset || parsed != _lastParsedLevel)
            {
                _lastParsedLevel = parsed;
                _pendingLevelReset = false;
                if (parsed > 0)
                    ResetForLevel(parsed);
                else
                    ClearLevelState();
            }
        }

        private int ParseHudLevel()
        {
            var hud = FindHudText();
            if (hud == null || string.IsNullOrEmpty(hud.text)) return -1;
            if (!hud.text.StartsWith("Level ", StringComparison.Ordinal)) return -1;
            int sp = hud.text.IndexOf(' ', 6);
            if (sp <= 6) return -1;
            return int.TryParse(hud.text.Substring(6, sp - 6), out int n) ? n : -1;
        }

        private TextMeshProUGUI FindHudText()
        {
            if (_hudText != null) return _hudText;
            if (_hudScanned) return null;
            _hudScanned = true;
            // The HUD is the TMP that starts with "Level N" and mentions Moves.
            foreach (var tmp in FindObjectsByType<TextMeshProUGUI>())
            {
                if (tmp != null && tmp.text != null &&
                    tmp.text.StartsWith("Level ", StringComparison.Ordinal) &&
                    tmp.text.Contains("Moves:"))
                {
                    _hudText = tmp;
                    return tmp;
                }
            }
            return null;
        }

        private void ClearLevelState()
        {
            _levelNumber = 0;
            _levelData = null;
            _mirror = null;
            _mergeCount = 0;
            _lastOrbCount = -1;
            StopDemo();
            ClearGlow();
            HideChip();
        }

        private void ResetForLevel(int levelNumber)
        {
            if (_levelRepo == null)
                _levelRepo = new MergeLevelRepository(new ResourcesLevelJsonProvider());
            _levelNumber = levelNumber;
            _levelData = levelNumber > 0 && levelNumber <= _levelRepo.LevelCount
                ? _levelRepo.GetMergeLevel(levelNumber)
                : null;

            _mirror = new BoardController();
            if (_levelData != null)
                _mirror.Initialize(_levelData);

            _mergeCount = 0;
            _chipsShownThisLevel = 0;
            _demoPlayedThisLoad = false;
            _demoActive = false;
            _isDragging = false;
            _dragSourceCell = null;
            _lastDragSource = null;
            _lastDragTarget = null;
            _lastInputTime = Time.time;
            _levelLoadTime = Time.time;

            StopDemo();
            ClearGlow();
            HideChip();
            DeriveGridGeometry();
            _lastOrbCount = CountOrbVisuals();
        }

        // ── Merge detection (orb-count poll per spec) ──

        private void TickOrbCount()
        {
            int count = CountOrbVisuals();
            if (count > _lastOrbCount)
            {
                // Board grew — a fresh board was loaded (Replay / Next / re-entry).
                ResetForLevel(_lastParsedLevel > 0 ? _lastParsedLevel : _levelNumber);
                return;
            }
            if (count < _lastOrbCount)
            {
                _mergeCount++;
                _lastOrbCount = count;

                // Wean-off: after merge 2 the reactive glow stops.
                if (_mergeCount >= 2) ClearGlow();

                // Sanity: mirror must agree with the visual count, else drop it
                // (graceful: glow turns off, chips/demo unaffected).
                if (_mirror != null && CountMirrorOrbs() != count)
                    _mirror = null;
            }
        }

        private int CountOrbVisuals()
        {
            int n = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).name.Contains("Orb"))
                    n++;
            }
            return n;
        }

        private int CountMirrorOrbs()
        {
            if (_mirror == null) return -1;
            int n = 0;
            for (int y = 0; y < _mirror.Height; y++)
            for (int x = 0; x < _mirror.Width; x++)
            {
                if (_mirror.GetOrbAt(new GridPosition(x, y)) != null) n++;
            }
            return n;
        }

        // ── Input observation (mirrors MergeBoardView, never blocks it) ──

        private bool _wasPressed;
        private Vector2 _lastPointerScreen;

        private void TickInput()
        {
            bool pressed = IsPointerPressed();
            Vector2 screen = ReadPointerScreen();
            bool down = pressed && !_wasPressed;
            bool up = !pressed && _wasPressed;
            _wasPressed = pressed;

            if (down || up || screen != _lastPointerScreen)
                _lastInputTime = Time.time;
            _lastPointerScreen = screen;

            if (down)
            {
                // Any first touch cancels a pending (not yet started) hand demo.
                if (_levelNumber == 1 && !_demoPlayedThisLoad && !_demoActive)
                {
                    _demoPlayedThisLoad = true;
                    StopDemo();
                }

                if (_chipRoot != null)
                {
                    HideChip();
                    _chipsShownThisLevel++;
                }

                if (_mirror == null || _mirror.IsLevelComplete) return;
                var cell = CellAtWorld(PointerWorldPosition(screen));
                if (cell.HasValue &&
                    _mirror.GetOrbAt(new GridPosition(cell.Value.x, cell.Value.y)) != null)
                {
                    _isDragging = true;
                    _dragSourceCell = cell;
                    _lastDragSource = cell;
                }
            }

            if (_isDragging && up)
            {
                var target = CellAtWorld(PointerWorldPosition(screen));
                _lastDragTarget = target;
                if (_lastDragSource.HasValue && target.HasValue)
                {
                    _mirror.TryMergeAt(
                        new GridPosition(_lastDragSource.Value.x, _lastDragSource.Value.y),
                        new GridPosition(target.Value.x, target.Value.y));
                }
                _isDragging = false;
                _dragSourceCell = null;
                ClearGlow();
            }

            if (!_isDragging && _mirror != null && _mirror.IsLevelComplete)
                ClearGlow();
        }

        private static bool IsPointerPressed()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private static Vector2 ReadPointerScreen()
        {
            if (Mouse.current != null && Mouse.current.enabled)
                return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            return Vector2.zero;
        }

        private Vector3 PointerWorldPosition(Vector2 screen)
        {
            return Camera.main != null
                ? Camera.main.ScreenToWorldPoint(screen)
                : Vector3.zero;
        }

        // ── Reactive glow on valid merge targets ──

        private void TickDragGlow()
        {
            if (!_isDragging || _mergeCount >= 2 || _mirror == null)
            {
                ClearGlow();
                return;
            }
            if (!_dragSourceCell.HasValue) return;

            var src = new GridPosition(_dragSourceCell.Value.x, _dragSourceCell.Value.y);
            var srcOrb = _mirror.GetOrbAt(src);
            if (srcOrb == null)
            {
                ClearGlow();
                return;
            }

            // Compute the valid target cells (same rules as the real board).
            List<Vector2Int> targets = new(8);
            for (int y = 0; y < _mirror.Height; y++)
            {
                for (int x = 0; x < _mirror.Width; x++)
                {
                    if (x == src.X && y == src.Y) continue;
                    var orb = _mirror.GetOrbAt(new GridPosition(x, y));
                    if (orb != null && MergeRules.CanMerge(srcOrb, orb))
                        targets.Add(new Vector2Int(x, y));
                }
            }

            EnsureGlowRings(targets);
            PulseGlowRings();
        }

        private void EnsureGlowRings(List<Vector2Int> cells)
        {
            // Remove rings for cells that are no longer valid.
            for (int i = _glowRings.Count - 1; i >= 0; i--)
            {
                var ring = _glowRings[i];
                if (ring == null) { _glowRings.RemoveAt(i); continue; }
                var cell = CellOfVisual(ring.transform.parent);
                if (!cell.HasValue || !cells.Contains(new Vector2Int(cell.Value.x, cell.Value.y)))
                {
                    Destroy(ring);
                    _glowRings.RemoveAt(i);
                }
            }

            // Add missing rings.
            foreach (var c in cells)
            {
                bool present = false;
                foreach (var ring in _glowRings)
                {
                    if (ring == null) continue;
                    var cell = CellOfVisual(ring.transform.parent);
                    if (cell.HasValue && cell.Value.x == c.x && cell.Value.y == c.y)
                    {
                        present = true;
                        break;
                    }
                }
                if (present) continue;

                var orbVisual = FindVisualAtCell(c.x, c.y);
                if (orbVisual == null) continue;

                var go = new GameObject("TargetGlowRing");
                go.transform.SetParent(orbVisual.transform, false);
                go.transform.localPosition = Vector3.zero;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetGlowSprite();
                sr.color = new Color(1f, 1f, 1f, 0.55f);
                sr.sortingOrder = -1; // halo behind the orb sprite
                _glowRings.Add(go);
            }
        }

        private void PulseGlowRings()
        {
            float k = (Mathf.Sin(Time.time * GlowPulseSpeed) + 1f) * 0.5f;
            for (int i = _glowRings.Count - 1; i >= 0; i--)
            {
                var ring = _glowRings[i];
                if (ring == null) { _glowRings.RemoveAt(i); continue; }
                var parent = ring.transform.parent;
                if (parent == null) { Destroy(ring); _glowRings.RemoveAt(i); continue; }
                float baseScale = parent.localScale.x * GlowRingOversize;
                ring.transform.localScale = Vector3.one * (baseScale * (1f + GlowScaleBoost * k));
            }
        }

        private void ClearGlow()
        {
            foreach (var ring in _glowRings)
            {
                if (ring != null) Destroy(ring);
            }
            _glowRings.Clear();
        }

        // ── Level 1 hand demo (visual only, input never blocked) ──

        private void TickDemo()
        {
            if (_levelNumber != 1 || _mergeCount > 0 || _demoPlayedThisLoad || _demoActive)
                return;
            if (_mirror == null || _mirror.IsLevelComplete) return;
            if (_isDragging) return;
            if (Time.time - _levelLoadTime < DemoStartDelay) return;

            _demoRoutine = StartCoroutine(DemoRoutine());
        }

        private void StopDemo()
        {
            if (_demoRoutine != null)
            {
                StopCoroutine(_demoRoutine);
                _demoRoutine = null;
            }
            if (_demoActive && _hand != null)
            {
                _hand.FadeOut();
                _demoActive = false;
            }
        }

        private IEnumerator DemoRoutine()
        {
            _demoActive = true;
            _demoRoutine = null; // routine owns itself now

            var pair = PickDemoPair();
            if (!pair.HasValue)
            {
                _demoActive = false;
                _demoPlayedThisLoad = true;
                yield break;
            }

            var visualA = FindVisualAtCell(pair.Value.Item1.x, pair.Value.Item1.y);
            var visualB = FindVisualAtCell(pair.Value.Item2.x, pair.Value.Item2.y);
            if (visualA == null || visualB == null || Camera.main == null)
            {
                _demoActive = false;
                _demoPlayedThisLoad = true;
                yield break;
            }

            Vector2 screenA = Camera.main.WorldToScreenPoint(visualA.transform.position);
            Vector2 screenB = Camera.main.WorldToScreenPoint(visualB.transform.position);

            _hand = EnsureHand();

            // 1. Point at orb A and bounce.
            _hand.PointAt(screenA, "Drag", true);
            yield return new WaitForSeconds(HandSettleDelay);

            if (!_demoActive) yield break;

            // 2. Arc-drag onto orb B (the merge gesture).
            _hand.TransitionTo(screenB, "Drag", true, HandDragDuration);
            yield return new WaitForSeconds(HandDragDuration + HandHoldDelay);

            if (!_demoActive) yield break;

            // 3. Fade away; glow + chips take over.
            _hand.FadeOut();
            _demoActive = false;
            _demoPlayedThisLoad = true;
        }

        private ((int x, int y), (int x, int y))? PickDemoPair()
        {
            if (_mirror == null || _levelData == null) return null;
            for (int y = 0; y < _mirror.Height; y++)
            {
                for (int x = 0; x < _mirror.Width; x++)
                {
                    var a = _mirror.GetOrbAt(new GridPosition(x, y));
                    if (a == null) continue;
                    for (int yy = 0; yy < _mirror.Height; yy++)
                    {
                        for (int xx = 0; xx < _mirror.Width; xx++)
                        {
                            if (xx == x && yy == y) continue;
                            var b = _mirror.GetOrbAt(new GridPosition(xx, yy));
                            if (b != null && MergeRules.CanMerge(a, b))
                                return ((x, y), (xx, yy));
                        }
                    }
                }
            }
            return null;
        }

        private TutorialHandPointer EnsureHand()
        {
            if (_hand != null) return _hand;
            var go = new GameObject("OnboardingHand");
            go.transform.SetParent(transform, false);
            _hand = go.AddComponent<TutorialHandPointer>();
            return _hand;
        }

        // ── Hesitation chips (L1–L3, idle ≥ 5s, ≤ 2/level, dismiss on touch) ──

        private void TickChip()
        {
            if (_chipRoot != null) return;
            if (_levelNumber < 1 || _levelNumber > 3) return;
            if (_chipsShownThisLevel >= MaxChipsPerLevel) return;
            if (_mirror == null || _mirror.IsLevelComplete) return;

            // Don't nag while teaching is happening; restart the idle clock.
            if (_demoActive || _isDragging)
            {
                _lastInputTime = Time.time;
                return;
            }

            if (Time.time - _lastInputTime < IdleChipThreshold) return;

            var text = ChipTextByLevel[_levelNumber];
            if (string.IsNullOrEmpty(text)) return;
            ShowChip(text);
        }

        private void ShowChip(string text)
        {
            var canvasGo = new GameObject("OnboardingChips");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30; // above HUD, below intro banner (50) and hand (100)
            canvasGo.AddComponent<CanvasScaler>();

            var root = new GameObject("Chip");
            root.transform.SetParent(canvasGo.transform, false);
            _chipGroup = root.AddComponent<CanvasGroup>();
            _chipGroup.alpha = 0f;

            var img = root.AddComponent<Image>();
            img.sprite = GetChipBgSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.051f, 0.067f, 0.09f, 0.88f);
            img.raycastTarget = false;

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.66f);
            rect.anchorMax = new Vector2(0.5f, 0.66f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 42f);
            rect.anchoredPosition = Vector2.zero;

            var label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 1f, 1f, 0.95f);
            tmp.raycastTarget = false;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            _chipRoot = root;
            StartCoroutine(FadeChip(1f));
        }

        private void HideChip()
        {
            if (_chipRoot == null) return;
            var root = _chipRoot;
            _chipRoot = null;
            if (root != null) Destroy(root);
        }

        private IEnumerator FadeChip(float targetAlpha)
        {
            if (_chipGroup == null) yield break;
            float t = 0f;
            float from = _chipGroup.alpha;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                _chipGroup.alpha = Mathf.Lerp(from, targetAlpha, t / 0.25f);
                yield return null;
            }
            _chipGroup.alpha = targetAlpha;
        }

        // ── Grid geometry (derived from orb visuals — no MergeBoardView reads) ──

        private void DeriveGridGeometry()
        {
            _tileSize = 1.2f;
            _boardOffset = Vector2.zero;
            if (_levelData == null || _mirror == null) return;

            // Collect orb visual positions.
            var positions = new List<Vector3>(32);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name.Contains("Orb"))
                    positions.Add(child.position);
            }
            if (positions.Count < 2) return;

            // Tile size = min positive spacing along an axis.
            float best = float.MaxValue;
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = i + 1; j < positions.Count; j++)
                {
                    float dx = Mathf.Abs(positions[i].x - positions[j].x);
                    float dy = Mathf.Abs(positions[i].y - positions[j].y);
                    if (dx > 0.05f && dy < 0.05f) best = Mathf.Min(best, dx);
                    if (dy > 0.05f && dx < 0.05f) best = Mathf.Min(best, dy);
                }
            }
            if (best >= 0.4f && best <= 4f) _tileSize = best;

            // Board is centered at world origin (MergeBoardView computes
            // offset = -size/2 * tileSize), so the offset follows from W/H/tile.
            _boardOffset = new Vector2(
                -_levelData.Width * _tileSize * 0.5f,
                -_levelData.Height * _tileSize * 0.5f);
        }

        private (int x, int y)? CellAtWorld(Vector3 world)
        {
            if (_levelData == null) return null;
            float gx = (world.x - _boardOffset.x - _tileSize * 0.5f) / _tileSize;
            float gy = (world.y - _boardOffset.y - _tileSize * 0.5f) / _tileSize;
            int x = Mathf.RoundToInt(gx);
            int y = Mathf.RoundToInt(gy);
            if (x < 0 || x >= _levelData.Width || y < 0 || y >= _levelData.Height)
                return null;
            return (x, y);
        }

        private Vector3 ExpectedVisualPosition(int x, int y)
        {
            return new Vector3(
                x * _tileSize + _boardOffset.x + _tileSize * 0.5f,
                y * _tileSize + _boardOffset.y + _tileSize * 0.5f,
                0f);
        }

        private Transform FindVisualAtCell(int x, int y)
        {
            Vector3 expected = ExpectedVisualPosition(x, y);
            float tolerance = Mathf.Max(0.35f, _tileSize * 0.3f);
            Transform best = null;
            float bestDist = tolerance;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.name.Contains("Orb")) continue;
                float d = Vector3.Distance(child.position, expected);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = child;
                }
            }
            return best;
        }

        private (int x, int y)? CellOfVisual(Transform visual)
        {
            if (visual == null || _mirror == null) return null;
            float bestDist = Mathf.Max(0.35f, _tileSize * 0.3f);
            (int x, int y)? best = null;
            for (int y = 0; y < _mirror.Height; y++)
            {
                for (int x = 0; x < _mirror.Width; x++)
                {
                    if (_mirror.GetOrbAt(new GridPosition(x, y)) == null) continue;
                    float d = Vector3.Distance(visual.position, ExpectedVisualPosition(x, y));
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = (x, y);
                    }
                }
            }
            return best;
        }

        // ── Procedural sprites (no asset dependencies) ──

        private Sprite GetGlowSprite()
        {
            if (_glowSprite == null)
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - half) / half;
                        float dy = (y - half) / half;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r);
                        a = a * a * (3f - 2f * a); // smoothstep falloff
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * 0.95f));
                    }
                }
                tex.Apply();
                _glowSprite = Sprite.Create(
                    tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
            return _glowSprite;
        }

        private Sprite GetChipBgSprite()
        {
            if (_chipBgSprite == null)
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                const float corner = 18f;
                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = Mathf.Abs(x - half) + 0.5f;
                        float py = Mathf.Abs(y - half) + 0.5f;
                        float qx = Mathf.Max(0f, px - (half - corner));
                        float qy = Mathf.Max(0f, py - (half - corner));
                        bool inside = Mathf.Sqrt(qx * qx + qy * qy) <= corner;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
                    }
                }
                tex.Apply();
                _chipBgSprite = Sprite.Create(
                    tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
                    0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
            }
            return _chipBgSprite;
        }
    }
}
