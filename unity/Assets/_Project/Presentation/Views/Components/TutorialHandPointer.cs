using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class TutorialHandPointer : MonoBehaviour
    {
        private RectTransform _handRect;
        private RawImage _handImage;
        private TextMeshProUGUI _badgeText;
        private GameObject _badgeBg;
        private RectTransform _badgeRect;
        private CanvasGroup _canvasGroup;

        private Tweener _bounceTween;
        private Tweener _transitionTween;

        private static readonly Color HandOutline = new(0f, 0.898f, 1f, 0.6f);
        private static readonly Color HandFill = new(0f, 0.898f, 1f, 0.10f);
        private static readonly Color HandTip = new(1f, 1f, 1f, 0.4f);
        private static readonly Color BadgeBgColor = new(0.051f, 0.067f, 0.090f, 0.7f);
        private static readonly Color BadgeTextColor = Color.white;
        private static readonly Color BadgeTextCyan = new(0f, 0.898f, 1f);

        private void Awake() { CreateUI(); }

        private void CreateUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            var handGo = new GameObject("Hand");
            handGo.transform.SetParent(transform, false);
            _handImage = handGo.AddComponent<RawImage>();
            _handImage.texture = GenerateHandTexture(64, 80);
            _handImage.raycastTarget = false;
            _handRect = handGo.GetComponent<RectTransform>();
            _handRect.sizeDelta = new Vector2(48f, 60f);
            _handRect.anchorMin = new Vector2(0.5f, 0.5f);
            _handRect.anchorMax = new Vector2(0.5f, 0.5f);
            _handRect.anchoredPosition = Vector2.zero;

            _badgeBg = new GameObject("BadgeBg");
            _badgeBg.transform.SetParent(handGo.transform, false);
            var badgeBgImg = _badgeBg.AddComponent<Image>();
            badgeBgImg.color = BadgeBgColor;
            badgeBgImg.raycastTarget = false;
            _badgeRect = _badgeBg.GetComponent<RectTransform>();
            _badgeRect.anchorMin = new Vector2(0.5f, 1.05f);
            _badgeRect.anchorMax = new Vector2(0.5f, 1.05f);
            _badgeRect.sizeDelta = new Vector2(100f, 22f);
            _badgeRect.anchoredPosition = new Vector2(0f, 6f);
            _badgeRect.pivot = new Vector2(0.5f, 0f);

            var badgeTextGo = new GameObject("BadgeText");
            badgeTextGo.transform.SetParent(_badgeBg.transform, false);
            _badgeText = badgeTextGo.AddComponent<TextMeshProUGUI>();
            _badgeText.fontSize = 10;
            _badgeText.fontStyle = FontStyles.Bold;
            _badgeText.alignment = TextAlignmentOptions.Center;
            _badgeText.color = BadgeTextColor;
            var textRt = _badgeText.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            _badgeBg.SetActive(false);
        }

        private Texture2D GenerateHandTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            int palmTop = (int)(h * 0.30f);
            int palmBot = (int)(h * 0.85f);
            int palmLeft = (int)(w * 0.15f);
            int palmRight = (int)(w * 0.80f);

            for (int y = palmTop; y < palmBot; y++)
                for (int x = palmLeft; x < palmRight; x++)
                    SetP(pixels, w, x, y, HandFill);

            DrawRectOutline(pixels, w, palmLeft, palmTop, palmRight - palmLeft, palmBot - palmTop, HandOutline);

            int fingerLeft = (int)(w * 0.55f);
            int fingerRight = (int)(w * 0.78f);
            int fingerBottom = palmTop;
            int fingerTop = (int)(h * 0.05f);

            for (int y = fingerTop; y < fingerBottom; y++)
                for (int x = fingerLeft; x < fingerRight; x++)
                    SetP(pixels, w, x, y, HandFill);

            DrawRectOutline(pixels, w, fingerLeft, fingerTop, fingerRight - fingerLeft, fingerBottom - fingerTop, HandOutline);

            for (int y = fingerTop; y < fingerTop + (int)(h * 0.05f); y++)
                for (int x = fingerLeft; x < fingerRight; x++)
                    SetP(pixels, w, x, y, HandTip);

            int thumbLeft = (int)(w * 0.02f);
            int thumbRight = palmLeft;
            int thumbTop = (int)(h * 0.50f);
            int thumbBot = (int)(h * 0.70f);

            for (int y = thumbTop; y < thumbBot; y++)
                for (int x = thumbLeft; x < thumbRight; x++)
                    SetP(pixels, w, x, y, HandFill);

            DrawRectOutline(pixels, w, thumbLeft, thumbTop, thumbRight - thumbLeft, thumbBot - thumbTop, HandOutline);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void SetP(Color[] pixels, int w, int x, int y, Color c)
        {
            if (x < 0 || x >= w || y < 0 || y >= pixels.Length / w) return;
            int idx = y * w + x;
            Color bg = pixels[idx];
            pixels[idx] = new Color(
                bg.r * (1f - c.a) + c.r * c.a,
                bg.g * (1f - c.a) + c.g * c.a,
                bg.b * (1f - c.a) + c.b * c.a,
                bg.a + c.a * (1f - bg.a));
        }

        private static void DrawRectOutline(Color[] pixels, int w, int x, int y, int rw, int rh, Color c)
        {
            int t = 2;
            for (int i = 0; i < t; i++)
            {
                for (int px = x - i; px < x + rw + i; px++) SetP(pixels, w, px, y - i, c);
                for (int px = x - i; px < x + rw + i; px++) SetP(pixels, w, px, y + rh + i - 1, c);
                for (int py = y - i; py < y + rh + i; py++) SetP(pixels, w, x - i, py, c);
                for (int py = y - i; py < y + rh + i; py++) SetP(pixels, w, x + rw + i - 1, py, c);
            }
        }

        public void PointAt(Vector2 screenPos, string badgeText, bool isCyanBadge = false)
        {
            StopAnimations();
            if (_badgeBg != null)
            {
                _badgeBg.SetActive(true);
                _badgeText.text = badgeText;
                _badgeText.color = isCyanBadge ? BadgeTextCyan : BadgeTextColor;
            }
            _handRect.anchoredPosition = screenPos;
            _canvasGroup.alpha = 1f;
            _handRect.localScale = Vector3.one;
            _handRect.localRotation = Quaternion.Euler(0f, 0f, isCyanBadge ? -15f : 0f);
            StartBounceAnim(slow: false);
        }

        public void TransitionTo(Vector2 target, string badgeText, bool isCyanBadge, float duration = 0.4f)
        {
            StopAnimations();
            if (_badgeBg != null)
            {
                _badgeBg.SetActive(true);
                _badgeText.text = badgeText;
                _badgeText.color = isCyanBadge ? BadgeTextCyan : BadgeTextColor;
            }
            StartArcTransition(target, duration, isCyanBadge);
        }

        public void FadeOut()
        {
            StopAnimations();
            StartFadeOutAnim();
        }

        public void Hide()
        {
            StopAnimations();
            _canvasGroup.alpha = 0f;
            if (_badgeBg != null) _badgeBg.SetActive(false);
        }

        public void ShowDimly(Vector2 screenPos, string badgeText, bool isCyanBadge = false)
        {
            StopAnimations();
            _canvasGroup.alpha = 0.4f;
            _handRect.anchoredPosition = screenPos;
            if (_badgeBg != null)
            {
                _badgeBg.SetActive(true);
                _badgeText.text = badgeText;
                _badgeText.color = isCyanBadge ? BadgeTextCyan : BadgeTextColor;
            }
            StartBounceAnim(slow: true);
        }

        private void StartBounceAnim(bool slow = false)
        {
            _bounceTween?.Kill();
            float period = slow ? 2.0f : 1.2f;
            // Bounce up/down
            _bounceTween = _handRect.DOAnchorPosY(_handRect.anchoredPosition.y + 4f, period * 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            // Tap scale (brief shrink every 2s)
            if (!slow)
            {
                _handRect.DOScale(0.95f, 0.1f)
                    .SetLoops(-1, LoopType.Restart)
                    .SetDelay(1.9f);
            }
            else
            {
                _handRect.localScale = Vector3.one * 0.85f;
            }
        }

        private void StartArcTransition(Vector2 target, float duration, bool isCyanBadge)
        {
            _transitionTween?.Kill();
            _canvasGroup.alpha = 1f;
            Vector2 start = _handRect.anchoredPosition;
            Vector2 mid = (start + target) * 0.5f + new Vector2(0f, 40f);

            // Use a path tween for the arc
            Vector3[] path = new Vector3[] { start, mid, target };
            _transitionTween = _handRect.DOPath(path, duration, PathType.CatmullRom)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _handRect.anchoredPosition = target;
                    _handRect.localRotation = Quaternion.Euler(0f, 0f, isCyanBadge ? -15f : 0f);
                    StartBounceAnim();
                });
        }

        private void StartFadeOutAnim()
        {
            float startAlpha = _canvasGroup.alpha;
            Vector2 startPos = _handRect.anchoredPosition;

            _canvasGroup.DOFade(0f, 0.8f).SetEase(Ease.OutQuad);
            _handRect.DOAnchorPos(startPos + new Vector2(0f, 15f), 0.8f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _canvasGroup.alpha = 0f;
                    if (_badgeBg != null) _badgeBg.SetActive(false);
                });
        }

        private void StopAnimations()
        {
            _bounceTween?.Kill();
            _bounceTween = null;
            _transitionTween?.Kill();
            _transitionTween = null;
            // Kill ALL tweens on handRect to clean up any orphaned scale tweens
            _handRect?.DOKill();
        }

        private void OnDestroy()
        {
            StopAnimations();
        }
    }
}
