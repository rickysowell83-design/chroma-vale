using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Top-bar HUD: TRACES counter (left), LEVEL indicator (center), RESET button (right).
    /// §4.3 — Dark background strip at 8% screen height with cyan accent bottom border.
    /// The text tutorial banner has been REMOVED (§4.6) — all tutorial is now visual (§5).
    /// </summary>
    public class HudPanel : MonoBehaviour
    {
        private TextMeshProUGUI _moveText;
        private TextMeshProUGUI _levelText;
        private Image _bgImage;

        public event Action OnResetRequested;

        // ── §4.3 palette ──
        private static readonly Color TopBarBg = new(0.051f, 0.067f, 0.090f); // #0D1117
        private static readonly Color TopBarAccent = new(0f, 0.898f, 1f, 0.15f); // #00E5FF at 15%
        private static readonly Color GreyText = new(0.55f, 0.58f, 0.62f); // Legible grey on dark bg
        private static readonly Color WhiteText = Color.white;

        private void Awake()
        {
            CreateTopBar();
        }

        private void CreateTopBar()
        {
            // TopBar canvas (§4.3: full width, 8% height)
            var mc = new GameObject("TopBar");
            mc.transform.SetParent(transform);
            var cv = mc.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 50;
            mc.AddComponent<CanvasScaler>();
            mc.AddComponent<GraphicRaycaster>();

            // ── Dark background strip (full width, 0% to 8% height) ──
            var bgGo = new GameObject("TopBarBG");
            bgGo.transform.SetParent(mc.transform, false);
            _bgImage = bgGo.AddComponent<Image>();
            _bgImage.color = TopBarBg;
            _bgImage.raycastTarget = false;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.92f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // ── Cyan accent line at bottom of top bar (1px, 15% opacity) ──
            var accentGo = new GameObject("TopBarAccent");
            accentGo.transform.SetParent(bgGo.transform, false);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = TopBarAccent;
            accentImg.raycastTarget = false;
            var accentRt = accentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(1f, 0.02f);
            accentRt.offsetMin = Vector2.zero;
            accentRt.offsetMax = Vector2.zero;

            // ── TRACES: N counter (left, cyan) ──
            var ct = new GameObject("CounterText");
            ct.transform.SetParent(mc.transform, false);
            _moveText = ct.AddComponent<TextMeshProUGUI>();
            _moveText.fontSize = 16;
            _moveText.fontStyle = FontStyles.Normal;
            _moveText.alignment = TextAlignmentOptions.Left;
            _moveText.color = ChromaPalette.NeonCyan;
            var tr = _moveText.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.03f, 0.93f);
            tr.anchorMax = new Vector2(0.30f, 0.99f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            // ── LEVEL N label (center, white, bold) ──
            var ll = new GameObject("LevelLabel");
            ll.transform.SetParent(mc.transform, false);
            _levelText = ll.AddComponent<TextMeshProUGUI>();
            _levelText.fontSize = 18;
            _levelText.fontStyle = FontStyles.Bold;
            _levelText.alignment = TextAlignmentOptions.Center;
            _levelText.color = WhiteText;
            var llr = _levelText.GetComponent<RectTransform>();
            llr.anchorMin = new Vector2(0.35f, 0.93f);
            llr.anchorMax = new Vector2(0.65f, 0.99f);
            llr.offsetMin = Vector2.zero;
            llr.offsetMax = Vector2.zero;

            // ── RESET button (right, grey §4.3) ──
            var rb = new GameObject("ResetBtn");
            rb.transform.SetParent(mc.transform, false);
            var rimg = rb.AddComponent<Image>();
            rimg.color = new Color(0.10f, 0.10f, 0.12f, 0.9f); // Dark button body
            rimg.raycastTarget = true;
            var rbtn = rb.AddComponent<Button>();
            rbtn.transition = Selectable.Transition.None;
            rbtn.onClick.AddListener(() => { if (OnResetRequested != null) OnResetRequested(); });
            var rr = rb.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.72f, 0.93f);
            rr.anchorMax = new Vector2(0.97f, 0.99f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;

            var rt = new GameObject("ResetText");
            rt.transform.SetParent(rb.transform, false);
            var rtx = rt.AddComponent<TextMeshProUGUI>();
            rtx.text = "RESET";
            rtx.fontSize = 14;
            rtx.fontStyle = FontStyles.Normal;
            rtx.alignment = TextAlignmentOptions.Center;
            rtx.color = ChromaPalette.NeonCyan;
            var rtr = rtx.GetComponent<RectTransform>();
            rtr.anchorMin = Vector2.zero;
            rtr.anchorMax = Vector2.one;
            rtr.sizeDelta = Vector2.zero;
        }

        public void SetMoves(int count)
        {
            if (_moveText != null)
                _moveText.text = "TRACES: " + count;
        }

        public void SetPieceCount(int total)
        {
            if (_moveText != null)
                _moveText.text = "TRACES: " + total;
        }

        public void SetLevel(int current, int max)
        {
            if (_levelText != null)
                _levelText.text = "LEVEL " + current;
        }

        /// <summary>
        /// ShowHint / HideHint are kept as no-ops for backward compatibility.
        /// The text tutorial banner has been removed (§4.6). Tutorial is now visual (§5).
        /// </summary>
        public void ShowHint(string text) { /* Text tutorial removed — tutorial is visual */ }
        public void HideHint() { /* Text tutorial removed — tutorial is visual */ }
    }
}
