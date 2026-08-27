// SPDX-License-Identifier: MIT
// Chroma Vale — JuiceController (Presentation, Merge/Win feedback)
//
// SALVAGE OF t_66805704 (game-builder timed out at the 60-iteration wall, run 191).
// Director wrote the complete implementation from the verified event API so the build
// cannot ship without merge/win juice — the #1 "not fun" factor from playtest.
//
// Subscribes to the SAME IBoardController the MergeBoardView drives:
//   - OnBoardChanged(BoardChange)  -> merge = ChangeType.OrbTransformed
//   - OnLevelComplete(LevelResult) -> win confetti + haptics
// Reuses AudioServiceInstaller.Instance.PlaySound (already exists) for SFX.
// Uses DOTween (present in project) for scale-pop + screen shake.
// Tier intensity follows DESIGN_CANON §3.1 (T1..T5 scale + particle emission ramp).

using ChromaVale.Core.GameLogic;
using ChromaVale.Infrastructure.Audio;
using ChromaVale.Presentation.Views.Components;
using DG.Tweening;
using UnityEngine;

namespace ChromaVale.Presentation.Views
{
    /// <summary>
    /// Drives all merge/win "juice": particle bursts, scale-pop, screen shake,
    /// and haptics. Pure presentation — no game logic.
    /// Drop on the same GameObject as MergeBoardView (or a parent) and wire
    /// <see cref="_boardView"/> in the inspector (auto-resolved if co-located).
    /// </summary>
    public sealed class JuiceController : MonoBehaviour
    {
        // ── Inspector ──
        [Header("Wiring")]
        [Tooltip("The MergeBoardView whose Board the juice subscribes to. If null, auto-resolved in Awake.")]
        [SerializeField] private MergeBoardView _boardView;

        [Header("Particles")]
        [SerializeField] private ParticleSystem _mergeBurstPrefab;
        [SerializeField] private ParticleSystem _winConfettiPrefab;

        [Header("Tuning")]
        [SerializeField] private float _mergeScalePop = 1.35f;
        [SerializeField] private float _mergePopDuration = 0.18f;
        [SerializeField] private float _screenShakeOnTierUp = 0.12f;
        [SerializeField] private float _screenShakeOnWin = 0.25f;

        // ── State ──
        private IBoardController _board;
        private ParticleSystem _mergeBurst;
        private ParticleSystem _winConfetti;
        private Camera _cam;

        // ── Lifecycle ──
        private void Awake()
        {
            if (_boardView == null)
                _boardView = GetComponentInParent<MergeBoardView>() ?? GetComponent<MergeBoardView>();

            _cam = Camera.main;

            // Self-provision particle systems so juice works with ZERO inspector wiring
            // (the pared-down build leaves prefab fields empty; FX sprites live in Resources/Art/FX).
            if (_mergeBurst == null)
                _mergeBurst = ProvisionBurst("fx_mergeburst");
            if (_winConfetti == null)
                _winConfetti = ProvisionBurst("fx_confetti");
        }

        /// <summary>Builds a one-shot ParticleSystem from a sprite in Resources/Art/FX if no prefab was assigned.</summary>
        private ParticleSystem ProvisionBurst(string spriteName)
        {
            var sprite = Resources.Load<Sprite>($"FX/{spriteName}");
            if (sprite == null) return null;

            var go = new GameObject($"Juice_{spriteName}");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.3f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy; // auto-cleanup

            var tex = ps.textureSheetAnimation; // reserved
            var emit = ps.emission;
            emit.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            // Color the particles from the FX sprite.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Color.white, new Color(1, 1, 1, 0));

            // Use the sprite as the particle texture via the Renderer module.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.mainTexture = sprite.texture;
            renderer.material.SetColor("_Color", Color.white);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Stop();
            return ps;
        }

        private void OnEnable()
        {
            if (_boardView == null) return;
            _board = _boardView.Board;
            if (_board == null) return;
            _board.OnBoardChanged += HandleBoardChanged;
            _board.OnLevelComplete += HandleLevelComplete;
        }

        private void OnDisable()
        {
            if (_board == null) return;
            _board.OnBoardChanged -= HandleBoardChanged;
            _board.OnLevelComplete -= HandleLevelComplete;
        }

        // ── Board events ──
        private void HandleBoardChanged(BoardChange change)
        {
            if (change.Type != ChangeType.OrbTransformed) return; // merges / mixes / brown-clear
            if (change.NewOrb == null) return;

            int tier = (int)change.NewOrb.Tier; // T1=1 .. T5=5
            Vector3 world = _boardView.GridToWorld(change.Position.X, change.Position.Y);

            // Particle burst at the cell (intensity ramps with tier — canon §3.1).
            if (_mergeBurst != null)
            {
                _mergeBurst.transform.position = world;
                var main = _mergeBurst.main;
                main.maxParticles = Mathf.Clamp(6 + tier * 4, 6, 30);
                _mergeBurst.Play();
            }

            // Scale-pop the result orb, then settle to its tier scale.
            var orbVis = _boardView.GetOrbVisual(change.Position.X, change.Position.Y);
            if (orbVis != null)
            {
                float baseScale = 1f + (tier - 1) * 0.2f; // §3.1: +20%/tier
                orbVis.transform.DOScale(baseScale * _mergeScalePop, _mergePopDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => orbVis.transform.DOScale(baseScale, _mergePopDuration)
                        .SetEase(Ease.OutQuad));
            }

            // Mix detection (different colour in/out — e.g. cyan+magenta -> Purple) for SFX + bigger shake.
            bool isMix = change.OldOrb != null && change.OldOrb.Color != change.NewOrb.Color;

            // Haptic tick + SFX (reuses existing AudioServiceInstaller).
            Handheld.Vibrate();
            AudioServiceInstaller.Instance?.PlaySound(isMix ? "mix" : "merge");

            // Screen shake scales with tier (bigger orb = bigger pop).
            if (tier >= 3) Shake(_screenShakeOnTierUp * (tier - 2));
        }

        private void HandleLevelComplete(LevelResult result)
        {
            if (_winConfetti != null)
            {
                _winConfetti.transform.position = _cam != null
                    ? _cam.transform.position + _cam.transform.forward * 5f
                    : Vector3.zero;
                _winConfetti.Play();
            }

            Handheld.Vibrate();
            AudioServiceInstaller.Instance?.PlaySound("win");
            Shake(_screenShakeOnWin);

            Debug.Log($"[Juice] Level complete — moves {result.MovesUsed}, par {result.Par}, stars {result.Stars}");
        }

        // ── Helpers ──
        private void Shake(float amount)
        {
            if (_cam == null) return;
            _cam.transform.DOShakePosition(0.2f, amount, 12, 90).SetLink(_cam.gameObject);
        }
    }
}
