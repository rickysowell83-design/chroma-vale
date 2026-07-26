using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.Progression;
using ChromaVale.Domain.PuzzleBoard;
using ChromaVale.Infrastructure.Audio;
using ChromaVale.Presentation.Views.Components;
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
        private Coroutine[,] _cellLerps;
        private LevelData _level;
        private LevelRepository _levelRepo = new();
        private int _maxLevel;

        // Tutorial step system
        private int _tutorialStep;
        private Coroutine _typewriterCoroutine;
        private bool _hasPlacedFirstPiece;
        // Tutorial guide arrow
        private LineRenderer _tutorialGuideArrow;


        // Component references
        private GridBuilder _gridBuilder;
        private HudPanel _hudPanel;
        private RouteButton _flowButtonComponent;
        private InventoryPanel _inventoryPanelComponent;
        private WinPopup _winPopupComponent;
        private EnvironmentBackdrop _envBackdrop;
        private MusicDirector _musicDirector;
        private CameraShake _cameraShake;
        private ParticleFxService _particleFx;

        // State
        private bool _solved;
        private int _moveCount;
        private int _starsEarned;
        private readonly Stack<(int x, int y, int pieceIdx)> _undoStack = new();

        // Pending rotation applied when placing from inventory (rotate-before-place)
        private int _pendingRotation = 0;

        // Audio
        private IAudioService _audioService;
        private float _lastFlowTickSoundTime;

        private void Start()
        {
            _maxLevel = _levelRepo.LevelCount;

            _audioService = AudioServiceInstaller.Instance;

                        // HARD-LOCKED to Level 1 for pipeline test — ignore save data
            _levelNumber = 1;
            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _flowSim = new SignalRouter();
            _inventory = new TraceInventory(_level.Inventory);

            EnsureEventSystem();
            CreateComponents();

            _renderers = _gridBuilder.Build(_level, _board, _tileSize, this);
            _cellLerps = new Coroutine[_board.Width, _board.Height];
            // STRIPPED for pipeline test — _envBackdrop.Build();
            // STRIPPED for pipeline test — _musicDirector.StartMusic();

            WireEvents();

            _hudPanel.SetMoves(0);
            _hudPanel.SetLevel(_levelNumber, _maxLevel);
            _hudPanel.SetPieceCount(_inventory.AvailableCount);
            _inventoryPanelComponent.Bind(_inventory);

            // FLOW button dimmed until at least one pipe is placed
            _flowButtonComponent.SetInteractable(false);

            // STRIPPED — _musicDirector.PlayBeep(440f, 0.15f);

            if (_audioService != null) _audioService.PlaySound("level_start");

            StartCoroutine(PulseSources());

            // Level 1 tutorial: pulse the source indicator emission
            if (_levelNumber == 1)
            {
                foreach (var src in _level.Sources)
                {
                    if (_renderers[src.X, src.Y] != null)
                        _renderers[src.X, src.Y].StartSourcePulse();
                }
            }

            // Show initial tutorial hint for Level 1
            ShowTutorialStep(0);
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
            // STRIPPED for pipeline test
            // _envBackdrop = CreateChildComponent<EnvironmentBackdrop>("EnvironmentBackdrop");
            // _musicDirector = CreateChildComponent<MusicDirector>("MusicDirector");
            // _cameraShake = Camera.main.gameObject.AddComponent<CameraShake>();
            // _particleFx = CreateChildComponent<ParticleFxService>("ParticleFxService");
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

        // ═══════════════════════════════════════════════════════════════
        // TUTORIAL STEP SYSTEM
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Show a contextual tutorial hint for the current step.
        /// Each step displays a typewriter-style message appropriate to the player's progress.
        /// </summary>
        private void ShowTutorialStep(int step)
        {
            if (_levelNumber != 1) return;

            _tutorialStep = step;

            string hintText = step switch
            {
                0 => "Drag traces to connect source to target",
                1 => "Good. Now press FLOW to test the circuit.",
                _ => ""
            };

            if (!string.IsNullOrEmpty(hintText))
            {
                StartTypewriterHint(hintText);
            }

            // Show guide arrow when tutorial first appears
            if (step == 0)
            {
                ShowTutorialGuideArrow();
            }
        }

        /// <summary>
        /// Show a dashed cyan guide arrow from source to target for tutorial.
        /// Uses a world-space LineRenderer with animated dash offset.
        /// </summary>
        private void ShowTutorialGuideArrow()
        {
            if (_tutorialGuideArrow != null) return;
            if (_level.Sources.Length == 0 || _level.Targets.Length == 0) return;

            var src = _level.Sources[0];
            var tgt = _level.Targets[0];
            if (_renderers[src.X, src.Y] == null || _renderers[tgt.X, tgt.Y] == null) return;

            var arrowGo = new GameObject("TutorialGuideArrow");
            arrowGo.transform.SetParent(transform);
            _tutorialGuideArrow = arrowGo.AddComponent<LineRenderer>();

            // Use URP-compatible material
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = ChromaPalette.NeonCyan;
            _tutorialGuideArrow.material = mat;
            _tutorialGuideArrow.startWidth = 0.06f;
            _tutorialGuideArrow.endWidth = 0.06f;
            _tutorialGuideArrow.textureMode = LineTextureMode.Tile;
            _tutorialGuideArrow.startColor = ChromaPalette.NeonCyan;
            _tutorialGuideArrow.endColor = ChromaPalette.NeonCyan;

            var srcPos = _renderers[src.X, src.Y].transform.position;
            var tgtPos = _renderers[tgt.X, tgt.Y].transform.position;
            // Offset Z slightly above the board
            srcPos.z -= 0.3f;
            tgtPos.z -= 0.3f;

            _tutorialGuideArrow.positionCount = 2;
            _tutorialGuideArrow.SetPosition(0, srcPos);
            _tutorialGuideArrow.SetPosition(1, tgtPos);

            // Start dash animation coroutine
            StartCoroutine(AnimateGuideArrowDash());
        }

        /// <summary>
        /// Animate the tutorial guide arrow with a moving dash pattern.
        /// </summary>
        private System.Collections.IEnumerator AnimateGuideArrowDash()
        {
            float dashSpeed = 1.5f;
            float offset = 0f;
            while (_tutorialGuideArrow != null && _tutorialGuideArrow.material != null)
            {
                offset += Time.deltaTime * dashSpeed;
                _tutorialGuideArrow.material.mainTextureOffset = new Vector2(offset, 0);
                // Pulse alpha for breathing effect
                float alpha = 0.4f + 0.3f * Mathf.Sin(Time.time * 2f);
                var c = _tutorialGuideArrow.startColor;
                c.a = alpha;
                _tutorialGuideArrow.startColor = c;
                _tutorialGuideArrow.endColor = c;
                yield return null;
            }
        }

        /// <summary>
        /// Hide and destroy the tutorial guide arrow.
        /// </summary>
        private void HideTutorialGuideArrow()
        {
            if (_tutorialGuideArrow != null)
            {
                Destroy(_tutorialGuideArrow.gameObject);
                _tutorialGuideArrow = null;
            }
        }


        /// <summary>
        /// Display hint text with a typewriter character-reveal effect over ~1 second.
        /// </summary>
        private void StartTypewriterHint(string text)
        {
            if (_typewriterCoroutine != null)
                StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterEffect(text));
        }

        private IEnumerator TypewriterEffect(string fullText)
        {
            float totalDuration = 1.0f;
            int totalChars = fullText.Length;
            float delayPerChar = totalDuration / totalChars;
            string currentText = "";

            for (int i = 0; i <= totalChars; i++)
            {
                currentText = fullText.Substring(0, i);
                _hudPanel.ShowHint(currentText + (i < totalChars ? "<color=#80808080>\u258C</color>" : ""));
                yield return new WaitForSeconds(delayPerChar);
            }

            // Full text without cursor blink
            _hudPanel.ShowHint(fullText);
            _typewriterCoroutine = null;
        }

        // ═══════════════════════════════════════════════════════════════



        private IEnumerator PulseSources()
        {
            while (!_solved)
            {
                foreach (var src in _level.Sources)
                {
                    if (_renderers[src.X, src.Y] != null)
                    {
                        var sr = _renderers[src.X, src.Y];
                        sr.transform.localScale = Vector3.one * 1.25f;
                    }
                }
                yield return new WaitForSeconds(0.6f);
                foreach (var src in _level.Sources)
                {
                    if (_renderers[src.X, src.Y] != null)
                    {
                        var sr = _renderers[src.X, src.Y];
                        sr.transform.localScale = Vector3.one;
                    }
                }
                yield return new WaitForSeconds(0.6f);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INPUT — Piece Placement
        // ═══════════════════════════════════════════════════════════════

        public void OnPointerDown(int x, int y)
        {
            if (_solved) return;
            if (_flowSim.IsRunning) return;

            var cell = _board.GetCell(x, y);

            int existingIdx = _inventory.GetPieceIndexAt(x, y);
            if (existingIdx >= 0 && cell.Type == CellType.Trace)
            {
                RotatePlacement(x, y, existingIdx);
                return;
            }

            if (cell.Type == CellType.Empty && _inventoryPanelComponent.SelectedPieceIndex >= 0)
            {
                PlaceSelectedPiece(x, y);
            }
        }

        public void OnRightClick()
        {
            if (_solved) return;
            if (_flowSim.IsRunning) return;
            UndoPlacement();
        }

        private void Update()
        {
            // Scroll to rotate the pending piece before placement
            if (_solved || _flowSim == null || _flowSim.IsRunning) return;

            if (_inventoryPanelComponent.SelectedPieceIndex >= 0)
            {
                float scrollDelta = Mouse.current.scroll.ReadValue().y;
                if (scrollDelta > 0f)
                {
                    _pendingRotation = (_pendingRotation + 90) % 360;
                    _inventoryPanelComponent.SetPendingRotation(_pendingRotation);
                }
                else if (scrollDelta < 0f)
                {
                    _pendingRotation = (_pendingRotation - 90 + 360) % 360;
                    _inventoryPanelComponent.SetPendingRotation(_pendingRotation);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PLACEMENT PREVIEW GHOST
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by TileClickHandler when the pointer enters a tile cell.
        /// If a piece is selected from the inventory and the cell is empty,
        /// shows a translucent preview ghost of the pipe at this tile.
        /// </summary>
        public void OnTileHover(int x, int y)
        {
            if (_solved) return;
            if (_flowSim == null || _flowSim.IsRunning) return;
            if (_inventoryPanelComponent == null) return;
            if (_renderers == null || x < 0 || y < 0 || x >= _renderers.GetLength(0) || y >= _renderers.GetLength(1)) return;
            if (_inventory == null) return;

            int selIdx = _inventoryPanelComponent.SelectedPieceIndex;
            if (selIdx < 0) return;

            var cell = _board.GetCell(x, y);
            if (cell.Type != CellType.Empty) return;

            var piece = _inventory.Pieces[selIdx];
            if (_renderers[x, y] != null)
            {
                _renderers[x, y].ShowPlacementPreview(piece.Shape, _pendingRotation);
            }
        }

        /// <summary>
        /// Called by TileClickHandler when the pointer exits a tile cell.
        /// Hides the placement preview ghost on this tile.
        /// </summary>
        public void OnTileHoverExit(int x, int y)
        {
            if (_renderers == null || x < 0 || y < 0 || x >= _renderers.GetLength(0) || y >= _renderers.GetLength(1)) return;
            if (_renderers[x, y] != null)
            {
                _renderers[x, y].HidePlacementPreview();
            }
        }

        private void RotatePlacement(int x, int y, int pieceIdx)
        {
            var piece = _inventory.GetPieceAt(x, y);
            if (piece == null) return;

            piece.Rotate();

            _renderers[x, y].SetShape(piece.Shape, piece.Rotation);
            // Show copper idle (no glow) between rotations
            _renderers[x, y].Color = ChromaPalette.CopperDark;

            if (_flowSim != null)
            {
                _flowSim.SetTraceShape(x, y, piece.Shape, piece.Direction, piece.Rotation);
            }

            StartCoroutine(PopAnim(_renderers[x, y].transform));
        }

        private void PlaceSelectedPiece(int x, int y)
        {
            int selIdx = _inventoryPanelComponent.SelectedPieceIndex;
            if (selIdx < 0) return;

            bool placed = _inventory.TryPlace(selIdx, _board, x, y, _flowSim, rotation: _pendingRotation);
            if (placed)
            {
                _undoStack.Push((x, y, selIdx));
                _moveCount++;
                _hudPanel.SetMoves(_moveCount);
                _renderers[x, y].Color = ChromaPalette.CopperDark; // Copper idle — neon flows later
                var piece = _inventory.GetPieceAt(x, y);
                if (piece != null) _renderers[x, y].SetShape(piece.Shape, piece.Rotation);
                StartCoroutine(PopAnim(_renderers[x, y].transform));
                _inventoryPanelComponent.Refresh();
                _inventoryPanelComponent.ClearSelection();
                _pendingRotation = 0;
                _inventoryPanelComponent.SetPendingRotation(0);

                // Enable FLOW button now that at least one pipe is on the board
                _flowButtonComponent.SetInteractable(true);
                _hudPanel.SetPieceCount(_inventory.AvailableCount);

                // Tutorial step advancement
                if (!_hasPlacedFirstPiece && _levelNumber == 1)
                {
                    _hasPlacedFirstPiece = true;
                    HideTutorialGuideArrow();

                    ShowTutorialStep(1);
                }
                else
                {
                    _hudPanel.HideHint();
                }

                if (_audioService != null) _audioService.PlaySound("pipe_place");
                // STRIPPED — _musicDirector.PlayBeep(660f, 0.08f);
                if (_particleFx != null)
                    _particleFx.PlacementPuff(_renderers[x, y].transform.position, GetPipeColor(0));
                if (_cameraShake != null) _cameraShake.Shake(0.06f, 0.05f);

                if (!_solved && !_flowSim.IsRunning && CheckAllConnected())
                {
                    StartCoroutine(RunFlowSimulation());
                }
            }
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
                    if (router.IsPathConnected(srcX, srcY, tgt.X, tgt.Y))
                    {
                        foundPath = true;
                        break;
                    }
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

            bool undone = _inventory.TryUndo(_board);
            if (undone)
            {
                _renderers[x, y].ClearShape();
                _renderers[x, y].Color = DarkTile;
                _moveCount = Mathf.Max(0, _moveCount - 1);
                _hudPanel.SetMoves(_moveCount);
                _hudPanel.SetPieceCount(_inventory.AvailableCount);
                _inventoryPanelComponent.Refresh();
                _undoStack.Pop();
                if (_audioService != null) _audioService.PlaySound("undo");

                // Reset tutorial step if player undid first piece
                if (_undoStack.Count == 0 && _levelNumber == 1)
                {
                    _hasPlacedFirstPiece = false;
                    _tutorialStep = 0;
                }

                // Re-dim FLOW button if no pipes left on board
                if (_undoStack.Count == 0)
                    _flowButtonComponent.SetInteractable(false);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FLOW SIMULATION
        // ═══════════════════════════════════════════════════════════════

        public void OnRouteButtonPressed()
        {
            if (_solved || _flowSim.IsRunning) return;
            if (_inventory.PlacedCount == 0) return;

            StartCoroutine(RunFlowSimulation());
        }

        private IEnumerator RunFlowSimulation()
        {
            _flowButtonComponent.SetInteractable(false);
            _inventoryPanelComponent.SetLocked(true);

            _flowSim.OnSignalAdvance += HandleFlowAdvance;
            _flowSim.OnTraceShort += HandlePipeBurst;
            _flowSim.OnColorMix += HandleColorMix;
            _flowSim.OnTargetReached += HandleTargetReached;

            _flowSim.StartSimulation(_board, _level, _inventory);

            // Pulse flow-head at each source to signal simulation start
            if (_particleFx != null)
            {
                foreach (var src in _level.Sources)
                    _particleFx.FlowHeadPulse(_renderers[src.X, src.Y].transform.position, GetPipeColor(src.ColorIndex));
            }

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
                // STRIPPED — _musicDirector.PlayBeep(880f, 0.3f);
                _starsEarned = ScoreCalculator.Calculate(_inventory, _flowSim, _level);
                // HARD-LOCKED: skip save for pipeline test
                // if (SaveGameManager.Instance != null)
                //     SaveGameManager.Instance.RecordLevelComplete(_levelNumber, _starsEarned);
                if (_particleFx != null)
                {
                    var positions = _level.Targets.Select(t => _renderers[t.X, t.Y].transform.position).ToArray();
                    _particleFx.WinCascade(positions);
                }
                yield return new WaitForSeconds(0.5f);
                _winPopupComponent.Show(_starsEarned, _moveCount, _levelNumber >= _maxLevel);
            }
            else
            {
                yield return StartCoroutine(FlashFailure());
                yield return new WaitForSeconds(1f);
                ResetPuzzle();
            }

            _flowButtonComponent.SetInteractable(true);
            _inventoryPanelComponent.SetLocked(false);
        }

        private void HandleFlowAdvance(int x, int y, int colorIndex)
        {
            if (_renderers[x, y] != null)
            {
                // Stop any existing lerp on this cell — re-entrant safety
                if (_cellLerps[x, y] != null)
                    StopCoroutine(_cellLerps[x, y]);
                _cellLerps[x, y] = StartCoroutine(FlowLerpAnim(_renderers[x, y], DarkTile, GetPipeColor(colorIndex)));
            }
            if (_audioService != null && Time.realtimeSinceStartup - _lastFlowTickSoundTime >= 0.1f)
            {
                _audioService.PlaySound("flow_tick");
                _lastFlowTickSoundTime = Time.realtimeSinceStartup;
            }
            // NEW: Flow-head particle pulse at the wave-front cell
            if (_particleFx != null)
                _particleFx.FlowHeadPulse(_renderers[x, y].transform.position, GetPipeColor(colorIndex));
        }

        private void HandlePipeBurst(int x, int y)
        {
            if (_renderers[x, y] != null)
            {
                _renderers[x, y].Color = new Color(0.5f, 0.1f, 0.05f);
                StartCoroutine(BurstAnim(_renderers[x, y].transform));
                if (_particleFx != null)
                    _particleFx.BurstExplosion(_renderers[x, y].transform.position);
            }
            _inventory.MarkShorted(x, y);
            if (_audioService != null) _audioService.PlaySound("pipe_burst");
            if (_cameraShake != null) _cameraShake.Shake(0.25f, 0.30f);
        }

        private void HandleColorMix(int x, int y, int colorA, int colorB)
        {
            if (_renderers[x, y] != null)
            {
                StartCoroutine(MixFlashAnim(_renderers[x, y]));
                if (_particleFx != null)
                    _particleFx.MixSwirl(_renderers[x, y].transform.position, GetPipeColor(colorA), GetPipeColor(colorB));
            }
            if (_audioService != null) _audioService.PlaySound("color_mix");
        }

        private void HandleTargetReached(int x, int y, int colorIndex)
        {
            if (_renderers[x, y] != null)
            {
                StartCoroutine(TargetBloomAnim(_renderers[x, y], colorIndex));
                if (_particleFx != null)
                    _particleFx.TargetBloom(_renderers[x, y].transform.position, GetPipeColor(colorIndex));
            }
            if (_audioService != null) _audioService.PlaySound("target_reached");
        }

        // ═══════════════════════════════════════════════════════════════
        // WIN / RESET / LEVEL PROGRESSION
        // ═══════════════════════════════════════════════════════════════

        public void ResetPuzzle()
        {
            LoadLevel(_levelNumber);
        }

        private void AdvanceLevel()
        {
            // HARD-LOCKED for pipeline test — always reload Level 1
            LoadLevel(1);
        }

        private void LoadLevel(int levelNum)
        {
            StopAllCoroutines();
            _gridBuilder.Clear();

            // Reset tutorial state
            _tutorialStep = 0;
            _hasPlacedFirstPiece = false;
            _typewriterCoroutine = null;
            HideTutorialGuideArrow();

            _solved = false;
            _moveCount = 0;
            _starsEarned = 0;
            _pendingRotation = 0;
            _undoStack.Clear();
            _winPopupComponent.Hide();

            _levelNumber = levelNum;
            _level = _levelRepo.GetLevel(_levelNumber);

            _board = new GridBoard(_level);
            _flowSim = new SignalRouter();
            _inventory = new TraceInventory(_level.Inventory);

            _renderers = _gridBuilder.Build(_level, _board, _tileSize, this);
            _cellLerps = new Coroutine[_board.Width, _board.Height];

            _hudPanel.SetMoves(0);
            _hudPanel.SetLevel(_levelNumber, _maxLevel);
            _hudPanel.SetPieceCount(_inventory.AvailableCount);
            _inventoryPanelComponent.Bind(_inventory);
            _flowButtonComponent.SetInteractable(false);  // Dimmed until first pipe placed
            _inventoryPanelComponent.SetLocked(false);

            // Show contextual tutorial for level 1 using the level's DisplayName
            if (_levelNumber == 1)
            {
                ShowTutorialStep(0);
            }
            else
            {
                _hudPanel.HideHint();
            }

            // Level 1 tutorial: pulse the source indicator emission
            if (_levelNumber == 1)
            {
                foreach (var src in _level.Sources)
                {
                    if (_renderers[src.X, src.Y] != null)
                        _renderers[src.X, src.Y].StartSourcePulse();
                }
            }

            StartCoroutine(PulseSources());
        }

        // ═══════════════════════════════════════════════════════════════
        // COLOR HELPERS
        // ═══════════════════════════════════════════════════════════════

        private Color GetPipeColor(int ci) => ci switch
        {
            0 => NeonCyan,
            1 => NeonMagenta,
            2 => NeonYellow,
            3 => NeonOrange,
            4 => NeonPurple,
            5 => NeonRed,
            6 => NeonPurple,
            7 => NeonGreen,
            8 => NeonOrange,
            9 => new Color(0.4f, 0.25f, 0.1f), // Brown
            _ => NeonCyan
        };

        // ═══════════════════════════════════════════════════════════════
        // ANIMATIONS
        // ═══════════════════════════════════════════════════════════════

        private IEnumerator PopAnim(Transform t)
        {
            float d = 0.12f, e = 0f;
            var o = t.localScale;
            while (e < d) { e += Time.deltaTime; t.localScale = o * (1f + Mathf.Sin(e / d * Mathf.PI) * 0.3f); yield return null; }
            t.localScale = o;
        }

        private IEnumerator FlowPulseAnim(Transform t)
        {
            float d = 0.15f, e = 0f;
            var o = t.localScale;
            while (e < d) { e += Time.deltaTime; t.localScale = o * (1f + Mathf.Sin(e / d * Mathf.PI) * 0.2f); yield return null; }
            t.localScale = o;
        }

        private IEnumerator FlowLerpAnim(TileVisual tv, Color fromColor, Color toColor)
        {
            float duration = 0.15f;
            float elapsed = 0f;
            var originalScale = tv.transform.localScale;
            tv.SetFlowIdle();
            tv.SetFlowActive(toColor);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Scale pulse: 1.15x at start → 1.0x at end
                float scaleT = 1f + 0.15f * (1f - t);
                tv.transform.localScale = originalScale * scaleT;
                yield return null;
            }

            tv.transform.localScale = originalScale;
        }

        private IEnumerator BurstAnim(Transform t)
        {
            float d = 0.4f, e = 0f;
            var orig = t.localPosition;
            while (e < d) { e += Time.deltaTime; t.localPosition = orig + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), 0); yield return null; }
            t.localPosition = orig;
        }

        private IEnumerator MixFlashAnim(TileVisual tv)
        {
            var orig = tv.Color;
            tv.Color = Color.white;
            yield return new WaitForSeconds(0.1f);
            tv.Color = orig;
        }

        private IEnumerator TargetBloomAnim(TileVisual tv, int colorIndex)
        {
            var targetColor = GetPipeColor(colorIndex);
            float d = 0.5f, e = 0f;
            var o = tv.transform.localScale;
            var origColor = tv.Color;
            while (e < d)
            {
                e += Time.deltaTime;
                float t = e / d;
                tv.Color = Color.Lerp(origColor, targetColor * 1.5f, t);
                tv.transform.localScale = o * (1f + t * 0.5f);
                yield return null;
            }
            tv.Color = targetColor;
            tv.transform.localScale = o;
        }

        private IEnumerator FlashFailure()
        {
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

    }
}