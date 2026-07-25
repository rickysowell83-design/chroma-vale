using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class FlowButton : MonoBehaviour
    {
        private Button _button;
        private TextMeshProUGUI _label;
        private RectTransform _buttonRect;
        private Image _buttonImage;
        private Image _borderImage;
        private Image _bgPanel;
        private bool _isInteractable = true;

        // Neon glow colors
        private static readonly Color BgPanelColor = new(0.02f, 0.02f, 0.04f, 0.85f);
        private static readonly Color BgPanelBorder = new(0.08f, 0.12f, 0.18f, 0.9f);
        private static readonly Color ButtonBodyDefault = new(0.10f, 0.12f, 0.16f, 0.95f);
        private static readonly Color ButtonBodyHover = new(0.15f, 0.18f, 0.22f, 0.95f);
        private static readonly Color ButtonBodyPressed = new(0.08f, 0.10f, 0.14f, 0.95f);
        private static readonly Color ButtonBodyDisabled = new(0.04f, 0.05f, 0.06f, 0.50f);
        private static readonly Color BorderCyanActive = new(0.0f, 0.85f, 1.0f, 1.0f);
        private static readonly Color BorderCyanHover = new(0.0f, 1.0f, 1.0f, 1.0f);
        private static readonly Color BorderCyanDim = new(0.0f, 0.4f, 0.5f, 0.35f);
        private static readonly Color BorderDisabled = new(0.18f, 0.20f, 0.22f, 0.40f);
        private static readonly Color LabelColor = new(0.85f, 0.95f, 1.0f);
        private static readonly Color LabelColorPulse = new(0.0f, 0.85f, 1.0f);

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

            // ── Background panel (dark semi-transparent strip with neon border) ──
            var bgPanelGo = new GameObject("FlowBgPanel");
            bgPanelGo.transform.SetParent(transform, false);
            var bgPanelRect = bgPanelGo.AddComponent<RectTransform>();
            bgPanelRect.anchorMin = new Vector2(0f, 0f);
            bgPanelRect.anchorMax = new Vector2(1f, 0.14f);
            bgPanelRect.offsetMin = Vector2.zero;
            bgPanelRect.offsetMax = Vector2.zero;

            // Panel background
            _bgPanel = bgPanelGo.AddComponent<Image>();
            _bgPanel.color = BgPanelColor;
            _bgPanel.raycastTarget = false;

            // Panel neon top-border line
            var borderLineGo = new GameObject("PanelBorder");
            borderLineGo.transform.SetParent(bgPanelGo.transform, false);
            var borderLineImg = borderLineGo.AddComponent<Image>();
            borderLineImg.color = BgPanelBorder;
            borderLineImg.raycastTarget = false;
            var borderLineRect = borderLineGo.GetComponent<RectTransform>();
            borderLineRect.anchorMin = new Vector2(0f, 0.94f);
            borderLineRect.anchorMax = new Vector2(1f, 1f);
            borderLineRect.offsetMin = Vector2.zero;
            borderLineRect.offsetMax = Vector2.zero;

            // ── Button container (anchored inside the dashboard area) ──
            var btnGo = new GameObject("FlowBtn");
            btnGo.transform.SetParent(bgPanelGo.transform, false);
            _buttonRect = btnGo.AddComponent<RectTransform>();
            _buttonRect.anchorMin = new Vector2(0.25f, 0.08f);
            _buttonRect.anchorMax = new Vector2(0.75f, 0.88f);
            _buttonRect.offsetMin = Vector2.zero;
            _buttonRect.offsetMax = Vector2.zero;

            // Button border (neon cyan outline, 3px thick)
            var borderGo = new GameObject("BtnBorder");
            borderGo.transform.SetParent(btnGo.transform, false);
            _borderImage = borderGo.AddComponent<Image>();
            _borderImage.color = BorderCyanActive;
            _borderImage.raycastTarget = false;
            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            // Button body (dark gunmetal, inset by 3px to reveal border)
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

            // Button component (interactivity on the body gameobject)
            _button = btnGo.AddComponent<Button>();
            _button.transition = Selectable.Transition.None; // Full procedural
            _button.targetGraphic = _buttonImage;
            _button.onClick.AddListener(() =>
            {
                if (OnFlowRequested != null && _isInteractable)
                {
                    OnFlowRequested();
                    StartCoroutine(ClickPunchAnim());
                }
            });

            // Hover/press detection for tactile feedback
            var trigger = btnGo.AddComponent<EventTrigger>();
            AddTriggerEvent(trigger, EventTriggerType.PointerEnter, () => OnButtonPointerEnter());
            AddTriggerEvent(trigger, EventTriggerType.PointerExit, () => OnButtonPointerExit());
            AddTriggerEvent(trigger, EventTriggerType.PointerDown, () => OnButtonPointerDown());
            AddTriggerEvent(trigger, EventTriggerType.PointerUp, () => OnButtonPointerUp());

            // ── Label: "▶ FLOW" with hacker-terminal feel ──
            var txtGo = new GameObject("FlowText");
            txtGo.transform.SetParent(btnGo.transform, false);
            _label = txtGo.AddComponent<TextMeshProUGUI>();
            _label.text = "> FLOW";
            _label.fontSize = 22;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = LabelColor;
            var lr = _label.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.sizeDelta = Vector2.zero;

            // Sub-label: tiny "EXECUTE" below the main label
            var subGo = new GameObject("FlowSubText");
            subGo.transform.SetParent(btnGo.transform, false);
            var subLabel = subGo.AddComponent<TextMeshProUGUI>();
            subLabel.text = "EXECUTE";
            subLabel.fontSize = 10;
            subLabel.fontStyle = FontStyles.Normal;
            subLabel.alignment = TextAlignmentOptions.Bottom;
            subLabel.color = new Color(0.4f, 0.55f, 0.6f, 0.5f);
            var subR = subLabel.GetComponent<RectTransform>();
            subR.anchorMin = Vector2.zero;
            subR.anchorMax = Vector2.one;
            subR.offsetMin = new Vector2(0f, 2f);
            subR.offsetMax = new Vector2(0f, -2f);

            // Start the pulsating border glow
            StartCoroutine(BorderPulseGlow());
            // Start the label glow pulse
            StartCoroutine(LabelGlowPulse());
        }

        private IEnumerator ClickPunchAnim()
        {
            if (_buttonRect == null) yield break;
            var orig = _buttonRect.localScale;
            _buttonRect.localScale = orig * 0.92f;
            yield return new WaitForSeconds(0.08f);
            if (_buttonRect != null)
                _buttonRect.localScale = orig;
        }

        private IEnumerator BorderPulseGlow()
        {
            while (true)
            {
                if (_isInteractable && _borderImage != null)
                {
                    // Pulse from bright to slightly dimmer cyan over ~2 seconds
                    float duration = 2.0f;
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.PingPong(elapsed, duration / 2f) / (duration / 2f);
                        float alpha = Mathf.Lerp(0.6f, 1.0f, t);
                        _borderImage.color = new Color(
                            BorderCyanActive.r,
                            BorderCyanActive.g,
                            BorderCyanActive.b,
                            alpha
                        );
                        yield return null;
                    }
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator LabelGlowPulse()
        {
            while (true)
            {
                if (_label != null && _isInteractable)
                {
                    float duration = 1.8f;
                    float elapsed = 0f;
                    Color baseColor = LabelColor;
                    Color glowColor = LabelColorPulse;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.PingPong(elapsed, duration / 2f) / (duration / 2f);
                        _label.color = Color.Lerp(baseColor, glowColor, t * 0.6f);
                        // Slight vertical "breathing" on the text
                        float scaleY = 1f + 0.03f * Mathf.Sin(elapsed * 2f);
                        _label.transform.localScale = new Vector3(1f, scaleY, 1f);
                        yield return null;
                    }
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        public void SetInteractable(bool on)
        {
            _isInteractable = on;
            if (_button != null) _button.interactable = on;
            if (_buttonImage != null)
                _buttonImage.color = on ? ButtonBodyDefault : ButtonBodyDisabled;
            if (_borderImage != null)
                _borderImage.color = on ? BorderCyanActive : BorderDisabled;
            if (_label != null)
                _label.color = on ? LabelColor : new Color(0.3f, 0.3f, 0.35f, 0.5f);
        }

        public void SetLabel(string text)
        {
            if (_label != null) _label.text = text;
        }

        // ── Hover/press tactile feedback ──

        private void AddTriggerEvent(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void OnButtonPointerEnter()
        {
            if (!_isInteractable) return;
            if (_borderImage != null)
                _borderImage.color = BorderCyanHover;
            if (_buttonImage != null)
                _buttonImage.color = ButtonBodyHover;
        }

        private void OnButtonPointerExit()
        {
            if (!_isInteractable) return;
            if (_borderImage != null)
                _borderImage.color = BorderCyanActive;
            if (_buttonImage != null)
                _buttonImage.color = ButtonBodyDefault;
        }

        private void OnButtonPointerDown()
        {
            if (!_isInteractable) return;
            if (_buttonImage != null)
                _buttonImage.color = ButtonBodyPressed;
        }

        private void OnButtonPointerUp()
        {
            if (!_isInteractable) return;
            if (_buttonImage != null)
                _buttonImage.color = ButtonBodyHover;
        }
    }
}
