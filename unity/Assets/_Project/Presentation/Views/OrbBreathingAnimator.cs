using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChromaVale.Presentation.Views
{
    /// <summary>
    /// Gentle idle "breathing" animation for merge-board orb visuals.
    /// Attach to the orb prefab (Orb_T1). Self-contained: detects its own state by polling,
    /// so it never needs events from MergeBoardView.
    ///
    /// Behaviors:
    ///  - 6-frame squash/recover/stretch/recover cycle at 8fps (0.75s loop), driven through
    ///    localScale.y squash/stretch (vertical shift ~4-5px at gameplay scale; position untouched).
    ///  - Per-orb random phase offset (Random.Range(0, loopDuration)) so nearby orbs desync.
    ///  - Drag pause: breathing pauses while the orb is being dragged (polled two ways:
    ///    position delta since last frame, or pointer pressed on this orb's position).
    ///  - Resume: breathing resumes ~0.2s after the orb stops being moved/held.
    ///  - Never fights MergeBoardView's own scale animations (spawn/merge/snap-back): scale
    ///    writes are suppressed until localScale has been externally stable for several frames.
    ///  - Brown orbs: the animator disables itself entirely (sprite name "brown*" or brownish
    ///    color heuristic for procedural fallback sprites).
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbBreathingAnimator : MonoBehaviour
    {
        [Header("Breathing")]
        [Tooltip("Seconds per full cycle. 6 frames at 8fps = 0.75s.")]
        [SerializeField] private float loopDuration = 0.75f;

        [Tooltip("Peak vertical squash/stretch as a fraction of the orb's own scale.y (~4-5px at gameplay scale).")]
        [SerializeField] private float amplitude = 0.04f;

        [Header("Drag / Resume")]
        [Tooltip("Seconds after the orb stops being dragged before breathing resumes.")]
        [SerializeField] private float resumeDelay = 0.2f;

        [Tooltip("World-space radius used to detect 'pointer is currently holding this orb'.")]
        [SerializeField] private float grabRadius = 0.5f;

        [Tooltip("Consecutive stable-scale frames required before this animator may write scale (avoids fighting spawn/merge/snap-back tweens).")]
        [SerializeField] private int requiredStableScaleFrames = 3;

        private const int KeyframeCount = 6;
        private const float ScaleEpsilonSq = 1e-10f;
        private const float MoveEpsilonSq = 1e-8f;

        private SpriteRenderer _sr;
        private Transform _tr;
        private Camera _cam;

        private float[] _keyframes;
        private float _phaseOffset;
        private Vector3 _baseScale;
        private bool _baseScaleCaptured;
        private Vector3 _lastPosition;
        private Vector3 _lastObservedScale;
        private int _stableScaleFrames;
        private float _resumeTimer;
        private bool _isBeingDragged;

        private Sprite _checkedSprite;

        private void Awake()
        {
            _tr = transform;
            _sr = GetComponent<SpriteRenderer>();
            _cam = Camera.main;
            _lastObservedScale = _tr.localScale;
            _lastPosition = _tr.position;

            // Keyframe multipliers for scale.y around base scale:
            //   rest → squash → recover → stretch → recover → rest (loops seamlessly)
            _keyframes = new float[KeyframeCount];
            _keyframes[0] = 1f;
            _keyframes[1] = 1f - amplitude;
            _keyframes[2] = 1f;
            _keyframes[3] = 1f + amplitude;
            _keyframes[4] = 1f;
            _keyframes[5] = 1f;

            // Per-orb random phase: spec asks Random.Range(0, 0.75) == loopDuration.
            _phaseOffset = UnityEngine.Random.Range(0f, loopDuration);
        }

        private void Start()
        {
            CheckBrownAndDisable();
        }

        private void Update()
        {
            if (_cam == null)
                _cam = Camera.main;

            // Defensive: if the sprite ever changes identity (e.g. board sync), re-check brown.
            CheckBrownAndDisable();
            if (!enabled)
                return;

            PollDragState();

            // External scale animation (SpawnRoutine / MergeRoutine / snap-back shrink) is
            // active whenever localScale keeps changing under us. Do not write while that
            // happens, and never capture a mid-animation value as the base scale.
            Vector3 curScale = _tr.localScale;
            if ((curScale - _lastObservedScale).sqrMagnitude > ScaleEpsilonSq)
            {
                _stableScaleFrames = 0;
                _lastObservedScale = curScale;
            }
            else
            {
                _stableScaleFrames++;
            }

            if (_stableScaleFrames < requiredStableScaleFrames)
                return; // some board tween still owns the scale

            if (!_baseScaleCaptured)
            {
                _baseScale = curScale;
                _baseScaleCaptured = true;
            }

            if (_isBeingDragged)
            {
                _resumeTimer = 0f;
                return; // paused while held/moved — do not write scale
            }

            _resumeTimer += Time.deltaTime;
            if (_resumeTimer < resumeDelay)
                return;

            float yMul = SampleBreath((Time.time + _phaseOffset) % loopDuration);
            _tr.localScale = new Vector3(_baseScale.x, _baseScale.y * yMul, _baseScale.z);
            _lastObservedScale = _tr.localScale; // we own the scale again
        }

        /// <summary>
        /// Polls whether this orb is currently being dragged. Two independent signals:
        /// (1) the transform moved since last frame (drag motion, snap-back flight);
        /// (2) the pointer is pressed and this orb is at the pointer (MergeBoardView moves the
        ///     grabbed orb to the pointer every frame, so the held-still case is covered too).
        /// </summary>
        private void PollDragState()
        {
            Vector3 pos = _tr.position;
            bool moved = (pos - _lastPosition).sqrMagnitude > MoveEpsilonSq;
            _lastPosition = pos;

            bool heldAtPointer = false;
            if (IsPointerPressed())
            {
                Vector2 screenPos = ReadPointerScreenPosition();
                if (_cam != null)
                {
                    Vector3 pointerWorld = _cam.ScreenToWorldPoint(screenPos);
                    pointerWorld.z = 0f;
                    heldAtPointer = ((Vector2)pos - (Vector2)pointerWorld).sqrMagnitude < grabRadius * grabRadius;
                }
            }

            _isBeingDragged = moved || heldAtPointer;
        }

        /// <summary>Sample the 6-keyframe loop with smoothstep interpolation.</summary>
        private float SampleBreath(float timeInLoop)
        {
            float slot = loopDuration / KeyframeCount;
            float pos = Mathf.Clamp(timeInLoop / slot, 0f, KeyframeCount);
            int i = Mathf.FloorToInt(pos) % KeyframeCount;
            float frac = pos - Mathf.Floor(pos);
            float a = _keyframes[i];
            float b = _keyframes[(i + 1) % KeyframeCount];
            frac = frac * frac * (3f - 2f * frac); // smoothstep
            return Mathf.Lerp(a, b, frac);
        }

        /// <summary>
        /// Brown orbs never breathe — the animator disables itself entirely.
        /// Art sprites are named like "brown_T1_idle"; procedural fallback sprites have no
        /// meaningful name, so a brownish color heuristic covers that path.
        /// </summary>
        private void CheckBrownAndDisable()
        {
            if (_sr == null)
                return;
            Sprite s = _sr.sprite;
            if (s == null || s == _checkedSprite)
                return;
            _checkedSprite = s;

            bool brown;
            if (!string.IsNullOrEmpty(s.name))
            {
                brown = s.name.StartsWith("brown", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                brown = IsBrownishColor(_sr.color);
            }

            if (brown)
            {
                enabled = false; // spec: disable the animator entirely
                if (_baseScaleCaptured)
                    _tr.localScale = _baseScale; // restore in case we were mid-breath
            }
        }

        private static bool IsBrownishColor(Color c)
        {
            // Brown base is (0.4, 0.2, 0.05) → hue ~30°, sat ~0.7-0.9, value 0.4.
            // Orange shares the hue but has value 1.0, which this heuristic excludes.
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return h >= 0.05f && h <= 0.12f && s > 0.4f && v < 0.7f && c.a > 0.5f;
        }

        private static bool IsPointerPressed()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private static Vector2 ReadPointerScreenPosition()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
        }
    }
}
