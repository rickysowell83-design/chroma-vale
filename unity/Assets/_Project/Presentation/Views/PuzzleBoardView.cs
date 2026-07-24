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
        [SerializeField] private GameObject _pipeTilePrefab;
        [SerializeField] private GameObject _sourceTilePrefab;
        [SerializeField] private GameObject _targetTilePrefab;
        [SerializeField] private GameObject _obstacleTilePrefab;
        [SerializeField] private float _tileSize = 1.2f;
        [SerializeField] private int _levelNumber = 1;
        [SerializeField] private float _flowTickInterval = 0.3f;

        private GridBoard _board;
        private FlowSimulator _flowSim;
        private PipeInventory _inventory;
        private SpriteRenderer[,] _renderers;
        private Coroutine[,] _cellLerps;
        private LevelData _level;
        private LevelRepository _levelRepo = new();
        private int _maxLevel;

        // Component references
        private GridBuilder _gridBuilder;
        private HudPanel _hudPanel;
        private FlowButton _flowButtonComponent;
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

        // Audio
        private IAudioService _audioService;
        private float _lastFlowTickSoundTime;

        private void Start()
        {
            _maxLevel = _levelRepo.LevelCount;

            _audioService = AudioServiceInstaller.Instance;

                        _levelNumber = SaveGameManager.Instance != null ? SaveGameManager.Instance.CurrentLevel : 1;
            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _flowSim = new FlowSimulator();
            _inventory = new PipeInventory(_level.Inventory);

            EnsureEventSystem();
            CreateComponents();

            _renderers = _gridBuilder.Build(_level, _board, _tileSize,
                _pipeTilePrefab, _sourceTilePrefab, _targetTilePrefab, _obstacleTilePrefab, this);
            _cellLerps = new Coroutine[_board.Width, _board.Height];
            _envBackdrop.Build();
            _musicDirector.StartMusic();

            WireEvents();

            _hudPanel.SetMoves(0);
            _hudPanel.SetLevel(_levelNumber, _maxLevel);
            _inventoryPanelComponent.Bind(_inventory);

            DrawConnectionHint();
            _musicDirector.PlayBeep(440f, 0.15f);

            if (_audioService != null) _audioService.PlaySound("level_start");

            StartCoroutine(PulseSources());

            if (_levelNumber == 1)
            {
                int srcColor = _level.Sources.Length > 0 ? _level.Sources[0].ColorIndex : 0;
                string colorName = srcColor == 0 ? "CYAN" : srcColor == 1 ? "MAGENTA" : "YELLOW";
                _hudPanel.ShowHint("Connect the glowing " + colorName + " source to the " + colorName + " target!\n" +
                                   "TAP a pipe below \u2192 TAP a dark cell \u2192 watch the flow!");
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
            _flowButtonComponent = CreateChildComponent<FlowButton>("FlowButton");
            _inventoryPanelComponent = CreateChildComponent<InventoryPanel>("InventoryPanel");
            _winPopupComponent = CreateChildComponent<WinPopup>("WinPopup");
            _envBackdrop = CreateChildComponent<EnvironmentBackdrop>("EnvironmentBackdrop");
            _musicDirector = CreateChildComponent<MusicDirector>("MusicDirector");
            _cameraShake = Camera.main.gameObject.AddComponent<CameraShake>();
            _particleFx = CreateChildComponent<ParticleFxService>("ParticleFxService");
        }

        private void WireEvents()
        {
            _winPopupComponent.OnNextLevel += AdvanceLevel;
            _winPopupComponent.OnReplay += ResetPuzzle;
            _flowButtonComponent.OnFlowRequested += OnFlowButtonPressed;
            _hudPanel.OnResetRequested += ResetPuzzle;
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

        private void DrawConnectionHint()
        {
            foreach (var src in _level.Sources)
            {
                foreach (var tgt in _level.Targets)
                {
                    if (tgt.ColorIndex != src.ColorIndex) continue;
                    var line = new GameObject("Hint_" + src.ColorIndex);
                    line.transform.SetParent(transform);
                    var lr = line.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    var off = new Vector3(-_board.Width * _tileSize / 2f, -_board.Height * _tileSize / 2f, 0);
                    lr.SetPosition(0, new Vector3(src.X * _tileSize + off.x, src.Y * _tileSize + off.y, -0.5f));
                    lr.SetPosition(1, new Vector3(tgt.X * _tileSize + off.x, tgt.Y * _tileSize + off.y, -0.5f));
                    lr.startWidth = 0.08f; lr.endWidth = 0.08f;
                    var col = GetPipeColor(src.ColorIndex); col.a = 0.35f;
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = col; lr.endColor = col;
                    lr.sortingOrder = -5;
                    StartCoroutine(DestroyOnSolve(line));
                }
            }
        }

        private IEnumerator DestroyOnSolve(GameObject go)
        {
            while (!_solved) yield return null;
            if (go != null) Destroy(go);
        }

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
            if (existingIdx >= 0 && cell.Type == CellType.Pipe)
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

        private void RotatePlacement(int x, int y, int pieceIdx)
        {
            var piece = _inventory.GetPieceAt(x, y);
            if (piece == null) return;

            piece.Rotate();

            DrawPipeShape(_renderers[x, y].gameObject, piece.Shape, piece.Rotation);

            if (!_flowSim.IsRunning)
            {
                _renderers[x, y].color = GetPipeColor(0);
            }

            if (_flowSim != null)
            {
                _flowSim.SetPipeShape(x, y, piece.Shape, piece.Direction, piece.Rotation);
            }

            StartCoroutine(PopAnim(_renderers[x, y].transform));
        }

        private void PlaceSelectedPiece(int x, int y)
        {
            int selIdx = _inventoryPanelComponent.SelectedPieceIndex;
            if (selIdx < 0) return;

            bool placed = _inventory.TryPlace(selIdx, _board, x, y, _flowSim, rotation: 0);
            if (placed)
            {
                _undoStack.Push((x, y, selIdx));
                _moveCount++;
                _hudPanel.SetMoves(_moveCount);
                _renderers[x, y].color = GetPipeColor(0);
                var piece = _inventory.GetPieceAt(x, y);
                if (piece != null) DrawPipeShape(_renderers[x, y].gameObject, piece.Shape, piece.Rotation);
                StartCoroutine(PopAnim(_renderers[x, y].transform));
                _inventoryPanelComponent.Refresh();
                _inventoryPanelComponent.ClearSelection();
                _hudPanel.HideHint();
                if (_audioService != null) _audioService.PlaySound("pipe_place");
                _musicDirector.PlayBeep(660f, 0.08f);
                if (_particleFx != null)
                    _particleFx.PlacementPuff(_renderers[x, y].transform.position, GetPipeColor(0));
                if (_cameraShake != null) _cameraShake.Shake(0.05f, 0.03f);

                if (!_solved && !_flowSim.IsRunning && CheckAllConnected())
                {
                    StartCoroutine(RunFlowSimulation());
                }
            }
        }

        private bool CheckAllConnected()
        {
            if (_inventory.PlacedCount == 0) return false;
            var router = new PipeRouter(_board);
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
                ClearPipeShape(_renderers[x, y].gameObject);
                _renderers[x, y].color = DarkTile;
                _moveCount = Mathf.Max(0, _moveCount - 1);
                _hudPanel.SetMoves(_moveCount);
                _inventoryPanelComponent.Refresh();
                _undoStack.Pop();
                if (_audioService != null) _audioService.PlaySound("undo");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FLOW SIMULATION
        // ═══════════════════════════════════════════════════════════════

        public void OnFlowButtonPressed()
        {
            if (_solved || _flowSim.IsRunning) return;
            if (_inventory.PlacedCount == 0) return;

            StartCoroutine(RunFlowSimulation());
        }

        private IEnumerator RunFlowSimulation()
        {
            _flowButtonComponent.SetInteractable(false);
            _inventoryPanelComponent.SetLocked(true);

            _flowSim.OnFlowAdvance += HandleFlowAdvance;
            _flowSim.OnPipeBurst += HandlePipeBurst;
            _flowSim.OnColorMix += HandleColorMix;
            _flowSim.OnTargetReached += HandleTargetReached;

            _flowSim.StartSimulation(_board, _level, _inventory);

            while (_flowSim.GetResult() == SimulationResult.InProgress)
            {
                yield return new WaitForSeconds(_flowTickInterval);
                _flowSim.Tick();
            }

            _flowSim.OnFlowAdvance -= HandleFlowAdvance;
            _flowSim.OnPipeBurst -= HandlePipeBurst;
            _flowSim.OnColorMix -= HandleColorMix;
            _flowSim.OnTargetReached -= HandleTargetReached;

            var result = _flowSim.GetResult();
            if (result == SimulationResult.AllTargetsReached)
            {
                _solved = true;
                _musicDirector.PlayBeep(880f, 0.3f);
                _starsEarned = ScoreCalculator.Calculate(_inventory, _flowSim, _level);
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
        }

        private void HandlePipeBurst(int x, int y)
        {
            if (_renderers[x, y] != null)
            {
                _renderers[x, y].color = new Color(0.5f, 0.1f, 0.05f);
                StartCoroutine(BurstAnim(_renderers[x, y].transform));
                if (_particleFx != null)
                    _particleFx.BurstExplosion(_renderers[x, y].transform.position);
            }
            _inventory.MarkBurst(x, y);
            if (_audioService != null) _audioService.PlaySound("pipe_burst");
            if (_cameraShake != null) _cameraShake.Shake(0.2f, 0.15f);
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
            int nextLevel;
            if (SaveGameManager.Instance != null)
                nextLevel = SaveGameManager.Instance.CurrentLevel;
            else
                nextLevel = _levelNumber + 1;

            if (nextLevel > _maxLevel) nextLevel = 1;
            LoadLevel(nextLevel);
        }

        private void LoadLevel(int levelNum)
        {
            _gridBuilder.Clear();

            _solved = false;
            _moveCount = 0;
            _starsEarned = 0;
            _undoStack.Clear();
            _winPopupComponent.Hide();

            _levelNumber = levelNum;
            _level = _levelRepo.GetLevel(_levelNumber);

            if (SaveGameManager.Instance != null)
            {
                SaveGameManager.Instance.SaveProgress();
            }
            _board = new GridBoard(_level);
            _flowSim = new FlowSimulator();
            _inventory = new PipeInventory(_level.Inventory);

            _renderers = _gridBuilder.Build(_level, _board, _tileSize,
                _pipeTilePrefab, _sourceTilePrefab, _targetTilePrefab, _obstacleTilePrefab, this);

            _hudPanel.SetMoves(0);
            _hudPanel.SetLevel(_levelNumber, _maxLevel);
            _inventoryPanelComponent.Bind(_inventory);
            _flowButtonComponent.SetInteractable(true);
            _inventoryPanelComponent.SetLocked(false);

            if (_levelNumber == 1)
                _hudPanel.ShowHint("Connect the glowing CYAN source to the CYAN target!\n" +
                                   "TAP a pipe below \u2192 TAP a dark cell \u2192 watch the flow!");
            else
                _hudPanel.HideHint();
        }

        // ═══════════════════════════════════════════════════════════════
        // COLOR HELPERS
        // ═══════════════════════════════════════════════════════════════

        private Color GetPipeColor(int ci) => ci switch
        {
            0 => NeonCyan,
            1 => NeonMagenta,
            2 => NeonYellow,
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

        private IEnumerator FlowLerpAnim(SpriteRenderer sr, Color fromColor, Color toColor)
        {
            float duration = 0.15f;
            float elapsed = 0f;
            var originalScale = sr.transform.localScale;
            sr.color = fromColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                sr.color = Color.Lerp(fromColor, toColor, smoothT);
                // Scale pulse: 1.15x at start → 1.0x at end
                float scaleT = 1f + 0.15f * (1f - t);
                sr.transform.localScale = originalScale * scaleT;
                yield return null;
            }

            sr.color = toColor;
            sr.transform.localScale = originalScale;
        }

        private IEnumerator BurstAnim(Transform t)
        {
            float d = 0.4f, e = 0f;
            var orig = t.localPosition;
            while (e < d) { e += Time.deltaTime; t.localPosition = orig + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), 0); yield return null; }
            t.localPosition = orig;
        }

        private IEnumerator MixFlashAnim(SpriteRenderer sr)
        {
            var orig = sr.color;
            sr.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            sr.color = orig;
        }

        private IEnumerator TargetBloomAnim(SpriteRenderer sr, int colorIndex)
        {
            var targetColor = GetPipeColor(colorIndex);
            float d = 0.5f, e = 0f;
            var o = sr.transform.localScale;
            var origColor = sr.color;
            while (e < d)
            {
                e += Time.deltaTime;
                float t = e / d;
                sr.color = Color.Lerp(origColor, targetColor * 1.5f, t);
                sr.transform.localScale = o * (1f + t * 0.5f);
                yield return null;
            }
            sr.color = targetColor;
            sr.transform.localScale = o;
        }

        private IEnumerator FlashFailure()
        {
            float d = 0.6f;
            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                    if (_renderers[x, y] != null)
                        _renderers[x, y].color = new Color(0.3f, 0.05f, 0.05f);
            yield return new WaitForSeconds(d);
            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                {
                    if (_renderers[x, y] == null) continue;
                    var cell = _board.GetCell(x, y);
                    _renderers[x, y].color = cell.Type switch
                    {
                        CellType.Source when cell.ColorIndex == 0 => CyanHint,
                        CellType.Source when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Target when cell.ColorIndex == 0 => CyanHint,
                        CellType.Target when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Obstacle => ObstacleCol,
                        CellType.FlowGate => DarkTile,
                        _ => DarkTile
                    };
                }
        }

        // ═══════════════════════════════════════════════════════════════
        // PIPE SHAPE DRAWING (procedural — no external sprites needed)
        // ═══════════════════════════════════════════════════════════════

        private void DrawPipeShape(GameObject tile, PieceShape shape, int rotation = 0)
        {
            ClearPipeShape(tile);
            var parentSr = tile.GetComponent<SpriteRenderer>();
            if (parentSr == null) return;

            var root = new GameObject("Shape_Root");
            root.transform.SetParent(tile.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;

            // Compute effective rotation for texture generation
            int texRotation = shape switch
            {
                PieceShape.Straight => rotation % 180,
                _ => rotation
            };

            // Neutral pipe color (colored during flow)
            Color pipeColor = new Color(0.3f, 0.35f, 0.45f, 1f);
            Color glowColor = pipeColor * 0.4f;
            glowColor.a = 0.6f;

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = PipeTextureFactory.CreatePipeSprite(shape, texRotation, pipeColor, glowColor);
            sr.sortingOrder = parentSr.sortingOrder + 3;
        }

        private static int _shapeCounter;

        private void AddBar(GameObject parent, string id, Vector3 scale, Vector3 offset, SpriteRenderer parentSr)
        {
            var go = new GameObject("Shape_" + id + "_" + _shapeCounter++);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = parentSr.sprite;
            sr.color = new Color(0.5f, 0.5f, 0.55f, 0.7f);
            sr.sortingOrder = parentSr.sortingOrder + 3;
        }

        private void ClearPipeShape(GameObject tile)
        {
            for (int i = tile.transform.childCount - 1; i >= 0; i--)
            {
                var child = tile.transform.GetChild(i);
                if (child.name == "Shape_Root")
                    Destroy(child.gameObject);
            }
        }
    }
}
