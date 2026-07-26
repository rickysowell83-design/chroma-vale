using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class HudPanel : MonoBehaviour
    {
        private TextMeshProUGUI _moveText;
        private TextMeshProUGUI _levelText;
        private GameObject _hintRoot;
        private TextMeshProUGUI _hintText;

        public event Action OnResetRequested;

        private void Awake()
        {
            CreateTopBar();
            CreateTutorialHint();
        }

        private void CreateTopBar()
        {
            // MoveCounter canvas
            var mc = new GameObject("MoveCounter");
            mc.transform.SetParent(transform);
            var cv = mc.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 50;
            mc.AddComponent<CanvasScaler>();
            mc.AddComponent<GraphicRaycaster>();

            // Move counter text
            var ct = new GameObject("CounterText");
            ct.transform.SetParent(mc.transform, false);
            _moveText = ct.AddComponent<TextMeshProUGUI>();
            _moveText.fontSize = 16;
            _moveText.alignment = TextAlignmentOptions.TopLeft;
            _moveText.color = ChromaPalette.NeonCyan;
            var tr = _moveText.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.02f, 0.94f);
            tr.anchorMax = new Vector2(0.3f, 0.99f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            // Level label
            var ll = new GameObject("LevelLabel");
            ll.transform.SetParent(mc.transform, false);
            _levelText = ll.AddComponent<TextMeshProUGUI>();
            _levelText.fontSize = 16;
            _levelText.alignment = TextAlignmentOptions.Top;
            _levelText.color = new Color(0.4f, 0.4f, 0.5f);
            var llr = _levelText.GetComponent<RectTransform>();
            llr.anchorMin = new Vector2(0.35f, 0.94f);
            llr.anchorMax = new Vector2(0.65f, 0.99f);
            llr.offsetMin = Vector2.zero;
            llr.offsetMax = Vector2.zero;

            // Reset button
            var rb = new GameObject("ResetBtn");
            rb.transform.SetParent(mc.transform, false);
            var rimg = rb.AddComponent<Image>();
            rimg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var rbtn = rb.AddComponent<Button>();
            rbtn.onClick.AddListener(() => { if (OnResetRequested != null) OnResetRequested(); });
            var rr = rb.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.7f, 0.94f);
            rr.anchorMax = new Vector2(0.95f, 0.99f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;
            var rt = new GameObject("ResetText");
            rt.transform.SetParent(rb.transform, false);
            var rtx = rt.AddComponent<TextMeshProUGUI>();
            rtx.text = "RESET";
            rtx.fontSize = 14;
            rtx.alignment = TextAlignmentOptions.Center;
            rtx.color = Color.white;
            var rtr = rtx.GetComponent<RectTransform>();
            rtr.anchorMin = Vector2.zero;
            rtr.anchorMax = Vector2.one;
            rtr.sizeDelta = Vector2.zero;
        }

        private void CreateTutorialHint()
        {
            _hintRoot = new GameObject("TutorialHint");
            _hintRoot.transform.SetParent(transform);
            var cv = _hintRoot.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 100;
            _hintRoot.AddComponent<CanvasScaler>();
            _hintRoot.AddComponent<GraphicRaycaster>();

            // Semi-transparent backdrop
            var bg = new GameObject("HintBG");
            bg.transform.SetParent(_hintRoot.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.7f);
            bgImg.raycastTarget = false;
            var bgr = bg.GetComponent<RectTransform>();
            bgr.anchorMin = new Vector2(0f, 0.1f);
            bgr.anchorMax = new Vector2(1f, 0.28f);
            bgr.offsetMin = Vector2.zero;
            bgr.offsetMax = Vector2.zero;

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(_hintRoot.transform, false);
            _hintText = textGo.AddComponent<TextMeshProUGUI>();
            _hintText.fontSize = 22;
            _hintText.fontStyle = FontStyles.Bold;
            _hintText.alignment = TextAlignmentOptions.Center;
            _hintText.color = ChromaPalette.NeonCyan;
            _hintText.raycastTarget = false;
            var thr = _hintText.GetComponent<RectTransform>();
            thr.anchorMin = new Vector2(0.05f, 0.12f);
            thr.anchorMax = new Vector2(0.95f, 0.26f);
            thr.offsetMin = Vector2.zero;
            thr.offsetMax = Vector2.zero;

            _hintRoot.SetActive(false);
        }

        public void SetMoves(int count)
        {
            // Now shows available inventory pieces, not move count.
            // Called as SetPieceCount from PuzzleBoardView.
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

        public void ShowHint(string text)
        {
            if (_hintRoot != null)
            {
                _hintRoot.SetActive(true);
                if (_hintText != null)
                    _hintText.text = text;
            }
        }

        public void HideHint()
        {
            if (_hintRoot != null)
                _hintRoot.SetActive(false);
        }
    }
}
