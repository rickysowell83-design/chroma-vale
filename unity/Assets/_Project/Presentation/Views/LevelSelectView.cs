// SPDX-License-Identifier: MIT
// Chroma Vale — LevelSelectView: Grid of level buttons with star ratings + lock state.
// Uses SaveGameManager for persistence. Launches MergeBoardView on tap.
using System;
using System.Collections.Generic;
using ChromaVale.Domain.Progression;
using ChromaVale.Infrastructure.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaVale.Presentation.Views
{
    /// <summary>
    /// Presents a grid of level buttons. Each button shows:
    /// - Level number
    /// - Star rating (0-3 filled stars)
    /// - Locked/unlocked state (locked = gray, can't tap)
    /// Tapping an unlocked level loads it into MergeBoardView.
    /// </summary>
    public class LevelSelectView : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int _columns = 5;
        [SerializeField] private float _buttonSize = 120f;
        [SerializeField] private float _spacing = 20f;

        [Header("Colors")]
        [SerializeField] private Color _unlockedColor = new(0.12f, 0.14f, 0.18f);
        [SerializeField] private Color _lockedColor = new(0.04f, 0.05f, 0.06f);
        [SerializeField] private Color _starFilledColor = new(1f, 0.84f, 0.28f);
        [SerializeField] private Color _starEmptyColor = new(0.2f, 0.2f, 0.2f);

        [Header("Refs")]
        [SerializeField] private MergeBoardView _boardView;
        [SerializeField] private Canvas _canvas;

        private readonly List<GameObject> _buttons = new();
        private int _totalLevels = 10; // L1-L10 for vertical slice

        private void Start()
        {
            // Auto-create canvas if not assigned
            if (_canvas == null)
            {
                var canvasGo = new GameObject("LevelSelectCanvas");
                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
            }

            // Auto-find board view if not assigned
            if (_boardView == null)
            {
                _boardView = FindAnyObjectByType<MergeBoardView>();
                if (_boardView == null)
                {
                    Debug.LogError("[LevelSelectView] No MergeBoardView found in scene!");
                    return;
                }
            }

            BuildLevelGrid();
            _boardView.gameObject.SetActive(false);
        }

        private void BuildLevelGrid()
        {
            // Clear existing
            foreach (var btn in _buttons)
            {
                if (btn != null) Destroy(btn);
            }
            _buttons.Clear();

            var saveManager = SaveGameManager.Instance;
            int currentLevel = saveManager != null ? saveManager.CurrentLevel : 1;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_canvas.transform, false);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "Chroma Vale";
            titleText.fontSize = 48;
            titleText.alignment = TextAlignmentOptions.Center;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -60f);
            titleRect.sizeDelta = new Vector2(600f, 80f);

            // Subtitle with total stars
            int totalStars = saveManager != null ? saveManager.TotalChromaStars : 0;
            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(_canvas.transform, false);
            var subText = subGo.AddComponent<TextMeshProUGUI>();
            subText.text = $"Total Stars: {totalStars} / {_totalLevels * 3}";
            subText.fontSize = 24;
            subText.alignment = TextAlignmentOptions.Center;
            var subRect = subGo.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 1f);
            subRect.anchorMax = new Vector2(0.5f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -150f);
            subRect.sizeDelta = new Vector2(600f, 40f);

            // Grid container
            var gridGo = new GameObject("LevelGrid");
            gridGo.transform.SetParent(_canvas.transform, false);
            var gridRect = gridGo.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);

            int rows = Mathf.CeilToInt((float)_totalLevels / _columns);
            float gridWidth = _columns * (_buttonSize + _spacing) - _spacing;
            float gridHeight = rows * (_buttonSize + _spacing) - _spacing;
            gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
            gridRect.anchoredPosition = Vector2.zero;

            for (int level = 1; level <= _totalLevels; level++)
            {
                int col = (level - 1) % _columns;
                int row = (level - 1) / _columns;

                // Flip row so level 1 is top-left
                float x = col * (_buttonSize + _spacing) - gridWidth / 2f + _buttonSize / 2f;
                float y = (rows - 1 - row) * (_buttonSize + _spacing) - gridHeight / 2f + _buttonSize / 2f;

                bool unlocked = level <= currentLevel;
                int stars = saveManager != null ? saveManager.GetStarsForLevel(level) : 0;

                var btn = CreateLevelButton(level, unlocked, stars, new Vector2(x, y));
                btn.transform.SetParent(gridRect, false);
                _buttons.Add(btn);
            }
        }

        private GameObject CreateLevelButton(int level, bool unlocked, int stars, Vector2 pos)
        {
            var go = new GameObject($"Level_{level}");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(_buttonSize, _buttonSize);
            rect.anchoredPosition = pos;

            // Background
            var bg = go.AddComponent<Image>();
            bg.color = unlocked ? _unlockedColor : _lockedColor;
            bg.raycastTarget = unlocked;

            // Level number
            var numGo = new GameObject("Number");
            numGo.transform.SetParent(go.transform, false);
            var numText = numGo.AddComponent<TextMeshProUGUI>();
            numText.text = unlocked ? level.ToString() : "Locked";
            numText.fontSize = 36;
            numText.alignment = TextAlignmentOptions.Center;
            var numRect = numGo.GetComponent<RectTransform>();
            numRect.anchorMin = new Vector2(0.5f, 0.5f);
            numRect.anchorMax = new Vector2(0.5f, 0.5f);
            numRect.pivot = new Vector2(0.5f, 0.5f);
            numRect.anchoredPosition = new Vector2(0f, 10f);
            numRect.sizeDelta = new Vector2(_buttonSize, 50f);

            // Stars row (3 small circles)
            if (unlocked)
            {
                for (int s = 0; s < 3; s++)
                {
                    var starGo = new GameObject($"Star_{s}");
                    starGo.transform.SetParent(go.transform, false);
                    var starImg = starGo.AddComponent<Image>();
                    starImg.color = s < stars ? _starFilledColor : _starEmptyColor;
                    starImg.raycastTarget = false;
                    var starRect = starGo.GetComponent<RectTransform>();
                    starRect.anchorMin = new Vector2(0.5f, 0f);
                    starRect.anchorMax = new Vector2(0.5f, 0f);
                    starRect.pivot = new Vector2(0.5f, 0.5f);
                    starRect.sizeDelta = new Vector2(20f, 20f);
                    starRect.anchoredPosition = new Vector2((s - 1) * 24f, 12f);
                }
            }

            // Button click handler
            if (unlocked)
            {
                var button = go.AddComponent<Button>();
                button.targetGraphic = bg;
                int capturedLevel = level; // capture for closure
                button.onClick.AddListener(() => OnLevelSelected(capturedLevel));
            }

            return go;
        }

        private void OnLevelSelected(int level)
        {
            // Audio: UI button tap
            if (AudioServiceInstaller.Instance != null)
                AudioServiceInstaller.Instance.PlaySound("button_tap");

            Debug.Log($"[LevelSelectView] Level {level} selected");

            // Hide level select, show board
            _canvas.gameObject.SetActive(false);
            _boardView.gameObject.SetActive(true);

            // Load the level
            _boardView.LoadLevel(level);
        }

        /// <summary>
        /// Return to level select after a level is completed.
        /// Called by MergeBoardView when the player taps "Back" or after level complete.
        /// </summary>
        public void ReturnToSelect()
        {
            _boardView.gameObject.SetActive(false);
            _canvas.gameObject.SetActive(true);
            BuildLevelGrid(); // Refresh stars
        }
    }
}
