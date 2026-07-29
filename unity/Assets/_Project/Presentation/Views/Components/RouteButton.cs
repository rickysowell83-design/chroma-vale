using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class RouteButton : MonoBehaviour
    {
        private Button _button;
        private TextMeshProUGUI _label;
        private RectTransform _buttonRect;
        private Image _buttonImage;
        private Image _borderImage;
        private Image _bgPanel;
        private bool _isInteractable = true;
        private Tweener _borderTween;
        private Tweener _labelTween;

        private static readonly Color BgPanelColor = new(0.051f, 0.067f, 0.090f, 0.85f);
        private static readonly Color BgPanelBorder = ChromaPalette.ButtonActiveTeal * 0.4f;
        private static readonly Color ButtonBodyDefault = ChromaPalette.ButtonInactive;
        private static readonly Color ButtonBodyHover = ChromaPalette.ButtonActiveTeal * 0.25f;
        private static readonly Color ButtonBodyPressed = new(0.15f, 0.15f, 0.15f, 0.95f);
        private static readonly Color ButtonBodyDisabled = new(0.10f, 0.10f, 0.10f, 0.50f);
        private static readonly Color BorderCyanActive = ChromaPalette.ButtonActiveTeal;
        private static readonly Color BorderCyanHover = new(0.0f, 1.0f, 1.0f, 1.0f);
        private static readonly Color BorderInactive = new(0.25f, 0.25f, 0.25f, 0.6f);
        private static readonly Color BorderDisabled = new(0.18f, 0.20f, 0.22f, 0.40f);
        private static readonly Color LabelColor = new(0.85f, 0.95f, 1.0f);
        private static readonly Color LabelColorPulse = ChromaPalette.ButtonActiveTeal;

        private const float TRAY_HEIGHT = 0.15f;

        public event Action OnFlowRequested;

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

            // ---- Background panel: right side of bottom tray ----
            var bgPanelGo = new GameObject("FlowBgPanel");
            bgPanelGo.transform.SetParent(transform, false);
            var bgPanelRect = bgPanelGo.AddComponent<RectTransform>();
            bgPanelRect.anchorMin = new Vector2(0.55f, 0f);
            bgPanelRect.anchorMax = new Vector2(1f, TRAY_HEIGHT);
            bgPanelRect.offsetMin = Vector2.zero;
            bgPanelRect.offsetMax = Vector2.zero;

            _bgPanel = bgPanelGo.AddComponent<Image>();
            _bgPanel.color = BgPanelColor;
            _bgPanel.raycastTarget = false;

            // ---- Top border line ----
            var borderLineGo = new GameObject("PanelBorder");
            borderLineGo.transform.SetParent(bgPanelGo.transform, false);
            var borderLineImg = borderLineGo.AddComponent<Image>();
            borderLineImg.color = BgPanelBorder;
            borderLineImg.raycastTarget = false;
            var borderLineRect = borderLineGo.GetComponent<RectTransform>();
            borderLineRect.anchorMin = new Vector2(0f, 0.88f);
            borderLineRect.anchorMax = new Vector2(1f, 1f);
            borderLineRect.offsetMin = Vector2.zero;
            borderLineRect.offsetMax = Vector2.zero;

            // ---- Button container ----
            var btnGo = new GameObject("FlowBtn");
            btnGo.transform.SetParent(bgPanelGo.transform, false);
            _buttonRect = btnGo.AddComponent<RectTransform>();
            _buttonRect.anchorMin = new Vector2(0.15f, 0.12f);
            _buttonRect.anchorMax = new Vector2(0.85f, 0.84f);
            _buttonRect.offsetMin = Vector2.zero;
            _buttonRect.offsetMax = Vector2.zero;

            // ---- Border ----
            var borderGo = new GameObject("BtnBorder");
            borderGo.transform.SetParent(btnGo.transform, false);
            _borderImage = borderGo.AddComponent<Image>();
            _borderImage.color = BorderInactive;
            _borderImage.raycastTarget = false;
            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            // ---- Body ----
            var bodyGo = new GameObject("BtnBody");
            bodyGo.transform.SetParent(btnGo.transform, false);
            _buttonImage = bodyGo.AddComponent<Image>();
            _buttonImage.color = ButtonBodyDefault;
            _buttonImage.raycastTarget = false;
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(3f, 3f);
            bodyRt.offsetMax = new Vector2(-3f, -3f);

            _button = btnGo.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = _buttonImage;
            _button.onClick.AddListener(() =>
            {
                if (OnFlowRequested != null && _isInteractable)
                {
                    OnFlowRequested();
                    ClickPunchAnim();
                }
            });

            var trigger = btnGo.AddComponent<EventTrigger>();
            AddTriggerEvent(trigger, EventTriggerType.PointerEnter, () => OnButtonPointerEnter());
            AddTriggerEvent(trigger, EventTriggerType.PointerExit, () => OnButtonPointerExit());
            AddTriggerEvent(trigger, EventTriggerType.PointerDown, () => OnButtonPointerDown());
            AddTriggerEvent(trigger, EventTriggerType.PointerUp, () => OnButtonPointerUp());

            // ---- Label ----
            var txtGo = new GameObject("FlowText");
            txtGo.transform.SetParent(btnGo.transform, false);
            _label = txtGo.AddComponent<TextMeshProUGUI>();
            _label.text = "> ROUTE";
            _label.fontSize = 22;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = LabelColor;
            var lr = _label.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.sizeDelta = Vector2.zero;

            StartBorderPulseGlow();
            StartLabelGlowPulse();
        }

        private void ClickPunchAnim()
        {
            if (_buttonRect == null) return;
            _buttonRect.DOPunchScale(Vector3.one * 0.08f, 0.16f, 1, 0f);
        }

        private void StartBorderPulseGlow()
        {
            if (_borderImage == null) return;
            _borderTween?.Kill();
            _borderTween = _borderImage.DOFade(0.6f, 1.0f)
                .From(1.0f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void StartLabelGlowPulse()
        {
            if (_label == null) return;
            _labelTween?.Kill();
            // Color pulse: label cycles between LabelColor and LabelColorPulse
            _labelTween = _label.DOColor(LabelColorPulse, 0.9f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            // Scale pulse on the label's transform
            _label.transform.DOScaleY(1.03f, 0.9f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void OnDestroy()
        {
            _borderTween?.Kill();
            _labelTween?.Kill();
        }

        public void SetInteractable(bool on)
        {
            _isInteractable = on;
            if (_button != null) _button.interactable = on;
            if (_buttonImage != null) _buttonImage.color = on ? ButtonBodyDefault : ButtonBodyDisabled;
            if (_borderImage != null) _borderImage.color = on ? BorderCyanActive : BorderDisabled;
            if (_label != null) _label.color = on ? LabelColor : new Color(0.3f, 0.3f, 0.35f, 0.5f);
        }

        public void SetLabel(string text)
        {
            if (_label != null) _label.text = text;
        }

        private void AddTriggerEvent(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void OnButtonPointerEnter()
        {
            if (!_isInteractable) return;
            if (_borderImage != null) _borderImage.color = BorderCyanHover;
            if (_buttonImage != null) _buttonImage.color = ButtonBodyHover;
        }

        private void OnButtonPointerExit()
        {
            if (!_isInteractable) return;
            if (_borderImage != null) _borderImage.color = BorderCyanActive;
            if (_buttonImage != null) _buttonImage.color = ButtonBodyDefault;
        }

        private void OnButtonPointerDown()
        {
            if (!_isInteractable) return;
            if (_buttonImage != null) _buttonImage.color = ButtonBodyPressed;
        }

        private void OnButtonPointerUp()
        {
            if (!_isInteractable) return;
            if (_buttonImage != null) _buttonImage.color = ButtonBodyHover;
        }

        public void SetRouting(bool routing)
        {
            if (_button != null) _button.interactable = !routing;
            if (_buttonImage != null) _buttonImage.color = routing ? new Color(0.0f, 0.25f, 0.35f, 0.95f) : ButtonBodyDefault;
            if (_borderImage != null) _borderImage.color = routing ? new Color(0.0f, 0.8f, 1.0f, 0.6f) : BorderCyanActive;
            if (_label != null)
            {
                _label.text = routing ? "ROUTING..." : "> ROUTE";
                _label.color = routing ? new Color(0.0f, 0.7f, 0.9f) : LabelColor;
            }
        }
    }
}
