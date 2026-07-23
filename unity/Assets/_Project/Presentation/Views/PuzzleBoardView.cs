using System.Collections.Generic;
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

        private GridBoard _board;
        private PipeRouter _router;
        private SpriteRenderer[,] _renderers;
        private GameObject _winPopup, _moveCounter, _levelLabel, _tutorialHint;
        private bool _solved;
        private int _moveCount, _activeColor = -1;
        private LevelRepository _levelRepo = new();
        private int _maxLevel;
        private readonly Stack<(int x, int y, Color prev)> _undoStack = new();
        private readonly List<SpriteRenderer> _highlightedTargets = new();

        private static readonly Color NeonCyan = new(0.2f, 0.9f, 0.95f);
        private static readonly Color NeonMagenta = new(0.95f, 0.2f, 0.7f);
        private static readonly Color DarkTile = new(0.08f, 0.08f, 0.1f);
        private static readonly Color DarkBG = new(0.02f, 0.02f, 0.04f);
        private static readonly Color CyanHint = new(0.06f, 0.16f, 0.20f);
        private static readonly Color MagentaHint = new(0.16f, 0.06f, 0.13f);
        private static readonly Color ObstacleCol = new(0.18f, 0.07f, 0.07f);
        private static readonly Color FlowGateUp = new(0.15f, 0.25f, 0.10f);
        private static readonly Color FlowGateDown = new(0.25f, 0.15f, 0.10f);
        private static readonly Color FlowGateRight = new(0.10f, 0.15f, 0.25f);
        private static readonly Color FlowGateLeft = new(0.20f, 0.10f, 0.15f);

        private LevelData _level;

        private void Start() { _maxLevel = _levelRepo.LevelCount; _level = _levelRepo.GetLevel(_levelNumber); _board = new GridBoard(_level); _router = new PipeRouter(_board); BuildGrid(); CreateUI(); }

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
                var tile = Instantiate(prefab, new Vector3(x * _tileSize + off.x, y * _tileSize + off.y, 0), Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{y}";
                var sr = tile.GetComponent<SpriteRenderer>();
                if (sr == null) sr = tile.AddComponent<SpriteRenderer>();
                _renderers[x, y] = sr;
                if (sr != null) sr.color = cell.Type switch
                {
                    CellType.Source when cell.ColorIndex == 0 => CyanHint,
                    CellType.Source when cell.ColorIndex == 1 => MagentaHint,
                    CellType.Target when cell.ColorIndex == 0 => CyanHint,
                    CellType.Target when cell.ColorIndex == 1 => MagentaHint,
                    CellType.Obstacle => ObstacleCol,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Up => FlowGateUp,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Down => FlowGateDown,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Right => FlowGateRight,
                    CellType.FlowGate when cell.FlowDirection == PipeDirection.Left => FlowGateLeft,
                    _ => DarkTile
                };
                // Arrow indicator for flow gates
                if (cell.Type == CellType.FlowGate)
                {
                    var arrow = new GameObject("Arrow");
                    arrow.transform.SetParent(tile.transform, false);
                    arrow.transform.localPosition = Vector3.zero;
                    var arrowSr = arrow.AddComponent<SpriteRenderer>();
                    if (arrowSr != null) { arrowSr.sprite = sr.sprite; arrowSr.color = Color.white; arrowSr.sortingOrder = 1; }
                    arrow.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                    float angle = cell.FlowDirection switch
                    {
                        PipeDirection.Up => 0f,
                        PipeDirection.Right => 270f,
                        PipeDirection.Down => 180f,
                        PipeDirection.Left => 90f,
                        _ => 0f
                    };
                    arrow.transform.localRotation = Quaternion.Euler(0, 0, angle);
                }
                // Source indicator: small bright dot in center
                if (cell.Type == CellType.Source)
                {
                    var dot = new GameObject("SrcDot");
                    dot.transform.SetParent(tile.transform, false);
                    dot.transform.localPosition = Vector3.zero;
                    var dotSr = dot.AddComponent<SpriteRenderer>();
                    if (dotSr != null) { dotSr.sprite = sr.sprite; dotSr.color = GetPipeColor(cell.ColorIndex) * 2f; dotSr.sortingOrder = 1; }
                    dot.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                }
                // Target indicator: larger ring outline
                if (cell.Type == CellType.Target)
                {
                    var ring = new GameObject("TgtRing");
                    ring.transform.SetParent(tile.transform, false);
                    ring.transform.localPosition = Vector3.zero;
                    var ringSr = ring.AddComponent<SpriteRenderer>();
                    if (ringSr != null) { ringSr.sprite = sr.sprite; ringSr.color = new Color(1f, 1f, 1f, 0.5f); ringSr.sortingOrder = 1; }
                    ring.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
                }
                var col = tile.AddComponent<BoxCollider>(); col.size = new Vector3(1, 1, 0.1f) * _tileSize;
                tile.AddComponent<TileClickHandler>().Init(x, y, this);
            }
            SetupCamera();
        }

        private void SetupCamera() { var cam = Camera.main; if (cam != null) { cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = Mathf.Max(_board.Width, _board.Height) * _tileSize / 2f + 1.5f; cam.backgroundColor = DarkBG; } }

        public void OnPointerDown(int x, int y)
        {
            if (_solved) return;
            var c = _board.GetCell(x, y);
            if (c.Type == CellType.Source)
            {
                _activeColor = c.ColorIndex;
                HighlightMatchingTargets(c.ColorIndex);
                return;
            }
            if (c.Type == CellType.Empty) PlacePipe(x, y);
        }

        public void OnPointerEnter(int x, int y)
        {
            if (_solved || !Input.GetMouseButton(0)) return;
            var c = _board.GetCell(x, y);
            if (c.Type == CellType.FlowGate || c.Type == CellType.Source || c.Type == CellType.Target) return;
            PlacePipe(x, y);
        }

        private void HighlightMatchingTargets(int colorIndex)
        {
            ClearHighlights();
            foreach (var t in _level.Targets)
            {
                if (t.ColorIndex == colorIndex && _renderers[t.X, t.Y] != null)
                {
                    var sr = _renderers[t.X, t.Y];
                    sr.color = GetPipeColor(colorIndex) * 1.5f; // bright glow
                    _highlightedTargets.Add(sr);
                }
            }
        }

        private void ClearHighlights()
        {
            foreach (var sr in _highlightedTargets)
            {
                // Restore original target color
                for (int i = 0; i < _level.Targets.Length; i++)
                {
                    var t = _level.Targets[i];
                    if (_renderers[t.X, t.Y] == sr)
                        sr.color = t.ColorIndex == 0 ? CyanHint : MagentaHint;
                }
            }
            _highlightedTargets.Clear();
        }

        private void PlacePipe(int x, int y)
        {
            if (_highlightedTargets.Count > 0) ClearHighlights();
            if (_tutorialHint != null && _tutorialHint.activeSelf) _tutorialHint.SetActive(false);
            int color = _activeColor >= 0 ? _activeColor : 0;
            var prev = _renderers[x, y].color;
            if (_router.CanPlace(x, y, color))
            {
                _router.Place(x, y, color);
                _undoStack.Push((x, y, prev));
                _moveCount++; UpdateMoveCounter();
                _renderers[x, y].color = GetPipeColor(color);
                StartCoroutine(PopAnim(_renderers[x, y].transform));
                CheckWin();
            }
        }

        private Color GetPipeColor(int ci) => ci == 1 ? NeonMagenta : NeonCyan;

        private void CheckWin()
        {
            int c = 0;
            foreach (var s in _level.Sources) foreach (var t in _level.Targets) if (s.ColorIndex == t.ColorIndex && _router.IsPathConnected(s.X, s.Y, t.X, t.Y)) { c++; break; }
            if (c >= _level.Sources.Length) { _solved = true; StartCoroutine(WinBloom()); }
        }

        private System.Collections.IEnumerator PopAnim(Transform t) { float d = 0.12f, e = 0f; var o = t.localScale; while (e < d) { e += Time.deltaTime; t.localScale = o * (1f + Mathf.Sin(e/d*Mathf.PI)*0.3f); yield return null; } t.localScale = o; }
        private System.Collections.IEnumerator WinBloom() { float t = 0f; while (t < 0.7f) { t += Time.deltaTime; float p = t/0.7f; foreach (var s in _level.Sources) _renderers[s.X,s.Y].color = Color.Lerp(_renderers[s.X,s.Y].color, GetPipeColor(s.ColorIndex), p); foreach (var tg in _level.Targets) _renderers[tg.X,tg.Y].color = Color.Lerp(_renderers[tg.X,tg.Y].color, GetPipeColor(tg.ColorIndex)*0.7f, p); yield return null; } yield return new WaitForSeconds(0.3f); ShowWinPopup(); }

        private void CreateUI()
        {
            // Ensure EventSystem exists for UI button clicks to work
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var mc = new GameObject("MoveCounter"); mc.transform.SetParent(transform);
            var cv = mc.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 50;
            mc.AddComponent<CanvasScaler>(); mc.AddComponent<GraphicRaycaster>();
            var ct = new GameObject("CounterText"); ct.transform.SetParent(mc.transform, false);
            var tx = ct.AddComponent<Text>(); tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tx.fontSize = 18; tx.alignment = TextAnchor.UpperLeft; tx.color = NeonCyan;
            var tr = tx.GetComponent<RectTransform>(); tr.anchorMin = new Vector2(0.02f, 0.92f); tr.anchorMax = new Vector2(0.3f, 0.98f); tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            _moveCounter = mc; UpdateMoveCounter();

            var rb = new GameObject("ResetBtn"); rb.transform.SetParent(mc.transform, false);
            var rimg = rb.AddComponent<Image>(); rimg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var rbtn = rb.AddComponent<Button>(); rbtn.onClick.AddListener(ResetPuzzle);
            var rr = rb.GetComponent<RectTransform>(); rr.anchorMin = new Vector2(0.7f, 0.92f); rr.anchorMax = new Vector2(0.95f, 0.98f); rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var rt = new GameObject("ResetText"); rt.transform.SetParent(rb.transform, false);
            var rtx = rt.AddComponent<Text>(); rtx.text = "RESET"; rtx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); rtx.fontSize = 14; rtx.alignment = TextAnchor.MiddleCenter; rtx.color = Color.white;
            var rtr = rtx.GetComponent<RectTransform>(); rtr.anchorMin = Vector2.zero; rtr.anchorMax = Vector2.one; rtr.sizeDelta = Vector2.zero;

            var uh = new GameObject("UndoHint"); uh.transform.SetParent(mc.transform, false);
            var utx = uh.AddComponent<Text>(); utx.text = "R-CLICK = UNDO"; utx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); utx.fontSize = 11; utx.alignment = TextAnchor.UpperLeft; utx.color = new Color(0.3f, 0.3f, 0.4f);
            var ur = utx.GetComponent<RectTransform>(); ur.anchorMin = new Vector2(0.02f, 0.88f); ur.anchorMax = new Vector2(0.3f, 0.92f); ur.offsetMin = Vector2.zero; ur.offsetMax = Vector2.zero;

            var ll = new GameObject("LevelLabel"); ll.transform.SetParent(mc.transform, false);
            var ltx = ll.AddComponent<Text>(); ltx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); ltx.fontSize = 17; ltx.alignment = TextAnchor.UpperCenter; ltx.color = new Color(0.4f, 0.4f, 0.5f);
            var llr = ltx.GetComponent<RectTransform>(); llr.anchorMin = new Vector2(0.35f, 0.92f); llr.anchorMax = new Vector2(0.65f, 0.98f); llr.offsetMin = Vector2.zero; llr.offsetMax = Vector2.zero;
            _levelLabel = ll; UpdateLevelLabel();

            // Tutorial hint (Level 1 only)
            var th = new GameObject("TutorialHint"); th.transform.SetParent(mc.transform, false);
            var thtx = th.AddComponent<Text>();
            thtx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            thtx.fontSize = 14; thtx.alignment = TextAnchor.LowerCenter; thtx.color = new Color(0.5f, 0.7f, 0.8f, 0.9f);
            var thr = thtx.GetComponent<RectTransform>();
            thr.anchorMin = new Vector2(0.1f, 0.05f); thr.anchorMax = new Vector2(0.9f, 0.18f);
            thr.offsetMin = Vector2.zero; thr.offsetMax = Vector2.zero;
            _tutorialHint = th;
            if (_levelNumber == 1 && _level.Sources.Length > 0)
            {
                var src = _level.Sources[0];
                var tgt = System.Array.Find(_level.Targets, t => t.ColorIndex == src.ColorIndex);
                string colorName = src.ColorIndex == 0 ? "CYAN" : "MAGENTA";
                thtx.text = $"Tap the {colorName} source ▶ then drag a path to the matching {colorName} target ◼";
            }
            else _tutorialHint.SetActive(false);

            var wc = new GameObject("WinCanvas"); wc.transform.SetParent(transform);
            var wcv = wc.AddComponent<Canvas>(); wcv.renderMode = RenderMode.ScreenSpaceOverlay; wcv.sortingOrder = 100; wc.AddComponent<CanvasScaler>(); wc.AddComponent<GraphicRaycaster>();
            var bg = new GameObject("WinBG"); bg.transform.SetParent(wc.transform, false);
            var bi = bg.AddComponent<Image>(); bi.color = new Color(0,0,0,0f); bi.raycastTarget = false;
            var br = bg.GetComponent<RectTransform>(); br.anchorMin=Vector2.zero; br.anchorMax=Vector2.one; br.sizeDelta=Vector2.zero;
            var t1 = new GameObject("WinMain"); t1.transform.SetParent(bg.transform, false);
            var wtx = t1.AddComponent<Text>(); wtx.text="NETWORK ONLINE"; wtx.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); wtx.fontSize=34; wtx.alignment=TextAnchor.MiddleCenter; wtx.color=NeonCyan;
            var wr = wtx.GetComponent<RectTransform>(); wr.anchorMin=new Vector2(0.5f,0.65f); wr.anchorMax=new Vector2(0.5f,0.65f); wr.sizeDelta=new Vector2(500,70);
            var t2 = new GameObject("WinSub"); t2.transform.SetParent(bg.transform, false);
            var wsx = t2.AddComponent<Text>(); wsx.text="All pipelines connected."; wsx.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); wsx.fontSize=15; wsx.alignment=TextAnchor.MiddleCenter; wsx.color=new Color(0.5f,0.5f,0.6f);
            var wr2 = wsx.GetComponent<RectTransform>(); wr2.anchorMin=new Vector2(0.5f,0.55f); wr2.anchorMax=new Vector2(0.5f,0.55f); wr2.sizeDelta=new Vector2(400,35);
            var t3 = new GameObject("WinScore"); t3.transform.SetParent(bg.transform, false);
            var scx = t3.AddComponent<Text>(); scx.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); scx.fontSize=13; scx.alignment=TextAnchor.MiddleCenter; scx.color=new Color(0.3f,0.3f,0.4f);
            var wr3 = scx.GetComponent<RectTransform>(); wr3.anchorMin=new Vector2(0.5f,0.48f); wr3.anchorMax=new Vector2(0.5f,0.48f); wr3.sizeDelta=new Vector2(400,25);

            var pa = new GameObject("PlayAgainBtn"); pa.transform.SetParent(bg.transform, false);
            var paImg = pa.AddComponent<Image>(); paImg.color = new Color(0.1f, 0.4f, 0.45f, 0.9f);
            var paBtn = pa.AddComponent<Button>(); paBtn.onClick.AddListener(ResetPuzzle);
            var paRect = pa.GetComponent<RectTransform>(); paRect.anchorMin=new Vector2(0.5f,0.32f); paRect.anchorMax=new Vector2(0.5f,0.32f); paRect.sizeDelta=new Vector2(160,36); paRect.anchoredPosition=Vector2.zero;
            var paText = new GameObject("PlayAgainText"); paText.transform.SetParent(pa.transform, false);
            var paTx = paText.AddComponent<Text>(); paTx.text="PLAY AGAIN"; paTx.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); paTx.fontSize=14; paTx.alignment=TextAnchor.MiddleCenter; paTx.color=Color.white;
            var paTr = paTx.GetComponent<RectTransform>(); paTr.anchorMin=Vector2.zero; paTr.anchorMax=Vector2.one; paTr.sizeDelta=Vector2.zero;

            var nl = new GameObject("NextLevelBtn"); nl.transform.SetParent(bg.transform, false);
            var nlImg = nl.AddComponent<Image>(); nlImg.color = new Color(0.35f, 0.15f, 0.45f, 0.9f);
            var nlBtn = nl.AddComponent<Button>(); nlBtn.onClick.AddListener(AdvanceLevel);
            var nlRect = nl.GetComponent<RectTransform>(); nlRect.anchorMin=new Vector2(0.5f,0.22f); nlRect.anchorMax=new Vector2(0.5f,0.22f); nlRect.sizeDelta=new Vector2(160,36); nlRect.anchoredPosition=Vector2.zero;
            var nlText = new GameObject("NextLevelText"); nlText.transform.SetParent(nl.transform, false);
            var nlTx = nlText.AddComponent<Text>(); nlTx.text="NEXT LEVEL ▶"; nlTx.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); nlTx.fontSize=14; nlTx.alignment=TextAnchor.MiddleCenter; nlTx.color=Color.white;
            var nlTr = nlTx.GetComponent<RectTransform>(); nlTr.anchorMin=Vector2.zero; nlTr.anchorMax=Vector2.one; nlTr.sizeDelta=Vector2.zero;

            _winPopup = wc; _winPopup.SetActive(false);
        }

        private void UpdateMoveCounter()
        {
            if (_moveCounter == null) return;
            var tx = _moveCounter.GetComponentInChildren<Text>();
            if (tx != null) tx.text = $"MOVES: {_moveCount}";
        }

        private void ShowWinPopup()
        {
            // Build a descriptive message: "CYAN + MAGENTA pipelines online"
            var names = new List<string>();
            foreach (var s in _level.Sources)
            {
                string colorName = s.ColorIndex switch { 0 => "CYAN", 1 => "MAGENTA", _ => $"COLOR {s.ColorIndex}" };
                if (!names.Contains(colorName)) names.Add(colorName);
            }
            string pipeList = string.Join(" + ", names);

            var ts = _winPopup.GetComponentsInChildren<Text>();
            foreach (var t in ts)
            {
                if (t.name == "WinMain")
                    t.text = $"PIPELINE{(names.Count > 1 ? "S" : "")} ONLINE";
                if (t.name == "WinSub")
                    t.text = $"{pipeList} connection{(names.Count > 1 ? "s" : "")} established.";
                if (t.name == "WinScore")
                    t.text = $"Completed in {_moveCount} moves";
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
                var c = bg.color;
                c.a = Mathf.Lerp(0f, 0.8f, e / d);
                bg.color = c;
                yield return null;
            }
        }

        private void Update()
        {
            if (_solved) return;
            if (Input.GetMouseButtonDown(1)) UndoLast();
        }

        private void UndoLast()
        {
            if (_undoStack.Count == 0) return;
            _router.Undo();
            var (x, y, c) = _undoStack.Pop();
            if (_renderers[x, y] != null)
                _renderers[x, y].color = c;
            _moveCount = Mathf.Max(0, _moveCount - 1);
            UpdateMoveCounter();
        }

        private void ResetPuzzle()
        {
            _solved = false;
            _moveCount = 0;
            _activeColor = -1;
            _undoStack.Clear();
            ClearHighlights();

            if (_winPopup != null)
                _winPopup.SetActive(false);

            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _router = new PipeRouter(_board);

            for (int x = 0; x < _board.Width; x++)
            {
                for (int y = 0; y < _board.Height; y++)
                {
                    var sr = _renderers[x, y];
                    if (sr == null) continue;

                    var cell = _board.GetCell(x, y);
                    sr.color = cell.Type switch
                    {
                        CellType.Source when cell.ColorIndex == 0 => CyanHint,
                        CellType.Source when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Target when cell.ColorIndex == 0 => CyanHint,
                        CellType.Target when cell.ColorIndex == 1 => MagentaHint,
                        CellType.Obstacle => ObstacleCol,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Up => FlowGateUp,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Down => FlowGateDown,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Right => FlowGateRight,
                        CellType.FlowGate when cell.FlowDirection == PipeDirection.Left => FlowGateLeft,
                        _ => DarkTile
                    };
                }
            }

            UpdateMoveCounter();
        }

        private void AdvanceLevel()
        {
            if (_levelNumber >= _maxLevel) { _levelNumber = 1; }
            else _levelNumber++;
            LoadLevel(_levelNumber);
        }

        private void LoadLevel(int levelNum)
        {
            // Destroy old tiles
            for (int x = 0; x < _board.Width; x++)
                for (int y = 0; y < _board.Height; y++)
                    if (_renderers[x, y] != null)
                        Destroy(_renderers[x, y].gameObject);

            _solved = false;
            _moveCount = 0;
            _activeColor = -1;
            _undoStack.Clear();
            ClearHighlights();
            if (_winPopup != null) _winPopup.SetActive(false);

            _levelNumber = levelNum;
            _level = _levelRepo.GetLevel(_levelNumber);
            _board = new GridBoard(_level);
            _router = new PipeRouter(_board);
            BuildGrid();
            UpdateMoveCounter();
            UpdateLevelLabel();
        }

        private void UpdateLevelLabel()
        {
            if (_levelLabel == null) return;
            var tx = _levelLabel.GetComponent<Text>();
            if (tx != null) tx.text = $"LEVEL {_levelNumber}/{_maxLevel}";
        }
    }

    public class TileClickHandler : MonoBehaviour { private int _x,_y; private PuzzleBoardView _board; public void Init(int x,int y,PuzzleBoardView b){_x=x;_y=y;_board=b;} private void OnMouseDown(){_board.OnPointerDown(_x,_y);} private void OnMouseEnter(){_board.OnPointerEnter(_x,_y);} }
}
