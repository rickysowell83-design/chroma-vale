using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaVale.Presentation.Views
{
    public class PuzzleBoardView : MonoBehaviour
    {
        [SerializeField] private GameObject _pipeTilePrefab;
        [SerializeField] private GameObject _sourceTilePrefab;
        [SerializeField] private GameObject _targetTilePrefab;
        [SerializeField] private float _tileSize = 1f;

        private GridBoard _board;
        private PipeRouter _router;
        private SpriteRenderer[,] _renderers;
        private GameObject _winPopup;

        private void Start()
        {
            var repo = new LevelRepository();
            var level = repo.GetLevel(1);
            _board = new GridBoard(level);
            _router = new PipeRouter(_board);

            BuildGrid();
            CreateWinPopup();
            Debug.Log($"[PuzzleBoardView] Ready. Click on gray tiles to place pipes. Connect source → target to win!");
        }

        private void CreateWinPopup()
        {
            // Create a canvas
            var canvasObj = new GameObject("WinCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Semi-transparent dark background
            var bg = new GameObject("WinBackground");
            bg.transform.SetParent(canvasObj.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // "Puzzle Solved!" text
            var textObj = new GameObject("WinText");
            textObj.transform.SetParent(bg.transform, false);
            var text = textObj.AddComponent<Text>();
            text.text = "🎉 Puzzle Solved!";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(400, 100);
            textRect.anchoredPosition = Vector2.zero;

            // Subtitle
            var subObj = new GameObject("WinSubtitle");
            subObj.transform.SetParent(bg.transform, false);
            var subText = subObj.AddComponent<Text>();
            subText.text = "Source connected!";
            subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subText.fontSize = 24;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.8f, 0.8f, 0.8f);
            var subRect = subText.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.4f);
            subRect.anchorMax = new Vector2(0.5f, 0.4f);
            subRect.sizeDelta = new Vector2(400, 50);
            subRect.anchoredPosition = Vector2.zero;

            _winPopup = canvasObj;
            _winPopup.SetActive(false);
        }

        private void ShowWinPopup()
        {
            _winPopup.SetActive(true);
            // Simple scale-up animation
            var winText = _winPopup.GetComponentInChildren<Text>();
            if (winText != null)
            {
                winText.transform.localScale = Vector3.zero;
                StartCoroutine(AnimateWin(winText.transform));
            }
        }

        private System.Collections.IEnumerator AnimateWin(Transform target)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3f;
                target.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private void BuildGrid()
        {
            _renderers = new SpriteRenderer[_board.Width, _board.Height];
            var offset = new Vector3(-_board.Width * _tileSize / 2f, -_board.Height * _tileSize / 2f, 0);

            for (int x = 0; x < _board.Width; x++)
            for (int y = 0; y < _board.Height; y++)
            {
                var cell = _board.GetCell(x, y);
                GameObject prefab = cell.Type switch
                {
                    CellType.Source => _sourceTilePrefab,
                    CellType.Target => _targetTilePrefab,
                    _ => _pipeTilePrefab
                };
                if (prefab == null) continue;

                var tile = Instantiate(prefab,
                    new Vector3(x * _tileSize + offset.x, y * _tileSize + offset.y, 0),
                    Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{y}";

                var sr = tile.GetComponent<SpriteRenderer>();
                _renderers[x, y] = sr;
                if (sr != null && cell.Type == CellType.Source)
                    sr.color = GetColorForIndex(cell.ColorIndex);

                var col = tile.AddComponent<BoxCollider>();
                col.size = new Vector3(1, 1, 0.1f) * _tileSize;

                var tileData = tile.AddComponent<TileClickHandler>();
                tileData.Init(x, y, this);
            }

            SetupCamera();
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 0, -10);
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(_board.Width, _board.Height) * _tileSize / 2f + 1f;
            }
        }

        public void OnTileClicked(int x, int y)
        {
            if (_winPopup.activeSelf) return; // Already solved
            var cell = _board.GetCell(x, y);
            if (cell.Type != CellType.Empty) return;

            if (_router.CanPlace(x, y, 0))
            {
                _router.Place(x, y, 0);
                _renderers[x, y].color = new Color(0.86f, 0.33f, 0.33f);

                var level = new LevelRepository().GetLevel(1);
                if (_router.IsPathConnected(level.Sources[0].X, level.Sources[0].Y,
                        level.Targets[0].X, level.Targets[0].Y))
                {
                    ShowWinPopup();
                }
            }
        }

        private Color GetColorForIndex(int index) => index switch
        {
            0 => new Color(0.86f, 0.33f, 0.33f),
            1 => new Color(0.33f, 0.53f, 0.86f),
            2 => new Color(0.33f, 0.86f, 0.33f),
            3 => new Color(0.86f, 0.73f, 0.33f),
            4 => new Color(0.73f, 0.33f, 0.86f),
            _ => Color.gray
        };
    }

    public class TileClickHandler : MonoBehaviour
    {
        private int _x, _y;
        private PuzzleBoardView _board;

        public void Init(int x, int y, PuzzleBoardView board)
        {
            _x = x;
            _y = y;
            _board = board;
        }

        private void OnMouseDown()
        {
            _board.OnTileClicked(_x, _y);
        }
    }
}