using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class FlowButton : MonoBehaviour
    {
        private Button _button;
        private TextMeshProUGUI _label;

        public event Action OnFlowRequested;

        private void Awake()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            // FlowButton canvas
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // Button
            var btnGo = new GameObject("FlowBtn");
            btnGo.transform.SetParent(transform, false);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.15f, 0.5f, 0.6f, 0.95f);
            _button = btnGo.AddComponent<Button>();
            _button.onClick.AddListener(() => { if (OnFlowRequested != null) OnFlowRequested(); });
            var br = btnGo.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.3f, 0.78f);
            br.anchorMax = new Vector2(0.7f, 0.84f);
            br.offsetMin = Vector2.zero;
            br.offsetMax = Vector2.zero;

            // Label
            var txt = new GameObject("FlowText");
            txt.transform.SetParent(btnGo.transform, false);
            _label = txt.AddComponent<TextMeshProUGUI>();
            _label.text = ">> FLOW";
            _label.fontSize = 20;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;
            var ftr = _label.GetComponent<RectTransform>();
            ftr.anchorMin = Vector2.zero;
            ftr.anchorMax = Vector2.one;
            ftr.sizeDelta = Vector2.zero;

        }

        public void SetInteractable(bool on)
        {
            if (_button != null) _button.interactable = on;
        }

        public void SetLabel(string text)
        {
            if (_label != null) _label.text = text;
        }
    }
}
