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
        private PipeInventory _inventory;
        private List<GameObject> _inventorySlots = new();
        private int _selectedPieceIndex = -1;
        private CanvasGroup _canvasGroup;

        private static readonly Color BodyDefault = new(0.06f, 0.08f, 0.12f, 0.92f);
        private static readonly Color BodySelected = new(0.12f, 0.10f, 0.20f, 0.95f);
        private static readonly Color DepletedBorder = new(0.25f, 0.28f, 0.32f);
        private static readonly Color LabelColor = new(0.92f, 0.95f, 1f);

        public int SelectedPieceIndex => _selectedPieceIndex;
        public event Action<PieceShape> OnPieceSelected;

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
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            bgImg.raycastTarget = false;
            var bgr = bg.GetComponent<RectTransform>();
            bgr.anchorMin = new Vector2(0f, 0f);
            bgr.anchorMax = new Vector2(1f, 0.12f);
            bgr.offsetMin = Vector2.zero;
            bgr.offsetMax = Vector2.zero;
        }

        public void Bind(PipeInventory inventory)
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
                sr.anchorMin = new Vector2(startX + idx * slotWidth, 0.02f);
                sr.anchorMax = new Vector2(startX + (idx + 1) * slotWidth - 0.01f, 0.10f);
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;

                // --- Body: dark translucent fill inset by 3px to reveal border ---
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(slot.transform, false);
                var bodyImg = bodyGo.AddComponent<Image>();
                bodyImg.color = BodyDefault;
                bodyImg.raycastTarget = false;
                var bodyRt = bodyGo.GetComponent<RectTransform>();
                bodyRt.anchorMin = Vector2.zero;
                bodyRt.anchorMax = Vector2.one;
                bodyRt.offsetMin = new Vector2(3f, 3f);
                bodyRt.offsetMax = new Vector2(-3f, -3f);

                // --- Shape label (bold, large, centered, near-white) ---
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(slot.transform, false);
                var labelTx = labelGo.AddComponent<TextMeshProUGUI>();
                labelTx.text = ShapeSymbol(shape);
                labelTx.fontSize = 20;
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
                badgeTx.fontSize = 18;
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
                if (piece.State == PieceState.InHand)
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

        private void SelectInventoryPiece(PieceShape shape)
        {
            if (_inventory == null) return;

            for (int i = 0; i < _inventory.Pieces.Count; i++)
            {
                if (_inventory.Pieces[i].State == PieceState.InHand &&
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

        private static Color GetShapeColor(PieceShape shape) => shape switch
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

        public static string ShapeSymbol(PieceShape shape) => shape switch
        {
            PieceShape.Straight => "STR",
            PieceShape.Elbow => "ELB",
            PieceShape.TJunction => "TEE",
            PieceShape.Cross => "CRS",
            PieceShape.Valve => "VLV",
            PieceShape.Amplifier => "AMP",
            PieceShape.Mixer => "MIX",
            PieceShape.Blocker => "BLK",
            _ => "?"
        };
    }
}
