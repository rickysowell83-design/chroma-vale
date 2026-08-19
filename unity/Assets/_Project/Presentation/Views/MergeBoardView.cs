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
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private int _maxLevel;
        private float _boardOffsetX;
        private float _boardOffsetY;

        // ── Orb visuals ──
        // Maps grid position → SpriteRenderer instance for each orb on the board
        private Dictionary<(int x, int y), SpriteRenderer> _orbVisuals = new();

        // ── Input state ──
        private bool _wasPointerDown;          // Pointer pressed last frame (edge detection)
        private Vector2 _lastPointerScreenPos; // Latest pointer position (screen space)
        private (int x, int y)? _hoverCell;    // Cell currently highlighted by hover
        private Coroutine _snapBackCoroutine;
        private Vector3 _draggedBaseScale = Vector3.one;
        private Sprite _lockFlashSprite;
        private readonly Dictionary<(int x, int y), Coroutine> _targetPulseRoutines = new();

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
            { OrbColor.Teal,      new Color(0f, 0.5f, 0.5f) },
            { OrbColor.Vermilion, new Color(0.886f, 0.2f, 0.2f) },
            { OrbColor.Amber,     new Color(0.937f, 0.6f, 0f) },
            { OrbColor.Slate,     new Color(0.424f, 0.459f, 0.49f) }, // #6C757D
        };

        // ── Lifecycle ──

        private void Start()
        {
            _levelRepo = new MergeLevelRepository();
            _maxLevel = _levelRepo.LevelCount;
            _levelNumber = Mathf.Clamp(_startLevel, 1, _maxLevel);

            LoadLevel(_levelNumber);
        }

        private void LoadLevel(int levelNumber)
        {
            // Clear previous board state
            ClearBoard();

            _level = _levelRepo.GetMergeLevel(levelNumber);

            // Create BoardController and initialize
            _board = new BoardController();
            _board.Initialize(_level);

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

            // TODO: Create grid background tiles (simple quads or sprites)
            // For now, just set up the camera to frame the board
            SetupCamera();
        }

        /// <summary>
        /// Creates a restoration target cell visual.  Pulses subtly (idle state)
        /// so the player sees where orbs need to go.  When a correct (color, tier)
        /// orb is placed, the target locks with a green flash.
        /// TODO: wire target_idle_pulse_strip.png (4-frame loop, 480ms) and
        /// target_lock_flash_strip.png (4-frame, 200ms) when artist delivers them.
        /// </summary>
        private void CreateTargetVisual(int x, int y, OrbColor color, OrbTier tier)
        {
            Vector3 worldPos = GridToWorld(x, y);
            var go = new GameObject($"Target_{x}_{y}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            // Fallback: semi-transparent colored circle until strips arrive
            sr.sprite = CreateDefaultSprite(color);
            sr.color = new Color(GetOrbColor(color).r, GetOrbColor(color).g, GetOrbColor(color).b, 0.25f);
            sr.sortingOrder = -1; // behind orbs

            _targetVisuals[(x, y)] = sr;

            // Idle pulse (procedural fallback until target_idle_pulse_strip.png arrives)
            _targetPulseRoutines[(x, y)] = StartCoroutine(TargetIdlePulseRoutine(go.transform, sr));
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
                        Debug.Log($"[MergeBoardView] Target locked at ({x},{y}) — {orb.Color} T{orb.Tier}");
                        break;
                    }
                }
            }
        }

        // ── Target animations (procedural fallback until strips arrive) ──

        /// <summary>
        /// Idle pulse loop: 4 frames × 120ms = 480ms, scale 1.0 → 1.05 → 1.0 → 0.98 → loop.
        /// TODO: replace with target_idle_pulse_strip.png when artist delivers.
        /// </summary>
        private System.Collections.IEnumerator TargetIdlePulseRoutine(Transform targetTransform, SpriteRenderer sr)
        {
            Vector3 baseScale = targetTransform.localScale;
            float[] keyframes = { 1.00f, 1.05f, 1.00f, 0.98f };
            float frameDuration = TargetPulseDuration / 4f; // 120ms
            float frameTimer = 0f;
            int frame = 0;

            while (targetTransform != null)
            {
                frameTimer += Time.deltaTime;
                if (frameTimer >= frameDuration)
                {
                    frameTimer = 0f;
                    frame = (frame + 1) % keyframes.Length;
                    targetTransform.localScale = baseScale * keyframes[frame];
                }
                yield return null;
            }
        }

        /// <summary>
        /// Green ring expand+fade on target lock.  4 frames × 50ms = 200ms.
        /// TODO: replace with target_lock_flash_strip.png when artist delivers.
        /// </summary>
        private void PlayTargetLockFlash(int x, int y)
        {
            if (_lockFlashSprite == null) _lockFlashSprite = CreateRingSprite();

            var worldPos = GridToWorld(x, y);
            var go = new GameObject($"LockFlash_{x}_{y}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _lockFlashSprite;
            sr.color = new Color(0.20f, 1f, 0.40f, 0.95f); // green ring
            sr.sortingOrder = 1; // above orbs

            StartCoroutine(TargetLockFlashRoutine(go));
        }

        private System.Collections.IEnumerator TargetLockFlashRoutine(GameObject flashGo)
        {
            float t = 0f;
            var sr = flashGo.GetComponent<SpriteRenderer>();
            Color startColor = sr.color;
            Vector3 startScale = flashGo.transform.localScale;

            while (t < TargetLockDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / TargetLockDuration);
                float e = k * k * (3f - 2f * k);
                flashGo.transform.localScale = startScale * Mathf.Lerp(0.8f, 1.6f, e);
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, e);
                sr.color = c;
                yield return null;
            }

            Destroy(flashGo);
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            float boardWidth = _level.Width * _tileSize;
            float boardHeight = _level.Height * _tileSize;
            float pad = _tileSize * 0.8f;
            float heightSize = (boardHeight / 2f) + pad;
            float aspect = Screen.width > 0 ? (float)Screen.width / Screen.height : 1.7778f;
            float widthSize = ((boardWidth / 2f) + pad) / aspect;
            cam.orthographicSize = Mathf.Max(heightSize, widthSize);
            cam.transform.position = new Vector3(0f, 0f, -10f);
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

            GameObject orbGo;
            if (_orbPrefab != null)
            {
                orbGo = Instantiate(_orbPrefab, worldPos, Quaternion.identity, transform);
            }
            else
            {
                // Fallback: create a simple quad with color
                orbGo = new GameObject($"Orb_{x}_{y}");
                orbGo.transform.SetParent(transform, false);
                orbGo.transform.position = worldPos;
                var sr = orbGo.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDefaultSprite(color);
                sr.color = GetOrbColor(color);
            }

            var spriteRenderer = orbGo.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = GetOrbColor(color);
                // Scale based on tier (T1=0.8, T2=0.85, T3=0.9, T4=0.95, T5=1.0)
                float scale = 0.8f + ((int)tier - 1) * 0.05f;
                orbGo.transform.localScale = Vector3.one * scale;
            }

            _orbVisuals[(x, y)] = spriteRenderer;
        }

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
            if (gridPos.HasValue && gridPos.Value != _dragSource && IsAdjacent(_dragSource, gridPos.Value))
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

        private static bool IsAdjacent((int x, int y) a, (int x, int y) b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        // ── Snap-back animation (procedural fallback until snap_back_strip.png) ──

        private void StartSnapBack(GameObject orbVisual, Vector3 origin, Vector3 baseScale)
        {
            StopSnapBack();
            _snapBackCoroutine = StartCoroutine(SnapBackRoutine(orbVisual, origin, baseScale));
        }

        private void StopSnapBack()
        {
            if (_snapBackCoroutine != null)
            {
                StopCoroutine(_snapBackCoroutine);
                _snapBackCoroutine = null;
            }
        }

        private System.Collections.IEnumerator SnapBackRoutine(GameObject orbVisual, Vector3 origin, Vector3 baseScale)
        {
            // 4 frames × 60ms = 240ms: quick "nope" — shrink + fade back to origin
            float t = 0f;
            Vector3 startPos = orbVisual.transform.position;
            var sr = orbVisual.GetComponent<SpriteRenderer>();
            Color startColor = sr != null ? sr.color : Color.white;

            while (t < SnapBackDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / SnapBackDuration);
                float e = k * k * (3f - 2f * k); // smoothstep
                orbVisual.transform.position = Vector3.Lerp(startPos, origin, e);
                orbVisual.transform.localScale = baseScale * (1f - 0.3f * e);
                if (sr != null)
                {
                    Color c = startColor;
                    c.a = Mathf.Lerp(startColor.a, 0f, e);
                    sr.color = c;
                }
                yield return null;
            }

            // Restore original state
            orbVisual.transform.position = origin;
            orbVisual.transform.localScale = baseScale;
            if (sr != null) sr.color = startColor;
            _snapBackCoroutine = null;
        }

        // ── Board Event Handling ──

        private void HandleBoardChanged(BoardChange change)
        {
            // TODO: Animate the change using design constants:
            //   Merge:  640ms (8 frames × 80ms) — squash → flash → settle
            //   Spawn:  480ms (8 frames × 60ms) — scale-in + overshoot settle
            //   Flash color = RESULT orb color, not input
            // For now, sync visuals immediately + check target locks

            switch (change.Type)
            {
                case ChangeType.OrbAdded:
                    if (change.NewOrb != null)
                    {
                        SpawnOrbVisual(change.Position.X, change.Position.Y,
                                       change.NewOrb.Color, change.NewOrb.Tier);
                        CheckTargetLock(change.Position.X, change.Position.Y, change.NewOrb);
                    }
                    break;

                case ChangeType.OrbRemoved:
                    RemoveOrbVisual(change.Position.X, change.Position.Y);
                    break;

                case ChangeType.OrbTransformed:
                    RemoveOrbVisual(change.Position.X, change.Position.Y);
                    if (change.NewOrb != null)
                    {
                        SpawnOrbVisual(change.Position.X, change.Position.Y,
                                       change.NewOrb.Color, change.NewOrb.Tier);
                        CheckTargetLock(change.Position.X, change.Position.Y, change.NewOrb);
                    }
                    break;
            }
        }

        private void HandleLevelComplete(LevelResult result)
        {
            Debug.Log($"[MergeBoardView] Level Complete! Moves: {result.MovesUsed}, " +
                      $"Par: {result.Par}, Stars: {result.Stars}");

            // TODO: Show win popup, offer next level / retry
            // For now, auto-advance after 2 seconds
            Invoke(nameof(NextLevel), 2f);
        }

        private void NextLevel()
        {
            _levelNumber++;
            if (_levelNumber > _maxLevel)
            {
                Debug.Log("[MergeBoardView] All levels complete!");
                return;
            }
            LoadLevel(_levelNumber);
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
            return orb == null ? 1f : 0.8f + ((int)orb.Tier - 1) * 0.05f;
        }

        private void ClearBoard()
        {
            // Unsubscribe from previous board events
            if (_board != null)
            {
                _board.OnBoardChanged -= HandleBoardChanged;
                _board.OnLevelComplete -= HandleLevelComplete;
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
        }

        private Color GetOrbColor(OrbColor color)
        {
            return OrbColors.TryGetValue(color, out var c) ? c : Color.white;
        }

        private Sprite CreateDefaultSprite(OrbColor color)
        {
            // Create a simple circle sprite procedurally
            int resolution = 64;
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
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 64f);
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
