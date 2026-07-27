using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChromaVale.Presentation.Views.Components
{
    public class WinPopup : MonoBehaviour
    {
        [Header("References")]
        private Canvas _canvas;
        private Image _bgImage;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private TextMeshProUGUI _scoreText;
        private TextMeshProUGUI _starText;
        private GameObject _nextLevelButton;

        public event Action OnNextLevel;
        public event Action OnReplay;

        private void Awake()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            // WinCanvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // Background
            var bg = new GameObject("WinBG");
            bg.transform.SetParent(transform, false);
            _bgImage = bg.AddComponent<Image>();
            _bgImage.color = new Color(0.02f, 0.02f, 0.06f, 0f);
            _bgImage.raycastTarget = false;
            var br = bg.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.sizeDelta = Vector2.zero;

            // Title
            var t1 = new GameObject("WinMain");
            t1.transform.SetParent(bg.transform, false);
            _titleText = t1.AddComponent<TextMeshProUGUI>();
            _titleText.text = "CIRCUIT RESTORED!";
            _titleText.fontSize = 32;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = ChromaPalette.NeonCyan;
            var wr = _titleText.GetComponent<RectTransform>();
            wr.anchorMin = new Vector2(0.5f, 0.7f);
            wr.anchorMax = new Vector2(0.5f, 0.7f);
            wr.sizeDelta = new Vector2(500, 70);

            // Subtitle
            var t2 = new GameObject("WinSub");
            t2.transform.SetParent(bg.transform, false);
            _subtitleText = t2.AddComponent<TextMeshProUGUI>();
            _subtitleText.text = "Flow delivered successfully.";
            _subtitleText.fontSize = 15;
            _subtitleText.alignment = TextAlignmentOptions.Center;
            _subtitleText.color = new Color(0.5f, 0.5f, 0.6f);
            var wr2 = _subtitleText.GetComponent<RectTransform>();
            wr2.anchorMin = new Vector2(0.5f, 0.6f);
            wr2.anchorMax = new Vector2(0.5f, 0.6f);
            wr2.sizeDelta = new Vector2(400, 35);

            // Stars — ASCII bracket display with TMP (LiberationSans SDF)
            var t3 = new GameObject("WinStars");
            t3.transform.SetParent(bg.transform, false);
            _starText = t3.AddComponent<TextMeshProUGUI>();
            _starText.text = "[   ]";
            _starText.fontSize = 36;
            _starText.fontStyle = FontStyles.Bold;
            _starText.alignment = TextAlignmentOptions.Center;
            _starText.color = ChromaPalette.NeonYellow;
            var wr3 = _starText.GetComponent<RectTransform>();
            wr3.anchorMin = new Vector2(0.5f, 0.5f);
            wr3.anchorMax = new Vector2(0.5f, 0.5f);
            wr3.sizeDelta = new Vector2(400, 40);

            // Score
            var t4 = new GameObject("WinScore");
            t4.transform.SetParent(bg.transform, false);
            _scoreText = t4.AddComponent<TextMeshProUGUI>();
            _scoreText.text = "Completed in 0 moves";
            _scoreText.fontSize = 13;
            _scoreText.alignment = TextAlignmentOptions.Center;
            _scoreText.color = new Color(0.3f, 0.3f, 0.4f);
            var wr4 = _scoreText.GetComponent<RectTransform>();
            wr4.anchorMin = new Vector2(0.5f, 0.43f);
            wr4.anchorMax = new Vector2(0.5f, 0.43f);
            wr4.sizeDelta = new Vector2(400, 25);

            // Play Again
            CreatePopupButton(bg, "Play Again", new Vector2(0.5f, 0.3f),
                () => { if (OnReplay != null) OnReplay(); },
                new Color(0.1f, 0.4f, 0.45f, 0.9f));

            // Next Level
            _nextLevelButton = CreatePopupButton(bg, "NEXT LEVEL >>", new Vector2(0.5f, 0.2f),
                () => { if (OnNextLevel != null) OnNextLevel(); },
                new Color(0.35f, 0.15f, 0.45f, 0.9f));

            gameObject.SetActive(false);
        }

        private GameObject CreatePopupButton(GameObject parent, string label, Vector2 anchor,
            UnityEngine.Events.UnityAction onClick, Color color)
        {
            var go = new GameObject(label + "Btn");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.sizeDelta = new Vector2(160, 36);
            r.anchoredPosition = Vector2.zero;

            var txt = new GameObject(label + "Text");
            txt.transform.SetParent(go.transform, false);
            var tx = txt.AddComponent<TextMeshProUGUI>();
            tx.text = label;
            tx.fontSize = 14;
            tx.alignment = TextAlignmentOptions.Center;
            tx.color = Color.white;
            var tr = tx.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;

            return go;
        }

        public void Show(int starsEarned, int moveCount, bool isLastLevel)
        {
            _titleText.text = "CIRCUIT RESTORED!";
            _subtitleText.text = starsEarned >= 3
                ? "All targets reached. Maximum efficiency."
                : "Flow delivered successfully.";
            _subtitleText.color = starsEarned >= 3
                ? ChromaPalette.NeonCyan
                : new Color(0.5f, 0.5f, 0.6f);
            _scoreText.text = "Completed in " + moveCount + " moves";

            // Stars — ASCII brackets
            _starText.text = starsEarned switch
            {
                3 => "[***]",
                2 => "[**-]",
                _ => "[*--]"
            };

            // Disable next level button if on last level
            if (_nextLevelButton != null)
            {
                var btn = _nextLevelButton.GetComponent<Button>();
                if (btn != null) btn.interactable = !isLastLevel;
            }

            gameObject.SetActive(true);
            StartCoroutine(FadeBg());
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private IEnumerator FadeBg()
        {
            float e = 0f, d = 0.8f;
            while (e < d)
            {
                e += Time.deltaTime;
                var c = _bgImage.color;
                c.a = Mathf.Lerp(0f, 0.8f, e / d);
                _bgImage.color = c;
                yield return null;
            }
        }
    }
}
