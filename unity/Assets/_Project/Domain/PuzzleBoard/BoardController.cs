// SPDX-License-Identifier: MIT
// Chroma Vale — BoardController (Domain layer)
//
// Orchestrates merge operations on the game board using the pure-C# Core
// merge rules. Holds the grid state (2D array of OrbData?), validates moves,
// applies MergeResults, tracks the move counter, and checks the win
// condition against LevelData.RestorationTargets.
//
// Deliberately engine-free: no MonoBehaviour, no UnityEngine imports. The
// presentation layer (PuzzleBoardView) binds to IBoardController events.

#nullable enable

using System;
using ChromaVale.Core.GameLogic;

namespace ChromaVale.Domain.PuzzleBoard
{
    /// <summary>
    /// Grid state manager for the Chroma Vale merge game.
    /// Implements IBoardController using MergeRules for all merge decisions.
    /// </summary>
    public class BoardController : IBoardController
    {
        private OrbData?[,] _cells = new OrbData?[0, 0];
        private RestorationTarget[] _targets = Array.Empty<RestorationTarget>();
        private int _par;
        private bool _complete;
        private bool _mixingEnabled = true;
        private BrownSpoilSystem? _spoilSystem;
        private DuskfallSystem? _duskfallSystem;

        /// <summary>Board width (set on Initialize).</summary>
        public int Width { get; private set; }

        /// <summary>Board height (set on Initialize).</summary>
        public int Height { get; private set; }

        /// <inheritdoc />
        public bool IsLevelComplete => _complete;

        /// <inheritdoc />
        public int MoveCount { get; private set; }

        /// <summary>
        /// Duskfall countdown state for the active level (null when the level
        /// has duskEnabled false). Presentation reads CounterRatio each frame
        /// to drive the vignette shader and subscribes to its events.
        /// </summary>
        public DuskfallSystem? Duskfall => _duskfallSystem;

        /// <inheritdoc />
        public event Action<BoardChange>? OnBoardChanged;

        /// <inheritdoc />
        public event Action<LevelResult>? OnLevelComplete;

        /// <summary>
        /// Engine-free diagnostic hook (temporary, task t_e36536c3). The
        /// presentation layer subscribes and forwards to Debug.Log. Kept as
        /// plain System.Action so the headless testrunner stays compile-clean.
        /// </summary>
        public event Action<string>? OnDiagnostic;

        /// <summary>
        /// Populates the grid from level data (orbs, targets, par) and resets
        /// the move counter. Out-of-bounds orb placements are skipped.
        /// </summary>
        public void Initialize(LevelData levelData)
        {
            if (levelData == null) throw new ArgumentNullException(nameof(levelData));

            Width = levelData.Width;
            Height = levelData.Height;
            _cells = new OrbData?[Width, Height];

            _targets = levelData.RestorationTargets ?? Array.Empty<RestorationTarget>();
            _par = levelData.ParMoves;
            _mixingEnabled = levelData.MixingEnabled;
            _spoilSystem = new BrownSpoilSystem(levelData);
            _duskfallSystem = new DuskfallSystem(levelData);
            _duskfallSystem.Reset();
            MoveCount = 0;
            _complete = false;

            // Arm the dusk countdown immediately when a Brown is pre-placed
            // (L8 always seeds Brown — spec §2 start-at-zero determinism).
            if (_duskfallSystem.Enabled)
            {
                bool prePlacedBrown = false;
                if (levelData.MergeOrbs != null)
                {
                    foreach (var orb in levelData.MergeOrbs)
                    {
                        if (orb.Color == OrbColor.Brown) { prePlacedBrown = true; break; }
                    }
                }
                if (prePlacedBrown) _duskfallSystem.Arm();
            }

            if (levelData.MergeOrbs != null)
            {
                foreach (var orb in levelData.MergeOrbs)
                {
                    if (IsInBounds(orb.X, orb.Y) && _cells[orb.X, orb.Y] == null)
                        _cells[orb.X, orb.Y] = new OrbData(orb.Color, orb.Tier);
                }
            }
        }

        /// <inheritdoc />
        public OrbData? GetOrbAt(GridPosition pos)
        {
            return IsInBounds(pos.X, pos.Y) ? _cells[pos.X, pos.Y] : null;
        }

        /// <inheritdoc />
        public bool TryMergeAt(GridPosition source, GridPosition target)
        {
            if (source == null || target == null) return false;

            // 1. Bounds check. Free-drag (playtest fix 2026-08-19): any cell to
            //    any cell merges — adjacency removed so T2+T2 merges separated by
            //    an empty cell after a row merge still work.
            if (!IsInBounds(source.X, source.Y) || !IsInBounds(target.X, target.Y))
                return false;

            // 1b. No self-merge: dropping an orb onto its own cell is a snap-back
            //     (UI) case, never a merge. Without this guard, CanMerge(a,a) is
            //     true and the orb silently upgrades itself in place.
            if (source.X == target.X && source.Y == target.Y)
                return false;

            var sourceOrb = _cells[source.X, source.Y];
            var targetOrb = _cells[target.X, target.Y];

            // 2. Ask Core rules whether this pair merges at all.
            var canMerge = MergeRules.CanMerge(sourceOrb, targetOrb, _mixingEnabled);
            OnDiagnostic?.Invoke(
                $"TryMergeAt src=({source.X},{source.Y}) {FmtOrb(sourceOrb)} " +
                $"dst=({target.X},{target.Y}) {FmtOrb(targetOrb)} " +
                $"canMerge={canMerge} mixing={_mixingEnabled}");
            if (!canMerge)
                return false;

            var result = MergeRules.TryMerge(sourceOrb, targetOrb, _mixingEnabled);
            OnDiagnostic?.Invoke(
                $"TryMergeAt outcome={result.Outcome} " +
                $"resultOrb={FmtOrb(result.ResultOrb)} " +
                $"consumesTarget={result.ConsumesTarget}");
            if (result.Outcome == MergeOutcome.Invalid)
                return false;

            // 3. Apply: consume source orb (move it onto the target cell).
            _cells[source.X, source.Y] = null;
            Emit(new BoardChange(ChangeType.OrbRemoved, source, sourceOrb, null));

            var oldTargetOrb = _cells[target.X, target.Y];
            if (result.ConsumesTarget)
            {
                _cells[target.X, target.Y] = null;
            }

            if (result.ResultOrb != null)
            {
                _cells[target.X, target.Y] = result.ResultOrb;
                Emit(new BoardChange(
                    ChangeType.OrbTransformed,
                    target,
                    oldTargetOrb,
                    result.ResultOrb));
            }
            else
            {
                Emit(new BoardChange(ChangeType.OrbRemoved, target, oldTargetOrb, null));
            }

            // 4. Count the move and re-check the win condition.
            MoveCount++;

            // 5. Brown pressure mechanic — mutually exclusive per level:
            //    Duskfall (visible countdown) takes priority over the retired
            //    SpoilingBrown multiplication (DESIGN_CANON §3.3.1, failed
            //    playtest — kept only for levels still flagging spoilEnabled).
            if (_duskfallSystem != null && _duskfallSystem.Enabled)
            {
                _duskfallSystem.OnMoveCompleted(this);
            }
            else if (_spoilSystem != null && _spoilSystem.Enabled)
            {
                var spoils = _spoilSystem.OnMoveCompleted(this);
                foreach (var spawn in spoils)
                {
                    _cells[spawn.X, spawn.Y] = new OrbData(OrbColor.Brown, OrbTier.T1);
                    Emit(new BoardChange(ChangeType.OrbAdded, spawn, null, _cells[spawn.X, spawn.Y]));
                }
            }

            CheckWin();

            return true;
        }

        private void CheckWin()
        {
            if (_targets.Length == 0)
            {
                _complete = false; // no targets defined → never auto-complete
                return;
            }

            // TEMP-DIAG (t_e36536c3): target requirements + full board state
            var req = string.Join(", ",
                Array.ConvertAll(_targets, t => $"{t.Color}/{t.Tier}"));
            var dump = new System.Text.StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var orb = _cells[x, y];
                    if (orb != null)
                        dump.Append($"({x},{y})={FmtOrb(orb)} ");
                }
            }
            OnDiagnostic?.Invoke(
                $"CheckWin(move={MoveCount}) targets=[{req}] board=[{dump}]");

            // Position-agnostic win (genre standard: Merge Dragons / EverMerge —
            // produce the required orb anywhere, don't park it on a specific cell).
            // Fixes levels whose targets sit at unreachable far corners like (3,3).
            // Each target must match a DIFFERENT orb — track consumed cells so
            // duplicate targets (e.g. 2x Cyan T2) can't both match the same orb
            // and produce a false win with fewer orbs than the level requires.
            var consumed = new System.Collections.Generic.HashSet<(int x, int y)>();
            foreach (var t in _targets)
            {
                bool found = false;
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (consumed.Contains((x, y))) continue;
                        var orb = _cells[x, y];
                        if (orb != null && orb.Color == t.Color && orb.Tier == t.Tier)
                        {
                            consumed.Add((x, y));
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                if (!found)
                {
                    OnDiagnostic?.Invoke($"CheckWin MISSING target {t.Color}/{t.Tier}");
                    _complete = false;
                    return;
                }
            }

            _complete = true;
            OnDiagnostic?.Invoke($"CheckWin COMPLETE — all targets matched");
            OnLevelComplete?.Invoke(new LevelResult(MoveCount, _par, CalculateStars(MoveCount, _par)));
        }

        /// <summary>
        /// Star rating per GDD §5.5: 3 stars if moves <= par, 2 stars if moves <= par * 1.5,
        /// otherwise 1 star. 3 stars is never a progression gate (vanity only).
        /// Designer ruling (2026-08-19): GDD ×1.5 formula is canon — the earlier
        /// par+2 threshold in the card spec was an error and is NOT used.
        /// </summary>
        public static int CalculateStars(int moves, int par)
        {
            if (moves <= par) return 3;
            if (moves <= par * 1.5) return 2;
            return 1;
        }

        private bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

#if UNITY_EDITOR
        /// <summary>EditMode test hook — removes the orb at a cell without firing
        /// board events (lets dusk tests simulate "last Brown left the board").</summary>
        public void DebugClearCell(int x, int y)
        {
            if (IsInBounds(x, y)) _cells[x, y] = null;
        }
#endif

        /// <summary>TEMP-DIAG (t_e36536c3): engine-free orb formatter.</summary>
        private static string FmtOrb(OrbData? orb)
        {
            return orb == null ? "empty" : $"{orb.Color}/{orb.Tier}";
        }

        private void Emit(BoardChange change)
        {
            OnBoardChanged?.Invoke(change);
        }
    }
}
