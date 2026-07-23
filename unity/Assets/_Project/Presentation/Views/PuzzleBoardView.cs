using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        [SerializeField] private float _flowTickInterval = 0.3f; // Seconds per flow tick

        private GridBoard _board;
        private FlowSimulator _flowSim;
        private PipeInventory _inventory;
        private SpriteRenderer[,] _renderers;
        private LevelData _level;
        private LevelRepository _levelRepo = new();
        private int _maxLevel;

        // UI
        private GameObject _winPopup, _moveCounter, _levelLabel, _tutorialHint;
        private GameObject _inventoryPanel, _flowButton;
        private List<GameObject> _inventorySlots = new();
        private int _selectedPieceIndex = -1;

        // State
        private bool _solved;
        private int _moveCount;
        private int _starsEarned;
        private readonly Stack<(int x, int y, int pieceIdx)> _undoStack = new();

        // Colors
        private static readonly Color NeonCyan = new(0.2f, 0.9f, 0.95f);
        private static readonly Color NeonMagenta = new(0.95f, 0.2f, 0.7f);
        private static readonly Color NeonYellow = new(0.95f, 0.9f, 0.1f);
        private static readonly Color NeonPurple = new(0.65f, 0.2f, 0.95f);
        private static readonly Color NeonGreen = new(0.2f, 0.95f, 0.3f);
        private static readonly Color NeonOrange = new(0.95f, 0.5f, 0.1f);
        private static readonly Color DarkTile = new(0.08f, 0.08f, 0.1f);
        private static readonly Color DarkBG = new(0.02f, 0.02f, 0.04f);
        private static readonly Color CyanHint = new(0.06f, 0.16f, 0.20f);
        private static readonly Color MagentaHint = new(0.16f, 0.06f, 0.13f);
        private static readonly Color YellowHint = new(0.14f, 0.14f, 0.06f);
        private static readonly Color PurpleHint = new(0.1f, 0.05f, 0.15f);
        private static readonly Color ObstacleCol = new(0.18f, 0.07f, 0.07f);
        private static readonly Color FlowGateUp = new(0.15f, 0.25f, 0.10f);
        private static readonly Color FlowGateDown = new(0.25f, 0.15f, 0.10f);
        private static readonly Color FlowGateRight = new(0.10f, 0.15f, 0.25f);
        private static readonly Color FlowGateLeft = new(0.20f, 0.10f, 0.15f);

        private void Start()
        {
            _maxLevel = _levelRepo.LevelCount;
            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _flowSim = new FlowSimulator();
            _inventory = new PipeInventory(_level.Inventory);
            BuildGrid();
            CreateUI();
        }

        // ═══════════════════════════════════════════════════════════════
        // GRID BUILDING
        // ═══════════════════════════════════════════════════════════════

        private void BuildGrid()
        {
            _renderers = new SpriteRenderer[_board.Width, _board.Height];
            var off = new Vector3(-_board.Width * _tileSize / 2f, -_board.Height * _tileSize / 2f, 0);

            for (int x = 0; x < _board.Width; x++)
            for (int y = 0; y < _board.Height; y++)
            {
                var cell = _board.GetCell(x, y);
                GameObject prefab = cell.Type switch
                {
                    CellType.Source => _sourceTilePrefab,
                    CellType.Target => _targetTilePrefab,
                    CellType.Obstacle => _obstacleTilePrefab ?? _pipeTilePrefab,
                    CellType.FlowGate => _pipeTilePrefab,
                    _ => _pipeTilePrefab
                };
                if (prefab == null) continue;
                var tile = Instantiate(prefab,
                    new Vector3(x * _tileSize + off.x, y * _tileSize + off.y, 0),
                    Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{y}";

                var sr = tile.GetComponent<SpriteRenderer>();
                if (sr == null) sr = tile.AddComponent<SpriteRenderer>();
                _renderers[x, y] = sr;

                sr.color = cell.Type switch
                {
                    CellType.Source when cell.ColorIndex == 0 => CyanHint,
                    CellType.Source when cell.ColorIndex == 1 => MagentaHint,
                    CellType.Source when cell.ColorIndex == 2 => YellowHint,
                    CellType.Target when cell.ColorIndex == 0 => CyanHint,
                    CellType.Target when cell.ColorIndex == 1 => MagentaHint,
                    CellType.Target when cell.ColorIndex == 2 => YellowHint,
                    CellType.Target when cell.ColorIndex == 6 => PurpleHint,
                    CellType.Target when cell.ColorIndex == 7 => new Color(0.05f, 0.15f, 0.05f),
                    CellType.Obstacle => ObstacleCol,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Up => FlowGateUp,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Down => FlowGateDown,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Right => FlowGateRight,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Left => FlowGateLeft,
                    _ => DarkTile
                };

                // Source/Target/FlowGate indicators (same as before)
                if (cell.Type == CellType.Source) AddIndicator(tile, "SrcDot", GetPipeColor(cell.ColorIndex) * 2f, 0.3f, 1);
                if (cell.Type == CellType.Target) AddIndicator(tile, "TgtRing", new Color(1f, 1f, 1f, 0.5f), 1.4f, 1);
                if (cell.Type == CellType.FlowGate) AddFlowGateArrow(tile, cell.FlowDirection);

                // Click handler for empty cells (piece placement)
                var col = tile.AddComponent<BoxCollider>();
                col.size = new Vector3(1, 1, 0.1f) * _tileSize;
                tile.AddComponent<TileClickHandler>().Init(x, y, this);
            }
            SetupCamera();
        }

        private void AddIndicator(GameObject parent, string name, Color color, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            var parentSr = parent.GetComponent<SpriteRenderer>();
            if (sr != null && parentSr != null) { sr.sprite = parentSr.sprite; sr.color = color; sr.sortingOrder = order; }
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void AddFlowGateArrow(GameObject parent, PipeDirection dir)
        {
            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(parent.transform, false);
            arrow.transform.localPosition = Vector3.zero;
            var sr = arrow.AddComponent<SpriteRenderer>();
            var parentSr = parent.GetComponent<SpriteRenderer>();
            if (sr != null && parentSr != null) { sr.sprite = parentSr.sprite; sr.color = Color.white; sr.sortingOrder = 1; }
            arrow.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            float angle = dir switch
            {
                PipeDirection.Up => 0f, PipeDirection.Right => 270f,
                PipeDirection.Down => 180f, PipeDirection.Left => 90f,
                _ => 0f
            };
            arrow.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, -0.8f, -10);
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(_board.Width, _board.Height) * _tileSize / 2f + 2f;
                cam.backgroundColor = DarkBG;
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

            // If there's a placed pipe here, rotate or remove it
            int existingIdx = _inventory.GetPieceIndexAt(x, y);
            if (existingIdx >= 0 && cell.Type == CellType.Pipe)
            {
                // Undo this placement on tap
                UndoPlacement(x, y, existingIdx);
                return;
            }

            // Place selected piece on empty cell
            if (cell.Type == CellType.Empty && _selectedPieceIndex >= 0)
            {
                PlaceSelectedPiece(x, y);
                return;
            }
        }

        private void PlaceSelectedPiece(int x, int y)
        {
            if (_selectedPieceIndex < 0) return;

            bool placed = _inventory.TryPlace(_selectedPieceIndex, _board, x, y, _flowSim);
            if (placed)
            {
                _undoStack.Push((x, y, _selectedPieceIndex));
                _moveCount++;
                UpdateMoveCounter();
                _renderers[x, y].color = GetPipeColor(0);
                // Draw the visual pipe shape on the tile
                var piece = _inventory.GetPieceAt(x, y);
                if (piece != null) DrawPipeShape(_renderers[x, y].gameObject, piece.Shape);
                StartCoroutine(PopAnim(_renderers[x, y].transform));
                UpdateInventoryUI();
                _selectedPieceIndex = -1;
                HighlightSelected(null);
                if (_tutorialHint != null && _tutorialHint.activeSelf)
                    _tutorialHint.SetActive(false);
            }
        }

        private void UndoPlacement(int x, int y, int pieceIdx)
        {
            bool undone = _inventory.TryUndo(_board);
            if (undone)
            {
                ClearPipeShape(_renderers[x, y].gameObject);
                _renderers[x, y].color = DarkTile;
                _moveCount = Mathf.Max(0, _moveCount - 1);
                UpdateMoveCounter();
                UpdateInventoryUI();
                // Pop the undo stack if this was the last placed piece
                if (_undoStack.Count > 0)
                {
                    var top = _undoStack.Peek();
                    if (top.x == x && top.y == y)
                        _undoStack.Pop();
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FLOW SIMULATION
        // ═══════════════════════════════════════════════════════════════

        public void OnFlowButtonPressed()
        {
            if (_solved || _flowSim.IsRunning) return;
            if (_inventory.PlacedCount == 0) return; // Nothing placed yet

            StartCoroutine(RunFlowSimulation());
        }

        private IEnumerator RunFlowSimulation()
        {
            // Lock UI
            if (_flowButton != null) _flowButton.GetComponent<Button>().interactable = false;
            DisableInventoryInteraction();

            // Start simulation
            _flowSim.OnFlowAdvance += HandleFlowAdvance;
            _flowSim.OnPipeBurst += HandlePipeBurst;
            _flowSim.OnColorMix += HandleColorMix;
            _flowSim.OnTargetReached += HandleTargetReached;

            _flowSim.StartSimulation(_board, _level, _inventory);

            // Run ticks
            while (_flowSim.GetResult() == SimulationResult.InProgress)
            {
                yield return new WaitForSeconds(_flowTickInterval);
                _flowSim.Tick();
            }

            // Cleanup
            _flowSim.OnFlowAdvance -= HandleFlowAdvance;
            _flowSim.OnPipeBurst -= HandlePipeBurst;
            _flowSim.OnColorMix -= HandleColorMix;
            _flowSim.OnTargetReached -= HandleTargetReached;

            // Check result
            var result = _flowSim.GetResult();
            if (result == SimulationResult.AllTargetsReached)
            {
                _solved = true;
                _starsEarned = CalculateStars();
                yield return new WaitForSeconds(0.5f);
                ShowWinPopup();
            }
            else
            {
                // Flow stopped — lose state
                // Flash board red briefly to indicate failure
                yield return StartCoroutine(FlashFailure());
                // Auto-reset after failure
                yield return new WaitForSeconds(1f);
                ResetPuzzle();
            }

            // Re-enable UI
            if (_flowButton != null) _flowButton.GetComponent<Button>().interactable = true;
            EnableInventoryInteraction();
        }

        private void HandleFlowAdvance(int x, int y, int colorIndex)
        {
            if (_renderers[x, y] != null)
            {
                _renderers[x, y].color = GetPipeColor(colorIndex);
                StartCoroutine(FlowPulseAnim(_renderers[x, y].transform));
            }
        }

        private void HandlePipeBurst(int x, int y)
        {
            if (_renderers[x, y] != null)
            {
                _renderers[x, y].color = new Color(0.5f, 0.1f, 0.05f); // Burnt orange
                StartCoroutine(BurstAnim(_renderers[x, y].transform));
            }
            _inventory.MarkBurst(x, y);
        }

        private void HandleColorMix(int x, int y, int colorA, int colorB)
        {
            // Visual: brief flash white at mixing cell
            if (_renderers[x, y] != null)
            {
                StartCoroutine(MixFlashAnim(_renderers[x, y]));
            }
        }

        private void HandleTargetReached(int x, int y, int colorIndex)
        {
            if (_renderers[x, y] != null)
            {
                StartCoroutine(TargetBloomAnim(_renderers[x, y], colorIndex));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STAR SCORING
        // ═══════════════════════════════════════════════════════════════

        private int CalculateStars()
        {
            int stars = 1; // Base: completed the level
            float efficiency = _inventory.GetEfficiency();
            bool noBursts = _inventory.BurstCount == 0;

            // ★★: No bursts + used ≤ 80% of available pieces
            if (noBursts && efficiency >= 0.2f)
                stars = 2;

            // ★★★: No bursts + used ≤ 60% + on par (ticks ≤ 2× par)
            bool onPar = _flowSim.CurrentTick <= _level.ParTicks * 2;
            if (noBursts && efficiency >= 0.4f && onPar)
                stars = 3;

            return stars;
        }

        // ═══════════════════════════════════════════════════════════════
        // UI CREATION
        // ═══════════════════════════════════════════════════════════════

        private void CreateUI()
        {
            EnsureEventSystem();
            CreateTopBar();
            CreateFlowButton();
            CreateInventoryPanel();
            CreateTutorialHint();
            CreateWinPopup();
            UpdateLevelLabel();
            UpdateMoveCounter();
            UpdateInventoryUI();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        private void CreateTopBar()
        {
            var mc = new GameObject("MoveCounter");
            mc.transform.SetParent(transform);
            var cv = mc.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 50;
            mc.AddComponent<CanvasScaler>(); mc.AddComponent<GraphicRaycaster>();

            // Move counter
            var ct = new GameObject("CounterText");
            ct.transform.SetParent(mc.transform, false);
            var tx = ct.AddComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = 16; tx.alignment = TextAnchor.UpperLeft; tx.color = NeonCyan;
            var tr = tx.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.02f, 0.94f); tr.anchorMax = new Vector2(0.3f, 0.99f);
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            _moveCounter = mc;

            // Level label
            var ll = new GameObject("LevelLabel");
            ll.transform.SetParent(mc.transform, false);
            var ltx = ll.AddComponent<Text>();
            ltx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ltx.fontSize = 16; ltx.alignment = TextAnchor.UpperCenter; ltx.color = new Color(0.4f, 0.4f, 0.5f);
            var llr = ltx.GetComponent<RectTransform>();
            llr.anchorMin = new Vector2(0.35f, 0.94f); llr.anchorMax = new Vector2(0.65f, 0.99f);
            llr.offsetMin = Vector2.zero; llr.offsetMax = Vector2.zero;
            _levelLabel = ll;

            // Reset button
            var rb = new GameObject("ResetBtn");
            rb.transform.SetParent(mc.transform, false);
            var rimg = rb.AddComponent<Image>(); rimg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var rbtn = rb.AddComponent<Button>(); rbtn.onClick.AddListener(ResetPuzzle);
            var rr = rb.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.7f, 0.94f); rr.anchorMax = new Vector2(0.95f, 0.99f);
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var rt = new GameObject("ResetText");
            rt.transform.SetParent(rb.transform, false);
            var rtx = rt.AddComponent<Text>();
            rtx.text = "RESET"; rtx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rtx.fontSize = 14; rtx.alignment = TextAnchor.MiddleCenter; rtx.color = Color.white;
            var rtr = rtx.GetComponent<RectTransform>();
            rtr.anchorMin = Vector2.zero; rtr.anchorMax = Vector2.one; rtr.sizeDelta = Vector2.zero;
        }

        private void CreateFlowButton()
        {
            var fb = new GameObject("FlowButton");
            fb.transform.SetParent(transform);
            var fbcv = fb.AddComponent<Canvas>();
            fbcv.renderMode = RenderMode.ScreenSpaceOverlay; fbcv.sortingOrder = 60;
            fb.AddComponent<CanvasScaler>(); fb.AddComponent<GraphicRaycaster>();

            var btnGo = new GameObject("FlowBtn");
            btnGo.transform.SetParent(fb.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.15f, 0.5f, 0.6f, 0.95f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(OnFlowButtonPressed);
            var br = btnGo.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.3f, 0.78f); br.anchorMax = new Vector2(0.7f, 0.84f);
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;

            var txt = new GameObject("FlowText");
            txt.transform.SetParent(btnGo.transform, false);
            var ftx = txt.AddComponent<Text>();
            ftx.text = "▶ FLOW ON";
            ftx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ftx.fontSize = 20; ftx.alignment = TextAnchor.MiddleCenter; ftx.color = Color.white;
            var ftr = ftx.GetComponent<RectTransform>();
            ftr.anchorMin = Vector2.zero; ftr.anchorMax = Vector2.one; ftr.sizeDelta = Vector2.zero;

            _flowButton = btnGo;
        }

        private void CreateInventoryPanel()
        {
            var ip = new GameObject("InventoryPanel");
            ip.transform.SetParent(transform);
            var ipcv = ip.AddComponent<Canvas>();
            ipcv.renderMode = RenderMode.ScreenSpaceOverlay; ipcv.sortingOrder = 60;
            ip.AddComponent<CanvasScaler>(); ip.AddComponent<GraphicRaycaster>();
            _inventoryPanel = ip;

            // Background strip
            var bg = new GameObject("InvBG");
            bg.transform.SetParent(ip.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            var bgr = bg.GetComponent<RectTransform>();
            bgr.anchorMin = new Vector2(0f, 0f); bgr.anchorMax = new Vector2(1f, 0.12f);
            bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;

            // Inventory slots will be populated in UpdateInventoryUI
        }

        private void UpdateInventoryUI()
        {
            // Clear existing slots
            foreach (var slot in _inventorySlots)
                if (slot != null) Destroy(slot);
            _inventorySlots.Clear();

            if (_inventoryPanel == null) return;

            var available = _inventory.GetAvailableCounts();
            if (available.Count == 0) return;

            float totalWidth = available.Count;
            float slotWidth = 0.8f / totalWidth;
            float startX = 0.1f;
            int idx = 0;

            foreach (var kvp in available)
            {
                var shape = kvp.Key;
                int count = kvp.Value;

                var slot = new GameObject($"Slot_{shape}");
                slot.transform.SetParent(_inventoryPanel.transform, false);

                var img = slot.AddComponent<Image>();
                img.color = GetShapeColor(shape);
                var sr = slot.GetComponent<RectTransform>();
                sr.anchorMin = new Vector2(startX + idx * slotWidth, 0.02f);
                sr.anchorMax = new Vector2(startX + (idx + 1) * slotWidth - 0.01f, 0.10f);
                sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;

                // Click handler
                var btn = slot.AddComponent<Button>();
                int capturedIdx = idx;
                btn.onClick.AddListener(() => SelectInventoryPiece(shape));

                // Label
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(slot.transform, false);
                var labelTx = labelGo.AddComponent<Text>();
                labelTx.text = $"{ShapeSymbol(shape)} ×{count}";
                labelTx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTx.fontSize = 11; labelTx.alignment = TextAnchor.MiddleCenter; labelTx.color = Color.white;
                var lr = labelTx.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.sizeDelta = Vector2.zero;

                _inventorySlots.Add(slot);
                idx++;
            }
        }

        private void SelectInventoryPiece(PieceShape shape)
        {
            if (_solved || _flowSim.IsRunning) return;

            // Find the first available piece of this shape in the inventory
            for (int i = 0; i < _inventory.Pieces.Count; i++)
            {
                if (_inventory.Pieces[i].State == PieceState.InHand &&
                    _inventory.Pieces[i].Shape == shape)
                {
                    _selectedPieceIndex = i;
                    HighlightSelected(_inventorySlots.Find(s => s != null && s.name == $"Slot_{shape}"));
                    return;
                }
            }
        }

        private void HighlightSelected(GameObject slot)
        {
            // Deselect all — reset to default
            foreach (var s in _inventorySlots)
            {
                if (s != null)
                {
                    var img = s.GetComponent<Image>();
                    if (img != null)
                    {
                        var c = img.color;
                        img.color = new Color(c.r, c.g, c.b, 0.75f);
                    }
                    s.transform.localScale = Vector3.one;
                }
            }
            // Select — bright + scale up
            if (slot != null)
            {
                var img = slot.GetComponent<Image>();
                if (img != null)
                {
                    var c = img.color;
                    img.color = new Color(c.r, c.g, c.b, 1f);
                }
                StartCoroutine(SelectionPulse(slot.transform));
            }
        }

        private IEnumerator SelectionPulse(Transform t)
        {
            float d = 0.4f;
            var orig = t.localScale;
            t.localScale = orig * 1.15f;
            yield return new WaitForSeconds(d);
            t.localScale = orig;
        }

        private void DisableInventoryInteraction()
        {
            foreach (var slot in _inventorySlots)
                if (slot != null) slot.GetComponent<Button>().interactable = false;
        }

        private void EnableInventoryInteraction()
        {
            foreach (var slot in _inventorySlots)
                if (slot != null) slot.GetComponent<Button>().interactable = true;
        }

        private void CreateTutorialHint()
        {
            var th = new GameObject("TutorialHint");
            th.transform.SetParent(transform);
            var cv = th.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 55;
            th.AddComponent<CanvasScaler>(); th.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(th.transform, false);
            var thtx = textGo.AddComponent<Text>();
            thtx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            thtx.fontSize = 13; thtx.alignment = TextAnchor.LowerCenter; thtx.color = new Color(0.5f, 0.7f, 0.8f, 0.9f);
            var thr = thtx.GetComponent<RectTransform>();
            thr.anchorMin = new Vector2(0.05f, 0.14f); thr.anchorMax = new Vector2(0.95f, 0.22f);
            thr.offsetMin = Vector2.zero; thr.offsetMax = Vector2.zero;

            _tutorialHint = th;
            if (_levelNumber == 1)
            {
                int srcColor = _level.Sources.Length > 0 ? _level.Sources[0].ColorIndex : 0;
                string colorName = srcColor == 0 ? "CYAN" : srcColor == 1 ? "MAGENTA" : "YELLOW";
                thtx.text = $"Connect the {colorName} source ▶ to the {colorName} target ◼\n" +
                            "1. TAP a pipe piece below to select it\n" +
                            "2. TAP dark cells to build a path\n" +
                            "3. Press ▶ FLOW ON to send the flow!";
            }
            else
                th.SetActive(false);
        }

        private void CreateWinPopup()
        {
            var wc = new GameObject("WinCanvas");
            wc.transform.SetParent(transform);
            var wcv = wc.AddComponent<Canvas>();
            wcv.renderMode = RenderMode.ScreenSpaceOverlay; wcv.sortingOrder = 100;
            wc.AddComponent<CanvasScaler>(); wc.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("WinBG");
            bg.transform.SetParent(wc.transform, false);
            var bi = bg.AddComponent<Image>();
            bi.color = new Color(0, 0, 0, 0f); bi.raycastTarget = false;
            var br = bg.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.sizeDelta = Vector2.zero;

            // Title
            var t1 = new GameObject("WinMain");
            t1.transform.SetParent(bg.transform, false);
            var wtx = t1.AddComponent<Text>();
            wtx.text = "PIPELINE ONLINE";
            wtx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wtx.fontSize = 32; wtx.alignment = TextAnchor.MiddleCenter; wtx.color = NeonCyan;
            var wr = wtx.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(0.5f, 0.7f); wr.anchorMax = new Vector2(0.5f, 0.7f);
            wr.sizeDelta = new Vector2(500, 70);

            // Subtitle
            var t2 = new GameObject("WinSub");
            t2.transform.SetParent(bg.transform, false);
            var wsx = t2.AddComponent<Text>();
            wsx.text = "Flow delivered successfully.";
            wsx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wsx.fontSize = 15; wsx.alignment = TextAnchor.MiddleCenter; wsx.color = new Color(0.5f, 0.5f, 0.6f);
            var wr2 = wsx.GetComponent<RectTransform>();
            wr2.anchorMin = new Vector2(0.5f, 0.6f); wr2.anchorMax = new Vector2(0.5f, 0.6f);
            wr2.sizeDelta = new Vector2(400, 35);

            // Stars
            var t3 = new GameObject("WinStars");
            t3.transform.SetParent(bg.transform, false);
            var scx = t3.AddComponent<Text>();
            scx.text = "★☆☆";
            scx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scx.fontSize = 30; scx.alignment = TextAnchor.MiddleCenter; scx.color = NeonYellow;
            var wr3 = scx.GetComponent<RectTransform>();
            wr3.anchorMin = new Vector2(0.5f, 0.5f); wr3.anchorMax = new Vector2(0.5f, 0.5f);
            wr3.sizeDelta = new Vector2(400, 40);

            // Score
            var t4 = new GameObject("WinScore");
            t4.transform.SetParent(bg.transform, false);
            var scx2 = t4.AddComponent<Text>();
            scx2.text = "Completed in 12 moves";
            scx2.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scx2.fontSize = 13; scx2.alignment = TextAnchor.MiddleCenter; scx2.color = new Color(0.3f, 0.3f, 0.4f);
            var wr4 = scx2.GetComponent<RectTransform>();
            wr4.anchorMin = new Vector2(0.5f, 0.43f); wr4.anchorMax = new Vector2(0.5f, 0.43f);
            wr4.sizeDelta = new Vector2(400, 25);

            // Play Again
            var pa = CreatePopupButton(bg, "Play Again", new Vector2(0.5f, 0.3f), ResetPuzzle,
                new Color(0.1f, 0.4f, 0.45f, 0.9f));

            // Next Level
            var nl = CreatePopupButton(bg, "NEXT LEVEL ▶", new Vector2(0.5f, 0.2f), AdvanceLevel,
                new Color(0.35f, 0.15f, 0.45f, 0.9f));

            _winPopup = wc;
            _winPopup.SetActive(false);
        }

        private GameObject CreatePopupButton(GameObject parent, string label, Vector2 anchor,
            UnityEngine.Events.UnityAction onClick, Color color)
        {
            var go = new GameObject(label + "Btn");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>(); img.color = color;
            var btn = go.AddComponent<Button>(); btn.onClick.AddListener(onClick);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = anchor; r.anchorMax = anchor;
            r.sizeDelta = new Vector2(160, 36); r.anchoredPosition = Vector2.zero;

            var txt = new GameObject(label + "Text");
            txt.transform.SetParent(go.transform, false);
            var tx = txt.AddComponent<Text>();
            tx.text = label; tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = 14; tx.alignment = TextAnchor.MiddleCenter; tx.color = Color.white;
            var tr = tx.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;

            return go;
        }

        // ═══════════════════════════════════════════════════════════════
        // WIN / RESET / LEVEL PROGRESSION
        // ═══════════════════════════════════════════════════════════════

        private void ShowWinPopup()
        {
            var ts = _winPopup.GetComponentsInChildren<Text>();
            foreach (var t in ts)
            {
                if (t.name == "WinMain")
                    t.text = "PIPELINE ONLINE";
                if (t.name == "WinSub")
                    t.text = "Flow delivered successfully.";
                if (t.name == "WinStars")
                {
                    string starStr = _starsEarned switch
                    {
                        3 => "★★★",
                        2 => "★★☆",
                        _ => "★☆☆"
                    };
                    t.text = starStr;
                    t.color = _starsEarned switch { 3 => NeonYellow, 2 => new Color(0.9f, 0.9f, 0.2f), _ => new Color(0.5f, 0.5f, 0.5f) };
                }
                if (t.name == "WinScore")
                    t.text = $"Completed in {_moveCount} placements · {_flowSim.CurrentTick} ticks";
            }
            _winPopup.SetActive(true);
            var bg = _winPopup.GetComponentInChildren<Image>();
            if (bg != null) StartCoroutine(FadeBg(bg));
        }

        private System.Collections.IEnumerator FadeBg(Image bg)
        {
            float e = 0f, d = 0.8f;
            while (e < d)
            {
                e += Time.deltaTime;
                var c = bg.color; c.a = Mathf.Lerp(0f, 0.8f, e / d);
                bg.color = c;
                yield return null;
            }
        }

        public void ResetPuzzle()
        {
            _solved = false;
            _moveCount = 0;
            _starsEarned = 0;
            _selectedPieceIndex = -1;
            _undoStack.Clear();

            if (_winPopup != null) _winPopup.SetActive(false);

            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _flowSim = new FlowSimulator();
            _inventory = new PipeInventory(_level.Inventory);

            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                {
                    var sr = _renderers[x, y];
                    if (sr == null) continue;
                    var cell = _board.GetCell(x, y);
                    sr.color = cell.Type switch
                    {
                        CellType.Source when cell.ColorIndex == 0 => CyanHint,
                        CellType.Source when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Source when cell.ColorIndex == 2 => YellowHint,
                        CellType.Target when cell.ColorIndex == 0 => CyanHint,
                        CellType.Target when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Target when cell.ColorIndex == 2 => YellowHint,
                        CellType.Target when cell.ColorIndex == 6 => PurpleHint,
                        CellType.Obstacle => ObstacleCol,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Up => FlowGateUp,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Down => FlowGateDown,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Right => FlowGateRight,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Left => FlowGateLeft,
                        _ => DarkTile
                    };
                }

            UpdateMoveCounter();
            UpdateInventoryUI();
            if (_flowButton != null) _flowButton.GetComponent<Button>().interactable = true;
            EnableInventoryInteraction();

            if (_levelNumber == 1 && _tutorialHint != null)
                _tutorialHint.SetActive(true);
        }

        private void AdvanceLevel()
        {
            if (_levelNumber >= _maxLevel) _levelNumber = 1;
            else _levelNumber++;
            LoadLevel(_levelNumber);
        }

        private void LoadLevel(int levelNum)
        {
            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                    if (_renderers[x, y] != null)
                        Destroy(_renderers[x, y].gameObject);

            _solved = false;
            _moveCount = 0;
            _starsEarned = 0;
            _selectedPieceIndex = -1;
            _undoStack.Clear();
            if (_winPopup != null) _winPopup.SetActive(false);

            _levelNumber = levelNum;
            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _flowSim = new FlowSimulator();
            _inventory = new PipeInventory(_level.Inventory);
            BuildGrid();
            UpdateMoveCounter();
            UpdateLevelLabel();
            UpdateInventoryUI();
            if (_flowButton != null) _flowButton.GetComponent<Button>().interactable = true;
            EnableInventoryInteraction();

            if (_levelNumber == 1 && _tutorialHint != null)
                _tutorialHint.SetActive(true);
            else if (_tutorialHint != null)
                _tutorialHint.SetActive(false);
        }

        private void UpdateMoveCounter()
        {
            if (_moveCounter == null) return;
            var tx = _moveCounter.GetComponentInChildren<Text>();
            if (tx != null) tx.text = $"PIECES: {_moveCount}";
        }

        private void UpdateLevelLabel()
        {
            if (_levelLabel == null) return;
            var tx = _levelLabel.GetComponent<Text>();
            if (tx != null) tx.text = $"LEVEL {_levelNumber}/{_maxLevel}";
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

        private Color GetShapeColor(PieceShape shape) => shape switch
        {
            PieceShape.Straight => new Color(0.2f, 0.3f, 0.5f, 0.8f),
            PieceShape.Elbow => new Color(0.2f, 0.5f, 0.3f, 0.8f),
            PieceShape.TJunction => new Color(0.5f, 0.3f, 0.2f, 0.8f),
            PieceShape.Cross => new Color(0.5f, 0.2f, 0.5f, 0.8f),
            PieceShape.Valve => new Color(0.3f, 0.5f, 0.5f, 0.8f),
            PieceShape.Amplifier => new Color(0.6f, 0.5f, 0.1f, 0.8f),
            PieceShape.Mixer => new Color(0.5f, 0.1f, 0.5f, 0.8f),
            PieceShape.Blocker => new Color(0.5f, 0.1f, 0.1f, 0.8f),
            _ => new Color(0.3f, 0.3f, 0.4f, 0.8f)
        };

        private static string ShapeSymbol(PieceShape shape) => shape switch
        {
            PieceShape.Straight => "─",
            PieceShape.Elbow => "└",
            PieceShape.TJunction => "├",
            PieceShape.Cross => "┼",
            PieceShape.Valve => "◇",
            PieceShape.Amplifier => "▲",
            PieceShape.Mixer => "✕",
            PieceShape.Blocker => "■",
            _ => "?"
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

        private IEnumerator BurstAnim(Transform t)
        {
            // Quick shake + flash
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
            // Brief red tint on all tiles
            float d = 0.6f;
            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                    if (_renderers[x, y] != null)
                        _renderers[x, y].color = new Color(0.3f, 0.05f, 0.05f);
            yield return new WaitForSeconds(d);
            // Reset to original colors
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

        private void DrawPipeShape(GameObject tile, PieceShape shape)
        {
            ClearPipeShape(tile);
            var parentSr = tile.GetComponent<SpriteRenderer>();
            if (parentSr == null) return;

            switch (shape)
            {
                case PieceShape.Straight:
                    AddBar(tile, "h", new Vector3(0.65f, 0.2f, 1f), Vector3.zero, parentSr);
                    break;
                case PieceShape.Elbow:
                    AddBar(tile, "h", new Vector3(0.45f, 0.2f, 1f), new Vector3(0.15f, -0.18f, 0), parentSr);
                    AddBar(tile, "v", new Vector3(0.2f, 0.45f, 1f), new Vector3(-0.18f, 0.15f, 0), parentSr);
                    break;
                case PieceShape.TJunction:
                    AddBar(tile, "h", new Vector3(0.65f, 0.2f, 1f), Vector3.zero, parentSr);
                    AddBar(tile, "v", new Vector3(0.2f, 0.35f, 1f), new Vector3(0f, 0.18f, 0), parentSr);
                    break;
                case PieceShape.Cross:
                    AddBar(tile, "h", new Vector3(0.65f, 0.2f, 1f), Vector3.zero, parentSr);
                    AddBar(tile, "v", new Vector3(0.2f, 0.65f, 1f), Vector3.zero, parentSr);
                    break;
                case PieceShape.Valve:
                    // Diamond indicator with arrow
                    AddBar(tile, "h", new Vector3(0.45f, 0.2f, 1f), Vector3.zero, parentSr);
                    AddBar(tile, "arr", new Vector3(0.2f, 0.2f, 1f), new Vector3(0.25f, 0f, 0), parentSr);
                    break;
                case PieceShape.Amplifier:
                    // Triangle-ish indicator
                    AddBar(tile, "h", new Vector3(0.4f, 0.3f, 1f), Vector3.zero, parentSr);
                    AddBar(tile, "plus", new Vector3(0.2f, 0.2f, 1f), new Vector3(0f, 0.2f, 0), parentSr);
                    break;
                case PieceShape.Mixer:
                    // X shape — two diagonal bars
                    AddBar(tile, "x1", new Vector3(0.55f, 0.2f, 1f), Vector3.zero, parentSr);
                    AddBar(tile, "x2", new Vector3(0.2f, 0.55f, 1f), Vector3.zero, parentSr);
                    break;
                case PieceShape.Blocker:
                    // Solid fill
                    AddBar(tile, "blk", new Vector3(0.7f, 0.7f, 1f), Vector3.zero, parentSr);
                    break;
            }
        }

        private void AddBar(GameObject parent, string id, Vector3 scale, Vector3 offset, SpriteRenderer parentSr)
        {
            var go = new GameObject($"Shape_{id}_{Time.frameCount}_{Random.Range(0,9999)}");
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
                if (child.name.StartsWith("Shape_"))
                    Destroy(child.gameObject);
            }
        }
    }

    /// <summary>
    /// Attached to each grid tile for click handling.
    /// </summary>
    public class TileClickHandler : MonoBehaviour
    {
        private int _x, _y;
        private PuzzleBoardView _board;
        public void Init(int x, int y, PuzzleBoardView b) { _x = x; _y = y; _board = b; }
        private void OnMouseDown() { _board.OnPointerDown(_x, _y); }
    }
}
