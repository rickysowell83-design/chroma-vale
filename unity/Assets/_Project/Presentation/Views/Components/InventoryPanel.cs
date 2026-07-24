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

            // Background strip
            var bg = new GameObject("InvBG");
            bg.transform.SetParent(transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
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

                var slot = new GameObject("Slot_" + shape);
                slot.transform.SetParent(transform, false);

                var img = slot.AddComponent<Image>();
                img.color = GetShapeColor(shape);
                var sr = slot.GetComponent<RectTransform>();
                sr.anchorMin = new Vector2(startX + idx * slotWidth, 0.02f);
                sr.anchorMax = new Vector2(startX + (idx + 1) * slotWidth - 0.01f, 0.10f);
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;

                // Click handler
                var btn = slot.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectInventoryPiece(shape));

                // Label
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(slot.transform, false);
                var labelTx = labelGo.AddComponent<TextMeshProUGUI>();
                labelTx.text = ShapeSymbol(shape) + " x" + count;
                labelTx.fontSize = 11;
                labelTx.alignment = TextAlignmentOptions.Center;
                labelTx.color = Color.white;
                var lr = labelTx.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.sizeDelta = Vector2.zero;

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

        public void ClearSelection()
        {
            _selectedPieceIndex = -1;
            ApplyHighlight(null);
        }

        public void SetLocked(bool locked)
        {
            foreach (var slot in _inventorySlots)
            {
                if (slot != null)
                {
                    var btn = slot.GetComponent<Button>();
                    if (btn != null) btn.interactable = !locked;
                }
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
            PieceShape.Straight => "\u2500", // ─
            PieceShape.Elbow => "\u2514",    // └
            PieceShape.TJunction => "\u251C",// ├
            PieceShape.Cross => "\u253C",    // ┼
            PieceShape.Valve => "\u25C7",    // ◇
            PieceShape.Amplifier => "\u25B2",// ▲
            PieceShape.Mixer => "\u2715",    // ✕
            PieceShape.Blocker => "\u25A0",  // ■
            _ => "?"
        };
    }
}
