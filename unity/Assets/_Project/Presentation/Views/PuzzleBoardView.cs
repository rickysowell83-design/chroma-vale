using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.Progression;
using ChromaVale.Domain.PuzzleBoard;
using ChromaVale.Infrastructure.Audio;
using ChromaVale.Presentation.Views.Components;
using DG.Tweening;
using static ChromaVale.Presentation.Views.Components.ChromaPalette;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views
{
    public class PuzzleBoardView : MonoBehaviour
    {
        [SerializeField] private float _tileSize = 1.2f;
        [SerializeField] private int _levelNumber = 1;
        [SerializeField] private float _flowTickInterval = 0.3f;

        private GridBoard _board;
        private SignalRouter _flowSim;
        private TraceInventory _inventory;
        private TileVisual[,] _renderers;
        private LevelData _level;
        private LevelRepository _levelRepo = new();
        private int _maxLevel;

        // Visual tutorial system (v2 §5) - hand-pointer
        private bool _hasPlacedFirstPiece;
        private bool _tutorialActive;
        private bool _handTransitioned;
        private Coroutine _tutorialCoroutine;
        private TutorialHandPointer _tutorialHandPointer;

        // Component references
        private GridBuilder _gridBuilder;
        private HudPanel _hudPanel;
        private RouteButton _flowButtonComponent;
        private InventoryPanel _inventoryPanelComponent;
        private WinPopup _winPopupComponent;
        private EnvironmentBackdrop _envBackdrop;
        private PiecePlacer _piecePlacer;
private ParticleFxService _particleFx;

        // State
        private bool _solved;
        private int _moveCount;
        private int _starsEarned;
        private readonly Stack<(int x, int y, int pieceIdx)> _undoStack = new();
        private int _pendingRotation = 0;

        // Audio
        private IAudioService _audioService;
        private float _lastFlowTickSoundTime;

        private void Start()
        {
            _maxLevel = _levelRepo.LevelCount;
            _audioService = AudioServiceInstaller.Instance;

            _levelNumber = 1;
            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _flowSim = new SignalRouter();
            _inventory = new TraceInventory(_level.Inventory);

            EnsureEventSystem();
            CreateComponents();

            _renderers = _gridBuilder.Build(_level, _board, _tileSize, this);

            _envBackdrop.Build();

            WireEvents();

            _hudPanel.SetMoves(0);
            _hudPanel.SetLevel(_levelNumber, _maxLevel);
            _hudPanel.SetPieceCount(_inventory.AvailableCount);
            _inventoryPanelComponent.Bind(_inventory);
            _flowButtonComponent.SetInteractable(false);

            if (_audioService != null) _audioService.PlaySound("level_start");
            StartCoroutine(PulseSources());

            if (_levelNumber == 1)
            {
                foreach (var src in _level.Sources)
                    if (_renderers[src.X, src.Y] != null)
                        _renderers[src.X, src.Y].StartSourcePulse();

                _tutorialActive = true;
                _tutorialCoroutine = StartCoroutine(RunHandPointerTutorial());
            }
        }

        private T CreateChildComponent<T>(string name) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            return go.AddComponent<T>();
        }

private void CreateComponents()
        {
            _gridBuilder = CreateChildComponent<GridBuilder>("GridBuilder");
            _hudPanel = CreateChildComponent<HudPanel>("HudPanel");
            _flowButtonComponent = CreateChildComponent<RouteButton>("RouteButton");
            _inventoryPanelComponent = CreateChildComponent<InventoryPanel>("InventoryPanel");
            _winPopupComponent = CreateChildComponent<WinPopup>("WinPopup");
            _envBackdrop = CreateChildComponent<EnvironmentBackdrop>("EnvironmentBackdrop");
            _tutorialHandPointer = CreateChildComponent<TutorialHandPointer>("TutorialHandPointer");
            _particleFx = CreateChildComponent<ParticleFxService>("ParticleFxService");
            // v3: PiecePlacer — manages Blender-authored prefab instantiation
            _piecePlacer = CreateChildComponent<PiecePlacer>("PiecePlacer");
        }

        private void WireEvents()
        {
            _winPopupComponent.OnNextLevel += AdvanceLevel;
            _winPopupComponent.OnReplay += ResetPuzzle;
            _flowButtonComponent.OnFlowRequested += OnRouteButtonPressed;
            _hudPanel.OnResetRequested += ResetPuzzle;
            _inventoryPanelComponent.OnPieceSelected += _ => { _pendingRotation = 0; _inventoryPanelComponent.SetPendingRotation(0); };
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        // ================================================================
        // HAND-POINTER TUTORIAL (v2 §5)
        // ================================================================

        private IEnumerator RunHandPointerTutorial()
        {
            if (_level.Sources.Length == 0 || _level.Targets.Length == 0) yield break;

            yield return new WaitForSeconds(0.6f);

            // Step 1: Point at first inventory piece
            PointHandAtInventorySlot(SegmentShape.Straight);

            // Wait for player to pick a piece
            while (!_hasPlacedFirstPiece && _tutorialActive)
            {
                if (_inventoryPanelComponent.SelectedPieceIndex >= 0 && !_handTransitioned)
                {
                    _handTransitioned = true;
                    yield return new WaitForSeconds(0.15f);
                    PointHandAtBoardCell(2, 1);
                }
                yield return new WaitForSeconds(0.2f);
            }

            // Step 4: Fade out after first placement
            if (_tutorialHandPointer != null)
                _tutorialHandPointer.FadeOut();
            _tutorialActive = false;
        }

        private void PointHandAtInventorySlot(SegmentShape shape)
        {
            if (_tutorialHandPointer == null) return;
            Vector2 screenPos = new Vector2(Screen.width * 0.22f, Screen.height * 0.10f);
            _tutorialHandPointer.PointAt(screenPos, "PICK THIS", false);
            _inventoryPanelComponent.SelectPieceForTutorial(shape);
        }

        private void PointHandAtBoardCell(int x, int y)
        {
            if (_tutorialHandPointer == null || _renderers[x, y] == null) return;
            Vector3 worldPos = _renderers[x, y].transform.position;
            Vector3 screenPos3 = Camera.main.WorldToScreenPoint(worldPos);
            Vector2 screenPos = new Vector2(screenPos3.x - Screen.width * 0.5f, screenPos3.y - Screen.height * 0.5f);
            _tutorialHandPointer.TransitionTo(screenPos, "PLACE HERE", true);
        }

        // ================================================================
        // INPUT
        // ================================================================

        public void OnPointerDown(int x, int y)
        {
            if (_solved || _flowSim.IsRunning) return;
            var cell = _board.GetCell(x, y);

            int existingIdx = _inventory.GetPieceIndexAt(x, y);
            if (existingIdx >= 0 && cell.Type == CellType.Trace)
            {
                RotatePlacement(x, y, existingIdx);
                return;
            }

            if (cell.Type == CellType.Empty && _inventoryPanelComponent.SelectedPieceIndex >= 0)
                PlaceSelectedPiece(x, y);
        }

        public void OnRightClick()
        {
            if (_solved || _flowSim.IsRunning) return;
            UndoPlacement();
        }

        private void Update()
        {
            if (_solved || _flowSim == null || _flowSim.IsRunning) return;
            if (_inventoryPanelComponent.SelectedPieceIndex >= 0)
            {
                float scrollDelta = Mouse.current.scroll.ReadValue().y;
                if (scrollDelta > 0f) { _pendingRotation = (_pendingRotation + 90) % 360; _inventoryPanelComponent.SetPendingRotation(_pendingRotation); }
                else if (scrollDelta < 0f) { _pendingRotation = (_pendingRotation - 90 + 360) % 360; _inventoryPanelComponent.SetPendingRotation(_pendingRotation); }
            }
        }

        public void OnTileHover(int x, int y)
        {
            if (_solved || _flowSim == null || _flowSim.IsRunning) return;
            if (_inventoryPanelComponent == null || _inventory == null) return;
            if (_renderers == null || x < 0 || y < 0 || x >= _renderers.GetLength(0) || y >= _renderers.GetLength(1)) return;

            int selIdx = _inventoryPanelComponent.SelectedPieceIndex;
            if (selIdx < 0) return;
            var cell = _board.GetCell(x, y);
            if (cell.Type != CellType.Empty) return;

            var piece = _inventory.Pieces[selIdx];
            if (_renderers[x, y] != null)
                _renderers[x, y].ShowPlacementPreview(piece.Shape, _pendingRotation);
        }

        public void OnTileHoverExit(int x, int y)
        {
            if (_renderers == null || x < 0 || y < 0 || x >= _renderers.GetLength(0) || y >= _renderers.GetLength(1)) return;
            if (_renderers[x, y] != null) _renderers[x, y].HidePlacementPreview();
        }

private void RotatePlacement(int x, int y, int pieceIdx)
        {
            var piece = _inventory.GetPieceAt(x, y);
            if (piece == null) return;
            piece.Rotate();
            _renderers[x, y].SetShape(piece.Shape, piece.Rotation);
            _renderers[x, y].Color = ChromaPalette.PlayerTraceCopper; // Bright copper #5C3A1E
            if (_flowSim != null) _flowSim.SetTraceShape(x, y, piece.Shape, piece.Direction, piece.Rotation);

            // v3: Update prefab piece rotation (remove old, place new)
            if (_piecePlacer != null)
            {
                _piecePlacer.RemovePieceAtCell(x, y);
                _cellPieceInfo[(x, y)] = (piece.Shape, piece.Rotation);
                _piecePlacer.PlacePieceAtCell(
                    piece.Shape, piece.Rotation, x, y,
                    _renderers[x, y].transform.position, _gridBuilder.transform);
            }

            _renderers[x, y].transform.DOPunchScale(Vector3.one * 0.3f, 0.12f, 1, 0f);
        }

private void PlaceSelectedPiece(int x, int y)
        {
            int selIdx = _inventoryPanelComponent.SelectedPieceIndex;
            if (selIdx < 0) return;

            bool placed = _inventory.TryPlace(selIdx, _board, x, y, _flowSim, rotation: _pendingRotation);
            if (!placed) return;

            _undoStack.Push((x, y, selIdx));
            _moveCount++;
            _hudPanel.SetMoves(_moveCount);
            _renderers[x, y].Color = ChromaPalette.PlayerTraceCopper; // Bright copper #5C3A1E — player placed
            var piece = _inventory.GetPieceAt(x, y);
            if (piece != null)
            {
                _renderers[x, y].SetShape(piece.Shape, piece.Rotation);
                // v3: Instantiate Blender-authored prefab piece
                if (_piecePlacer != null)
                {
                    _cellPieceInfo[(x, y)] = (piece.Shape, piece.Rotation);
                    _piecePlacer.PlacePieceAtCell(
                        piece.Shape, piece.Rotation, x, y,
                        _renderers[x, y].transform.position, _gridBuilder.transform);
                }
            }
            _renderers[x, y].transform.DOPunchScale(Vector3.one * 0.3f, 0.12f, 1, 0f);
            _inventoryPanelComponent.Refresh();
            _inventoryPanelComponent.ClearSelection();
            _pendingRotation = 0;
            _inventoryPanelComponent.SetPendingRotation(0);

            _flowButtonComponent.SetInteractable(true);
            _hudPanel.SetPieceCount(_inventory.AvailableCount);

            if (!_hasPlacedFirstPiece && _levelNumber == 1)
            {
                _hasPlacedFirstPiece = true;
                _handTransitioned = false; // reset for next piece cycle
            }

            if (_audioService != null) _audioService.PlaySound("pipe_place");
            if (_particleFx != null) _particleFx.PlacementPuff(_renderers[x, y].transform.position, GetPipeColor(0));

            if (!_solved && !_flowSim.IsRunning && CheckAllConnected())
                StartCoroutine(RunFlowSimulation());
        }

        private bool CheckAllConnected()
        {
            if (_inventory.PlacedCount == 0) return false;
            var router = new TraceRouter(_board);
            foreach (var src in _level.Sources)
            {
                int srcX = src.X, srcY = src.Y;
                bool foundPath = false;
                foreach (var tgt in _level.Targets)
                {
                    if (tgt.ColorIndex != src.ColorIndex) continue;
                    if (router.IsPathConnected(srcX, srcY, tgt.X, tgt.Y)) { foundPath = true; break; }
                }
                if (!foundPath) return false;
            }
            return true;
        }

private void UndoPlacement()
        {
            if (_undoStack.Count == 0) return;
            var top = _undoStack.Peek();
            int x = top.x, y = top.y;

            if (!_inventory.TryUndo(_board)) return;

            _renderers[x, y].ClearShape();
            _renderers[x, y].Color = DarkTile;

            // v3: Remove prefab piece
            _piecePlacer?.RemovePieceAtCell(x, y);
            _cellPieceInfo.Remove((x, y));

            _moveCount = Mathf.Max(0, _moveCount - 1);
            _hudPanel.SetMoves(_moveCount);
            _hudPanel.SetPieceCount(_inventory.AvailableCount);
            _inventoryPanelComponent.Refresh();
            _undoStack.Pop();
            if (_audioService != null) _audioService.PlaySound("undo");

            if (_undoStack.Count == 0 && _levelNumber == 1)
            {
                _hasPlacedFirstPiece = false;
                _handTransitioned = false;
                if (!_tutorialActive)
                {
                    _tutorialActive = true;
                    _tutorialCoroutine = StartCoroutine(RunHandPointerTutorial());
                }
            }

            if (_undoStack.Count == 0)
                _flowButtonComponent.SetInteractable(false);
        }

        // ================================================================
        // FLOW SIMULATION
        // ================================================================

        public void OnRouteButtonPressed()
        {
            if (_solved || _flowSim.IsRunning || _inventory.PlacedCount == 0) return;
            StartCoroutine(RunFlowSimulation());
        }

        private IEnumerator RunFlowSimulation()
        {
            _flowButtonComponent.SetRouting(true);
            _inventoryPanelComponent.SetLocked(true);

            _flowSim.OnSignalAdvance += HandleFlowAdvance;
            _flowSim.OnTraceShort += HandlePipeBurst;
            _flowSim.OnColorMix += HandleColorMix;
            _flowSim.OnTargetReached += HandleTargetReached;

            _flowSim.StartSimulation(_board, _level, _inventory);

            if (_particleFx != null)
                foreach (var src in _level.Sources)
                    _particleFx.FlowHeadPulse(_renderers[src.X, src.Y].transform.position, GetPipeColor(src.ColorIndex));

            while (_flowSim.GetResult() == SimulationResult.InProgress)
            {
                yield return new WaitForSeconds(_flowTickInterval);
                _flowSim.Tick();
            }

            _flowSim.OnSignalAdvance -= HandleFlowAdvance;
            _flowSim.OnTraceShort -= HandlePipeBurst;
            _flowSim.OnColorMix -= HandleColorMix;
            _flowSim.OnTargetReached -= HandleTargetReached;

            var result = _flowSim.GetResult();
            if (result == SimulationResult.AllTargetsReached)
            {
                _solved = true;
                _starsEarned = ScoreCalculator.Calculate(_inventory, _flowSim, _level);
                if (_particleFx != null)
                {
                    var positions = _level.Targets.Select(t => _renderers[t.X, t.Y].transform.position).ToArray();
                    _particleFx.WinCascade(positions);
                }
                yield return new WaitForSeconds(0.5f);
                _winPopupComponent.Show(_starsEarned, _moveCount, _levelNumber >= _maxLevel,
                    tracesUsed: _inventory.PlacedCount, maxTraces: _inventory.Pieces.Count);
            }
            else
            {
                yield return StartCoroutine(FlashFailure());
                yield return new WaitForSeconds(1f);
                ResetPuzzle();
            }

            _flowButtonComponent.SetRouting(false);
            _inventoryPanelComponent.SetLocked(false);
        }

private void HandleFlowAdvance(int x, int y, int colorIndex)
        {
            if (_renderers[x, y] != null)
            {
                var tv = _renderers[x, y];
                var originalScale = tv.transform.localScale;
                tv.SetFlowIdle();
                tv.SetFlowActive(GetPipeColor(colorIndex));
                tv.transform.DOScale(originalScale * 1.15f, 0.075f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => tv.transform.DOScale(originalScale, 0.075f).SetEase(Ease.OutQuad));

                // v3: Swap dormant prefab for active (emissive) variant
                if (_piecePlacer != null && _cellPieceInfo.TryGetValue((x, y), out var info))
                {
                    _piecePlacer.ActivatePieceAtCell(x, y, info.shape, info.rotation, _gridBuilder.transform);
                }
            }
            if (_audioService != null && Time.realtimeSinceStartup - _lastFlowTickSoundTime >= 0.1f)
            {
                _audioService.PlaySound("flow_tick");
                _lastFlowTickSoundTime = Time.realtimeSinceStartup;
            }
            if (_particleFx != null)
                _particleFx.FlowHeadPulse(_renderers[x, y].transform.position, GetPipeColor(colorIndex));
        }

        private void HandlePipeBurst(int x, int y)
        {
            if (_renderers[x, y] != null)
            {
                _renderers[x, y].Color = new Color(0.5f, 0.1f, 0.05f);
                            _renderers[x, y].transform.DOShakePosition(0.4f, 0.15f, 10, 90f, false, true);
                if (_particleFx != null) _particleFx.BurstExplosion(_renderers[x, y].transform.position);
            }
            _inventory.MarkShorted(x, y);
            if (_audioService != null) _audioService.PlaySound("pipe_burst");
        }

        private void HandleColorMix(int x, int y, int colorA, int colorB)
        {
            if (_renderers[x, y] != null)
            {
                var tv = _renderers[x, y];
                var orig = tv.Color;
                tv.Color = Color.white;
                DOTween.To(() => tv.Color, c => tv.Color = c, orig, 0.1f);
                if (_particleFx != null) _particleFx.MixSwirl(_renderers[x, y].transform.position, GetPipeColor(colorA), GetPipeColor(colorB));
            }
            if (_audioService != null) _audioService.PlaySound("color_mix");
        }

        private void HandleTargetReached(int x, int y, int colorIndex)
        {
            if (_renderers[x, y] != null)
            {
                var tv = _renderers[x, y];
                var targetColor = GetPipeColor(colorIndex);

                // ── Permanent emission ramp: dark socket → blazing neon ──
                // This swaps the TargetRing indicator materials and runs a
                // white-hot surge coroutine (mirrors SetFlowActive on traces).
                tv.SetTargetActive(targetColor);

                // ── Scale pop for impact ──
                var origScale = tv.transform.localScale;
                tv.transform.DOScale(origScale * 1.5f, 0.35f).SetEase(Ease.OutBack)
                    .OnComplete(() => tv.transform.DOScale(origScale, 0.25f).SetEase(Ease.OutQuad));

                // ── Particle burst: TargetBloom + RestorationPulse ──
                if (_particleFx != null)
                {
                    _particleFx.TargetBloom(_renderers[x, y].transform.position, targetColor);
                    _particleFx.RestorationPulse(_renderers[x, y].transform.position);
                }

                // ── Screen-space shockwave: post-process bloom pulse ──
                if (_particleFx != null)
                {
                    StartCoroutine(ScreenShockwavePulse());
                }
            }
            if (_audioService != null) _audioService.PlaySound("target_reached");
        }

        // ================================================================
        // WIN / RESET / LEVEL PROGRESSION
        // ================================================================

        public void ResetPuzzle() { LoadLevel(_levelNumber); }
        private void AdvanceLevel() { LoadLevel(1); }

        private void LoadLevel(int levelNum)
        {
            StopAllCoroutines();
            _gridBuilder.Clear();

            _tutorialActive = false;
            _hasPlacedFirstPiece = false;
            _handTransitioned = false;
            if (_tutorialHandPointer != null) _tutorialHandPointer.Hide();

            _solved = false;
            _moveCount = 0;
            _starsEarned = 0;
            _pendingRotation = 0;
            _undoStack.Clear();
            _cellPieceInfo.Clear();
            _piecePlacer?.ClearAllPieces();
            _winPopupComponent.Hide();

            _levelNumber = levelNum;
            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _flowSim = new SignalRouter();
            _inventory = new TraceInventory(_level.Inventory);

            _renderers = _gridBuilder.Build(_level, _board, _tileSize, this);

            _hudPanel.SetMoves(0);
            _hudPanel.SetLevel(_levelNumber, _maxLevel);
            _hudPanel.SetPieceCount(_inventory.AvailableCount);
            _inventoryPanelComponent.Bind(_inventory);
            _flowButtonComponent.SetInteractable(false);
            _inventoryPanelComponent.SetLocked(false);

            if (_levelNumber == 1)
            {
                foreach (var src in _level.Sources)
                    if (_renderers[src.X, src.Y] != null)
                        _renderers[src.X, src.Y].StartSourcePulse();

                _tutorialActive = true;
                _tutorialCoroutine = StartCoroutine(RunHandPointerTutorial());
            }

            StartCoroutine(PulseSources());
        }

        // ================================================================
        // COLOR HELPERS
        // ================================================================

        private Color GetPipeColor(int ci) => ci switch
        {
            0 => NeonCyan, 1 => NeonMagenta, 2 => NeonYellow, 3 => NeonOrange,
            4 => NeonPurple, 5 => NeonRed, 6 => NeonPurple, 7 => NeonGreen,
            8 => NeonOrange, 9 => new Color(0.4f, 0.25f, 0.1f), _ => NeonCyan
        };

// ── PiecePlacer-aware helpers ───────────────────────────────────
        /// <summary>
        /// Track cell→(shape, rotation) for prefab activation during flow.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<(int x, int y), (SegmentShape shape, int rotation)> _cellPieceInfo = new();


        // ================================================================
        // ANIMATIONS
        // ================================================================

        private IEnumerator PulseSources()
        {
            while (!_solved)
            {
                foreach (var src in _level.Sources)
                    if (_renderers[src.X, src.Y] != null)
                        _renderers[src.X, src.Y].transform.localScale = Vector3.one * 1.25f;
                yield return new WaitForSeconds(0.6f);
                foreach (var src in _level.Sources)
                    if (_renderers[src.X, src.Y] != null)
                        _renderers[src.X, src.Y].transform.localScale = Vector3.one;
                yield return new WaitForSeconds(0.6f);
            }
        }

private IEnumerator FlashFailure()
        {
            // v3: Clear all prefab pieces on failure (they'll be rebuilt on next placement)
            _piecePlacer?.ClearAllPieces();
            _cellPieceInfo.Clear();

            float d = 0.6f;
            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                    if (_renderers[x, y] != null)
                        _renderers[x, y].Color = new Color(0.3f, 0.05f, 0.05f);
            yield return new WaitForSeconds(d);
            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                {
                    if (_renderers[x, y] == null) continue;
                    var cell = _board.GetCell(x, y);
                    _renderers[x, y].Color = cell.Type switch
                    {
                        CellType.Source when cell.ColorIndex == 0 => CyanHint,
                        CellType.Source when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Target when cell.ColorIndex == 0 => CyanHint,
                        CellType.Target when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Obstacle => ObstacleCol,
                        CellType.SignalGate => DarkTile,
                        _ => DarkTile
                    };
                }
        }

        /// <summary>
        /// Screen-space shockwave: briefly spike the global Bloom intensity
        /// to create a radial pulse that sweeps across the screen.  Used for
        /// big moments — first pad per level, level complete.
        /// </summary>
        private IEnumerator ScreenShockwavePulse()
        {
            // Find the global post-process volume and spike Bloom
            var volume = FindAnyObjectByType<UnityEngine.Rendering.Volume>();
            if (volume == null) yield break;

            UnityEngine.Rendering.Universal.Bloom bloom;
            if (!volume.profile.TryGet(out bloom)) yield break;

            float originalIntensity = bloom.intensity.value;
            float originalThreshold = bloom.threshold.value;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Spike intensity: 1.4 → 4.0 → 1.4
                // Lower threshold: 0.85 → 0.4 → 0.85 (lets more light bloom)
                float intensityCurve = 1f - Mathf.Pow(1f - t, 3f); // Ease-out cubic
                bloom.intensity.value = Mathf.Lerp(originalIntensity, 4.0f, intensityCurve * (1f - Mathf.Abs(t - 0.3f) * 2f));
                bloom.threshold.value = Mathf.Lerp(originalThreshold, 0.4f, intensityCurve * (1f - Mathf.Abs(t - 0.3f) * 2f));

                yield return null;
            }

            // Restore
            bloom.intensity.value = originalIntensity;
            bloom.threshold.value = originalThreshold;
        }
    }
}
