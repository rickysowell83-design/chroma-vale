using System;
using System.Collections;
using System.Collections.Generic;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class InventoryPanel : MonoBehaviour
    {
        private TraceInventory _inventory;
        private List<GameObject> _inventorySlots = new();
        private int _selectedPieceIndex = -1;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _headerLabel;
        private TextMeshProUGUI _rotationLabel;

        // ---- v2 Palette ----
        private static readonly Color TrayBg = new(0.051f, 0.067f, 0.090f, 0.85f);
        private static readonly Color TrayBorder = new(0f, 0.898f, 1f, 0.20f);
        private static readonly Color TrayCornerAccent = new(0f, 0.898f, 1f, 0.40f);
        private static readonly Color SlotBg = new(0.039f, 0.086f, 0.039f);
        private static readonly Color SlotBorder = new(0.102f, 0.180f, 0.102f);
        private static readonly Color SlotBorderSelected = new(0f, 0.898f, 1f);
        private static readonly Color SlotBgSelected = new(0.08f, 0.12f, 0.08f);
        private static readonly Color DepletedBorder = new(0.25f, 0.28f, 0.32f);
        private static readonly Color CopperOxidizedCol = new(0.361f, 0.227f, 0.118f);
        private static readonly Color EnigGoldCol = new(0.831f, 0.659f, 0.263f);
        private static readonly Color LabelTypeColor = new(0f, 0.898f, 1f);
        private static readonly Color LabelCountColor = new(1f, 1f, 1f);
        private static readonly Color HeaderColor = new(0.35f, 0.55f, 0.65f, 0.7f);

        private const float TRAY_HEIGHT = 0.15f;
        private const float SLOT_TOP_MARGIN = 0.22f;
        private const float SLOT_BOTTOM_MARGIN = 0.08f;

        public int SelectedPieceIndex => _selectedPieceIndex;
        public int PendingRotation { get; private set; } = 0;
        public event Action<SegmentShape> OnPieceSelected;

        private void Awake()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // ---- Background tray: BOTTOM-DOCKED at 15% ----
            var bg = new GameObject("InvBG");
            bg.transform.SetParent(transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = TrayBg;
            bgImg.raycastTarget = false;
            var bgr = bg.GetComponent<RectTransform>();
            bgr.anchorMin = new Vector2(0f, 0f);
            bgr.anchorMax = new Vector2(1f, TRAY_HEIGHT);
            bgr.offsetMin = Vector2.zero;
            bgr.offsetMax = Vector2.zero;

            // ---- Top border line ----
            var topLine = new GameObject("InvTopBorder");
            topLine.transform.SetParent(bg.transform, false);
            var topLineImg = topLine.AddComponent<Image>();
            topLineImg.color = TrayBorder;
            topLineImg.raycastTarget = false;
            var topLineRt = topLine.GetComponent<RectTransform>();
            topLineRt.anchorMin = new Vector2(0f, 0.88f);
            topLineRt.anchorMax = new Vector2(1f, 1f);
            topLineRt.offsetMin = Vector2.zero;
            topLineRt.offsetMax = Vector2.zero;

            // ---- Corner brackets ----
            CreateCornerBrackets(bg.transform);

            // ---- Header ----
            var headerGo = new GameObject("InventoryHeader");
            headerGo.transform.SetParent(bg.transform, false);
            _headerLabel = headerGo.AddComponent<TextMeshProUGUI>();
            _headerLabel.text = "COMPONENTS";
            _headerLabel.fontSize = 11;
            _headerLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            _headerLabel.alignment = TextAlignmentOptions.Left;
            _headerLabel.color = HeaderColor;
            var headerRt = _headerLabel.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.03f, 0.70f);
            headerRt.anchorMax = new Vector2(0.45f, 0.90f);
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;

            // ---- Rotation indicator ----
            var rotGo = new GameObject("RotationLabel");
            rotGo.transform.SetParent(bg.transform, false);
            _rotationLabel = rotGo.AddComponent<TextMeshProUGUI>();
            _rotationLabel.text = "";
            _rotationLabel.fontSize = 10;
            _rotationLabel.fontStyle = FontStyles.Bold;
            _rotationLabel.alignment = TextAlignmentOptions.Right;
            _rotationLabel.color = ChromaPalette.NeonMagenta;
            var rotRt = _rotationLabel.GetComponent<RectTransform>();
            rotRt.anchorMin = new Vector2(0.50f, 0.70f);
            rotRt.anchorMax = new Vector2(0.97f, 0.90f);
            rotRt.offsetMin = Vector2.zero;
            rotRt.offsetMax = Vector2.zero;
            _rotationLabel.gameObject.SetActive(false);
        }

        private void CreateCornerBrackets(Transform parent)
        {
            CreateBracket(parent, new Vector2(0.01f, 0.90f), true);
            CreateBracket(parent, new Vector2(0.99f, 0.90f), false);
        }

        private void CreateBracket(Transform parent, Vector2 anchorPos, bool isLeft)
        {
            var bracket = new GameObject(isLeft ? "BracketTL" : "BracketTR");
            bracket.transform.SetParent(parent, false);
            var bracketRt = bracket.AddComponent<RectTransform>();
            bracketRt.anchorMin = anchorPos;
            bracketRt.anchorMax = anchorPos;
            bracketRt.sizeDelta = new Vector2(6f, 6f);
            bracketRt.pivot = new Vector2(isLeft ? 0f : 1f, 1f);
            bracketRt.anchoredPosition = Vector2.zero;

            var hArm = new GameObject("HArm");
            hArm.transform.SetParent(bracket.transform, false);
            var hImg = hArm.AddComponent<Image>();
            hImg.color = TrayCornerAccent;
            hImg.raycastTarget = false;
            var hRt = hArm.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0.8f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.offsetMin = Vector2.zero;
            hRt.offsetMax = Vector2.zero;

            var vArm = new GameObject("VArm");
            vArm.transform.SetParent(bracket.transform, false);
            var vImg = vArm.AddComponent<Image>();
            vImg.color = TrayCornerAccent;
            vImg.raycastTarget = false;
            var vRt = vArm.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(isLeft ? 0f : 0.7f, 0f);
            vRt.anchorMax = new Vector2(isLeft ? 0.3f : 1f, 0.8f);
            vRt.offsetMin = Vector2.zero;
            vRt.offsetMax = Vector2.zero;
        }

        public void Bind(TraceInventory inventory)
        {
            _inventory = inventory;
            Refresh();
        }

        public void Refresh()
        {
            ClearSlots();
            if (_inventory == null) return;

            var available = _inventory.GetAvailableCounts();
            if (available.Count == 0) return;

            int count = available.Count;
            float slotAreaWidth = 0.52f;
            float slotWidth = slotAreaWidth / Mathf.Max(count, 1);
            float startX = 0.03f;
            int idx = 0;

            foreach (var kvp in available)
            {
                var shape = kvp.Key;
                int pieceCount = kvp.Value;
                bool depleted = pieceCount <= 0;

                var slot = new GameObject("Slot_" + shape);
                slot.transform.SetParent(transform, false);

                var bgImg = slot.AddComponent<Image>();
                bgImg.color = depleted ? DepletedBorder : SlotBorder;
                bgImg.raycastTarget = false;

                var btn = slot.AddComponent<Button>();
                btn.interactable = !depleted;
                btn.transition = Selectable.Transition.None;

                var sr = slot.GetComponent<RectTransform>();
                float slotLeft = startX + idx * slotWidth;
                sr.anchorMin = new Vector2(slotLeft, SLOT_BOTTOM_MARGIN);
                sr.anchorMax = new Vector2(slotLeft + slotWidth * 0.9f, 1f - SLOT_TOP_MARGIN);
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;

                // ---- Inner body: FR-4 green ----
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(slot.transform, false);
                var bodyImg = bodyGo.AddComponent<Image>();
                bodyImg.color = depleted ? new Color(0.04f, 0.06f, 0.04f, 0.5f) : SlotBg;
                bodyImg.raycastTarget = false;
                var bodyRt = bodyGo.GetComponent<RectTransform>();
                bodyRt.anchorMin = Vector2.zero;
                bodyRt.anchorMax = Vector2.one;
                bodyRt.offsetMin = new Vector2(2f, 2f);
                bodyRt.offsetMax = new Vector2(-2f, -2f);

                // ---- Procedural trace thumbnail ----
                var thumbGo = new GameObject("Thumb");
                thumbGo.transform.SetParent(slot.transform, false);
                var thumbImg = thumbGo.AddComponent<RawImage>();
                thumbImg.texture = GenerateTraceThumbnail(shape, 96, 72);
                thumbImg.raycastTarget = false;
                var thumbRt = thumbGo.GetComponent<RectTransform>();
                thumbRt.anchorMin = new Vector2(0.1f, 0.25f);
                thumbRt.anchorMax = new Vector2(0.9f, 0.85f);
                thumbRt.offsetMin = Vector2.zero;
                thumbRt.offsetMax = Vector2.zero;

                // ---- Count badge (now fills more space without type label) ----
                var badgeGo = new GameObject("Badge");
                badgeGo.transform.SetParent(slot.transform, false);
                var badgeTx = badgeGo.AddComponent<TextMeshProUGUI>();
                badgeTx.text = "x" + pieceCount;
                badgeTx.fontSize = 12;
                badgeTx.enableAutoSizing = false;
                badgeTx.fontStyle = FontStyles.Bold;
                badgeTx.alignment = TextAlignmentOptions.Center;
                badgeTx.color = depleted ? new Color(0.3f, 0.3f, 0.3f, 0.5f) : LabelCountColor;
                var badgeRt = badgeTx.GetComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(0f, 0f);
                badgeRt.anchorMax = new Vector2(1f, 0.30f);
                badgeRt.offsetMin = Vector2.zero;
                badgeRt.offsetMax = Vector2.zero;

                btn.onClick.AddListener(() => SelectInventoryPiece(shape));
                _inventorySlots.Add(slot);
                idx++;
            }

            if (_selectedPieceIndex >= 0 && _inventory != null)
            {
                var piece = _inventory.Pieces[_selectedPieceIndex];
                if (piece.State == SegmentState.InHand)
                {
                    var slot = _inventorySlots.Find(s => s != null && s.name == "Slot_" + piece.Shape);
                    ApplyHighlight(slot);
                }
                else
                {
                    _selectedPieceIndex = -1;
                }
            }
        }

        // ================================================================
        // PROCEDURAL TRACE THUMBNAILS
        // ================================================================

        private static Texture2D GenerateTraceThumbnail(SegmentShape shape, int texW, int texH)
        {
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[texW * texH];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = SlotBg;
            DrawCopperTrace(pixels, texW, texH, shape);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void DrawCopperTrace(Color[] pixels, int w, int h, SegmentShape shape)
        {
            switch (shape)
            {
                case SegmentShape.Straight:
                    DrawLine(pixels, w, h, w * 0.10f, h * 0.50f, w * 0.90f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawCircle(pixels, w, h, w * 0.10f, h * 0.50f, 4, EnigGoldCol);
                    DrawCircle(pixels, w, h, w * 0.90f, h * 0.50f, 4, EnigGoldCol);
                    break;
                case SegmentShape.Corner:
                    DrawLine(pixels, w, h, w * 0.50f, h * 0.75f, w * 0.50f, h * 0.25f, CopperOxidizedCol, 3);
                    DrawLine(pixels, w, h, w * 0.50f, h * 0.75f, w * 0.85f, h * 0.75f, CopperOxidizedCol, 3);
                    DrawCircle(pixels, w, h, w * 0.50f, h * 0.25f, 4, EnigGoldCol);
                    DrawCircle(pixels, w, h, w * 0.85f, h * 0.75f, 4, EnigGoldCol);
                    DrawRing(pixels, w, h, w * 0.50f, h * 0.75f, 4, 1, EnigGoldCol);
                    break;
                case SegmentShape.Splitter:
                    DrawLine(pixels, w, h, w * 0.10f, h * 0.50f, w * 0.50f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawLine(pixels, w, h, w * 0.50f, h * 0.50f, w * 0.50f, h * 0.18f, CopperOxidizedCol, 3);
                    DrawLine(pixels, w, h, w * 0.50f, h * 0.50f, w * 0.85f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawCircle(pixels, w, h, w * 0.10f, h * 0.50f, 4, EnigGoldCol);
                    DrawCircle(pixels, w, h, w * 0.50f, h * 0.18f, 4, EnigGoldCol);
                    DrawCircle(pixels, w, h, w * 0.85f, h * 0.50f, 4, EnigGoldCol);
                    DrawRing(pixels, w, h, w * 0.50f, h * 0.50f, 4, 1, EnigGoldCol);
                    break;
                case SegmentShape.CrossJunction:
                    DrawLine(pixels, w, h, w * 0.10f, h * 0.50f, w * 0.90f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawLine(pixels, w, h, w * 0.50f, h * 0.10f, w * 0.50f, h * 0.90f, CopperOxidizedCol, 3);
                    DrawRing(pixels, w, h, w * 0.50f, h * 0.50f, 5, 1, EnigGoldCol);
                    break;
                case SegmentShape.Diode:
                    DrawLine(pixels, w, h, w * 0.10f, h * 0.50f, w * 0.68f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawLine(pixels, w, h, w * 0.68f, h * 0.50f, w * 0.90f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawCircle(pixels, w, h, w * 0.10f, h * 0.50f, 3, EnigGoldCol);
                    DrawCircle(pixels, w, h, w * 0.90f, h * 0.50f, 3, EnigGoldCol);
                    DrawTriangleRight(pixels, w, h, w * 0.68f, h * 0.50f, 5, EnigGoldCol);
                    break;
                case SegmentShape.Repeater:
                    DrawLine(pixels, w, h, w * 0.10f, h * 0.50f, w * 0.90f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawCircle(pixels, w, h, w * 0.10f, h * 0.50f, 3, EnigGoldCol);
                    DrawCircle(pixels, w, h, w * 0.90f, h * 0.50f, 3, EnigGoldCol);
                    DrawLine(pixels, w, h, w * 0.50f, h * 0.38f, w * 0.50f, h * 0.62f, EnigGoldCol, 2);
                    DrawLine(pixels, w, h, w * 0.44f, h * 0.50f, w * 0.56f, h * 0.50f, EnigGoldCol, 2);
                    break;
                case SegmentShape.Combiner:
                    DrawLine(pixels, w, h, w * 0.10f, h * 0.50f, w * 0.38f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawLine(pixels, w, h, w * 0.62f, h * 0.50f, w * 0.90f, h * 0.50f, CopperOxidizedCol, 3);
                    DrawLine(pixels, w, h, w * 0.38f, h * 0.38f, w * 0.62f, h * 0.62f, CopperOxidizedCol, 2);
                    DrawLine(pixels, w, h, w * 0.38f, h * 0.62f, w * 0.62f, h * 0.38f, CopperOxidizedCol, 2);
                    DrawCircle(pixels, w, h, w * 0.10f, h * 0.50f, 3, EnigGoldCol);
                    DrawCircle(pixels, w, h, w * 0.90f, h * 0.50f, 3, EnigGoldCol);
                    break;
                case SegmentShape.Breaker:
                    for (int px = (int)(w * 0.15f); px < (int)(w * 0.85f); px++)
                        for (int py = (int)(h * 0.25f); py < (int)(h * 0.75f); py++)
                            if (px >= 0 && px < w && py >= 0 && py < h)
                                pixels[py * w + px] = CopperOxidizedCol;
                    break;
            }
        }

        // ---- Pixel helpers ----

        private static void SetPixel(Color[] pixels, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return;
            pixels[y * w + x] = BlendOver(pixels[y * w + x], c);
        }

        private static Color BlendOver(Color bg, Color fg)
        {
            float a = fg.a;
            return new Color(
                bg.r * (1f - a) + fg.r * a,
                bg.g * (1f - a) + fg.g * a,
                bg.b * (1f - a) + fg.b * a,
                Mathf.Min(1f, bg.a + fg.a)
            );
        }

        private static void DrawLine(Color[] pixels, int w, int h, float x0, float y0, float x1, float y1, Color c, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs((int)(x1 - x0)), Mathf.Abs((int)(y1 - y0)), 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int cx = (int)(x0 + (x1 - x0) * t);
                int cy = (int)(y0 + (y1 - y0) * t);
                int r = thickness / 2;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        SetPixel(pixels, w, h, cx + dx, cy + dy, c);
            }
        }

        private static void DrawCircle(Color[] pixels, int w, int h, float cx, float cy, int radius, Color c)
        {
            int r2 = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                    if (dx * dx + dy * dy <= r2)
                        SetPixel(pixels, w, h, (int)(cx + dx), (int)(cy + dy), c);
        }

        private static void DrawRing(Color[] pixels, int w, int h, float cx, float cy, int outerR, int innerR, Color c)
        {
            int o2 = outerR * outerR;
            int i2 = innerR * innerR;
            for (int dx = -outerR; dx <= outerR; dx++)
                for (int dy = -outerR; dy <= outerR; dy++)
                {
                    int d2 = dx * dx + dy * dy;
                    if (d2 <= o2 && d2 >= i2)
                        SetPixel(pixels, w, h, (int)(cx + dx), (int)(cy + dy), c);
                }
        }

        private static void DrawTriangleRight(Color[] pixels, int w, int h, float cx, float cy, int size, Color c)
        {
            int hx = (int)cx, hy = (int)cy;
            for (int dy = -size / 2; dy <= size / 2; dy++)
            {
                int rowWidth = (int)((dy + size / 2f) / (size + 1f) * size);
                for (int dx = rowWidth / 2; dx <= rowWidth; dx++)
                    SetPixel(pixels, w, h, hx + dx, hy + dy, c);
            }
        }

        // ================================================================
        // SELECTION / HIGHLIGHT
        // ================================================================

        private void SelectInventoryPiece(SegmentShape shape)
        {
            if (_inventory == null) return;
            for (int i = 0; i < _inventory.Pieces.Count; i++)
            {
                if (_inventory.Pieces[i].State == SegmentState.InHand &&
                    _inventory.Pieces[i].Shape == shape)
                {
                    _selectedPieceIndex = i;
                    var slot = _inventorySlots.Find(s => s != null && s.name == "Slot_" + shape);
                    ApplyHighlight(slot);
                    OnPieceSelected?.Invoke(shape);
                    return;
                }
            }
        }

        private void ApplyHighlight(GameObject slot)
        {
            foreach (var s in _inventorySlots)
            {
                if (s == null) continue;
                var border = s.GetComponent<Image>();
                if (border != null) border.color = SlotBorder;
                var body = s.transform.Find("Body")?.GetComponent<Image>();
                if (body != null) body.color = SlotBg;
            }
            if (slot != null)
            {
                var border = slot.GetComponent<Image>();
                if (border != null) border.color = SlotBorderSelected;
                var body = slot.transform.Find("Body")?.GetComponent<Image>();
                if (body != null) body.color = SlotBgSelected;
                slot.transform.DOScale(1.08f, 0f);  // Pop up instantly
                slot.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            }
        }

        public void ClearSelection()
        {
            _selectedPieceIndex = -1;
            ApplyHighlight(null);
        }

        public void SetPendingRotation(int degrees)
        {
            PendingRotation = degrees;
            if (_rotationLabel != null)
            {
                if (degrees > 0)
                {
                    _rotationLabel.text = $"SCROLL \u21BB {degrees}\u00B0";
                    _rotationLabel.gameObject.SetActive(true);
                }
                else
                {
                    _rotationLabel.gameObject.SetActive(false);
                }
            }
        }

        public void SelectPieceForTutorial(SegmentShape shape)
        {
            if (_inventory == null) return;
            for (int i = 0; i < _inventory.Pieces.Count; i++)
            {
                if (_inventory.Pieces[i].State == SegmentState.InHand &&
                    _inventory.Pieces[i].Shape == shape)
                {
                    _selectedPieceIndex = i;
                    var slot = _inventorySlots.Find(s => s != null && s.name == "Slot_" + shape);
                    ApplyHighlight(slot);
                    return;
                }
            }
        }

        public void SetLocked(bool locked)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = locked ? 0.45f : 1f;
                _canvasGroup.interactable = !locked;
                _canvasGroup.blocksRaycasts = !locked;
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in _inventorySlots)
                if (slot != null) Destroy(slot);
            _inventorySlots.Clear();
        }

        public static string ShapeTypeLabel(SegmentShape shape) => shape switch
        {
            SegmentShape.Straight => "STRAIGHT",
            SegmentShape.Corner => "CORNER",
            SegmentShape.Splitter => "SPLITTER",
            SegmentShape.CrossJunction => "CROSS",
            SegmentShape.Diode => "DIODE",
            SegmentShape.Repeater => "REPEATER",
            SegmentShape.Combiner => "COMBINER",
            SegmentShape.Breaker => "BREAKER",
            _ => "TRACE"
        };

        public static string ShapeSymbol(SegmentShape shape) => shape switch
        {
            SegmentShape.Straight => "STR",
            SegmentShape.Corner => "CRN",
            SegmentShape.Splitter => "TEE",
            SegmentShape.CrossJunction => "CRS",
            SegmentShape.Diode => "DIO",
            SegmentShape.Repeater => "RPT",
            SegmentShape.Combiner => "CMB",
            SegmentShape.Breaker => "BRK",
            _ => "?"
        };
    }
}
