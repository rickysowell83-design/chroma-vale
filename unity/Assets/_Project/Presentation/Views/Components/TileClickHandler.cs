using ChromaVale.Core.GameLogic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Attached to each grid tile for click handling.
    /// Left-click = rotate (or place piece on empty cell).
    /// Right-click = undo/remove most recently placed piece.
    /// </summary>
    public class TileClickHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private int _x, _y;
        private PuzzleBoardView _board;
        private bool _isMouseOver;

        public void Init(int x, int y, PuzzleBoardView b) { _x = x; _y = y; _board = b; }
        public void OnPointerClick(PointerEventData d) { if (_board != null) _board.OnPointerDown(_x, _y); }
        public void OnPointerEnter(PointerEventData d) { _isMouseOver = true; if (_board != null) _board.OnTileHover(_x, _y); }
        public void OnPointerExit(PointerEventData d) { _isMouseOver = false; if (_board != null) _board.OnTileHoverExit(_x, _y); }

        private void Update()
        {
            if (_isMouseOver && Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (_board != null) _board.OnRightClick();
            }
        }
    }
}
