using System;
using System.Collections;
using System.Collections.Generic;
using ChromaVale.Core.GameLogic;
using ChromaVale.Domain.PuzzleBoard;
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

        private static readonly Color BodyDefault = new(0.06f, 0.08f, 0.12f, 0.92f);
        private static readonly Color BodySelected = new(0.12f, 0.10f, 0.20f, 0.95f);
        private static readonly Color DepletedBorder = new(0.25f, 0.28f, 0.32f);
        private static readonly Color LabelColor = new(0.92f, 0.95f, 1f);
        private static readonly Color BgStripColor = new(0.02f, 0.02f, 0.04f, 0.94f);
        private static readonly Color BgStripTopBorder = new(0.0f, 0.4f, 0.5f, 0.3f);
        private static readonly Color HeaderColor = new(0.35f, 0.55f, 0.65f, 0.7f);

        public int SelectedPieceIndex => _selectedPieceIndex;
        public int PendingRotation { get; private set; } = 0;
        public event Action<SegmentShape> OnPieceSelected;

        private void Awake()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            // InventoryPanel canvas
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // CanvasGroup for locked-state dimming
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // Background strip
            var bg = new GameObject("InvBG");
            bg.transform.SetParent(transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = BgStripColor;
            bgImg.raycastTarget = false;
            var bgr = bg.GetComponent<RectTransform>();
            bgr.anchorMin = new Vector2(0f, 0.12f);
            bgr.anchorMax = new Vector2(1f, 0.24f);
            bgr.offsetMin = Vector2.zero;
            bgr.offsetMax = Vector2.zero;

            // Top border line (neon cyan glow)
            var topLine = new GameObject("InvTopBorder");
            topLine.transform.SetParent(bg.transform, false);
            var topLineImg = topLine.AddComponent<Image>();
            topLineImg.color = BgStripTopBorder;
            topLineImg.raycastTarget = false;
            var topLineRt = topLine.GetComponent<RectTransform>();
            topLineRt.anchorMin = new Vector2(0f, 0.92f);
            topLineRt.anchorMax = new Vector2(1f, 1f);
            topLineRt.offsetMin = Vector2.zero;
            topLineRt.offsetMax = Vector2.zero;

            // "INVENTORY" header label
            var headerGo = new GameObject("InventoryHeader");
            headerGo.transform.SetParent(bg.transform, false);
            _headerLabel = headerGo.AddComponent<TextMeshProUGUI>();
            _headerLabel.text = "INVENTORY";
            _headerLabel.fontSize = 11;
            _headerLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            _headerLabel.alignment = TextAlignmentOptions.Left;
            _headerLabel.color = HeaderColor;
            var headerRt = _headerLabel.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.03f, 0.70f);
            headerRt.anchorMax = new Vector2(0.40f, 0.90f);
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;

            // Rotation indicator (hidden until set)
            var rotGo = new GameObject("RotationLabel");
            rotGo.transform.SetParent(bg.transform, false);
            _rotationLabel = rotGo.AddComponent<TextMeshProUGUI>();
            _rotationLabel.text = "";
            _rotationLabel.fontSize = 10;
            _rotationLabel.fontStyle = FontStyles.Bold;
            _rotationLabel.alignment = TextAlignmentOptions.Right;
            _rotationLabel.color = ChromaPalette.NeonMagenta;
            var rotRt = _rotationLabel.GetComponent<RectTransform>();
            rotRt.anchorMin = new Vector2(0.55f, 0.70f);
            rotRt.anchorMax = new Vector2(0.97f, 0.90f);
            rotRt.offsetMin = Vector2.zero;
            rotRt.offsetMax = Vector2.zero;
            _rotationLabel.gameObject.SetActive(false);

            // Scanline overlay (very thin repeating horizontal lines)
            var scanGo = new GameObject("Scanlines");
            scanGo.transform.SetParent(bg.transform, false);
            var scanImg = scanGo.AddComponent<Image>();
            scanImg.color = new Color(0f, 0f, 0f, 0.04f);
            scanImg.raycastTarget = false;
            var scanRt = scanGo.GetComponent<RectTransform>();
            scanRt.anchorMin = Vector2.zero;
            scanRt.anchorMax = Vector2.one;
            scanRt.sizeDelta = Vector2.zero;
            // Set a tiling sprite for scanlines if we had one; for now use a repeated
            // raw image effect via a simple shader-like approach
            scanImg.type = Image.Type.Tiled;
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

            float totalWidth = available.Count;
            float slotWidth = 0.8f / totalWidth;
            float startX = 0.1f;
            int idx = 0;

            foreach (var kvp in available)
            {
                var shape = kvp.Key;
                int count = kvp.Value;
                bool depleted = count <= 0;

                var slot = new GameObject("Slot_" + shape);
                slot.transform.SetParent(transform, false);

                // --- Root: neon border (Image) + interactability (Button) ---
                var borderImg = slot.AddComponent<Image>();
                borderImg.color = depleted ? DepletedBorder : ChromaPalette.NeonCyan;
                borderImg.raycastTarget = false;

                var btn = slot.AddComponent<Button>();
                btn.interactable = !depleted;
                btn.transition = Selectable.Transition.None; // Full procedural styling

                var sr = slot.GetComponent<RectTransform>();
                sr.anchorMin = new Vector2(startX + idx * slotWidth, 0.14f);
                sr.anchorMax = new Vector2(startX + (idx + 1) * slotWidth - 0.01f, 0.22f);
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;

                // --- Body: dark translucent fill inset by 4px to reveal border (was 3px) ---
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(slot.transform, false);
                var bodyImg = bodyGo.AddComponent<Image>();
                bodyImg.color = BodyDefault;
                bodyImg.raycastTarget = false;
                var bodyRt = bodyGo.GetComponent<RectTransform>();
                bodyRt.anchorMin = Vector2.zero;
                bodyRt.anchorMax = Vector2.one;
                bodyRt.offsetMin = new Vector2(4f, 4f);
                bodyRt.offsetMax = new Vector2(-4f, -4f);

                // --- Shape label (bold, large, centered, near-white) ---
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(slot.transform, false);
                var labelTx = labelGo.AddComponent<TextMeshProUGUI>();
                labelTx.text = ShapeSymbol(shape);
                labelTx.fontSize = 18;
                labelTx.enableAutoSizing = false;
                labelTx.fontStyle = FontStyles.Bold;
                labelTx.color = LabelColor;
                labelTx.alignment = TextAlignmentOptions.Center;
                if (depleted)
                {
                    var c = labelTx.color;
                    labelTx.color = new Color(c.r, c.g, c.b, 0.35f);
                }
                var lr = labelTx.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero;
                lr.offsetMax = Vector2.zero;

                // --- Count badge (neon-yellow, small, top-right) ---
                var badgeGo = new GameObject("Badge");
                badgeGo.transform.SetParent(slot.transform, false);
                var badgeTx = badgeGo.AddComponent<TextMeshProUGUI>();
                badgeTx.text = count.ToString();
                badgeTx.fontSize = 16;
                badgeTx.enableAutoSizing = false;
                badgeTx.fontStyle = FontStyles.Normal;
                badgeTx.color = ChromaPalette.NeonYellow;
                badgeTx.alignment = TextAlignmentOptions.TopRight;
                if (depleted)
                {
                    var c = badgeTx.color;
                    badgeTx.color = new Color(c.r, c.g, c.b, 0.35f);
                }
                var bdr = badgeTx.GetComponent<RectTransform>();
                bdr.anchorMin = new Vector2(0.55f, 0.45f);
                bdr.anchorMax = new Vector2(0.92f, 0.92f);
                bdr.offsetMin = Vector2.zero;
                bdr.offsetMax = Vector2.zero;

                btn.onClick.AddListener(() => SelectInventoryPiece(shape));

                _inventorySlots.Add(slot);
                idx++;
            }

            // Re-apply selection highlight if one was active
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

        private void ClearSlots()
        {
            foreach (var slot in _inventorySlots)
                if (slot != null) Destroy(slot);
            _inventorySlots.Clear();
        }

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
                    if (OnPieceSelected != null) OnPieceSelected(shape);
                    return;
                }
            }
        }

        private void ApplyHighlight(GameObject slot)
        {
            // Deselect all — revert to default styling
            foreach (var s in _inventorySlots)
            {
                if (s == null) continue;
                var border = s.GetComponent<Image>();
                if (border != null)
                    border.color = ChromaPalette.NeonCyan;
                var body = s.transform.Find("Body")?.GetComponent<Image>();
                if (body != null)
                    body.color = BodyDefault;
            }

            // Select — magenta border + brighter body + pulse
            if (slot != null)
            {
                var border = slot.GetComponent<Image>();
                if (border != null) border.color = ChromaPalette.NeonMagenta;
                var body = slot.transform.Find("Body")?.GetComponent<Image>();
                if (body != null) body.color = BodySelected;
                StartCoroutine(SelectionPulse(slot.transform));
            }
        }

        public void ClearSelection()
        {
            _selectedPieceIndex = -1;
            ApplyHighlight(null);
        }

        /// <summary>
        /// Show or hide the pending rotation indicator on the inventory panel.
        /// </summary>
        /// <param name="degrees">Rotation in degrees (0 = hidden).</param>
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

        public void SetLocked(bool locked)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = locked ? 0.45f : 1f;
                _canvasGroup.interactable = !locked;
                _canvasGroup.blocksRaycasts = !locked;
            }
        }

        private IEnumerator SelectionPulse(Transform t)
        {
            if (t == null) yield break;
            float d = 0.4f;
            var orig = t.localScale;
            t.localScale = orig * 1.15f;
            yield return new WaitForSeconds(d);
            if (t != null) t.localScale = orig;
        }

        private static Color GetShapeColor(SegmentShape shape) => shape switch
        {
            SegmentShape.Straight => new Color(0.2f, 0.3f, 0.5f, 0.8f),
            SegmentShape.Corner => new Color(0.2f, 0.5f, 0.3f, 0.8f),
            SegmentShape.Splitter => new Color(0.5f, 0.3f, 0.2f, 0.8f),
            SegmentShape.CrossJunction => new Color(0.5f, 0.2f, 0.5f, 0.8f),
            SegmentShape.Diode => new Color(0.3f, 0.5f, 0.5f, 0.8f),
            SegmentShape.Repeater => new Color(0.6f, 0.5f, 0.1f, 0.8f),
            SegmentShape.Combiner => new Color(0.5f, 0.1f, 0.5f, 0.8f),
            SegmentShape.Breaker => new Color(0.5f, 0.1f, 0.1f, 0.8f),
            _ => new Color(0.3f, 0.3f, 0.4f, 0.8f)
        };

        public static string ShapeSymbol(SegmentShape shape) => shape switch
        {
            SegmentShape.Straight => "STR",
            SegmentShape.Corner => "ELB",
            SegmentShape.Splitter => "TEE",
            SegmentShape.CrossJunction => "CRS",
            SegmentShape.Diode => "VLV",
            SegmentShape.Repeater => "AMP",
            SegmentShape.Combiner => "MIX",
            SegmentShape.Breaker => "BLK",
            _ => "?"
        };
    }
}
