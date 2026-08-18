// LevelModel.cs — Chroma Merge level schema, canon-faithful merge rules.
// Source of truth: DESIGN_CANON.md v2.0 §3 (merge rules) + §10 (level JSON schema).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChromaMerge.Validator
{
    public enum OrbColor { Cyan = 0, Magenta = 1, Yellow = 2, Purple = 6, Green = 7, Orange = 8, Brown = 9 }

    public sealed class Orb
    {
        public OrbColor Color;
        public int Tier;      // 1..5 (cap)
        public int X, Y;      // board position (for schema fidelity + obstacle checks)

        public bool IsBase => (int)Color <= 2;
        public bool IsMixed => (int)Color >= 6 && (int)Color <= 8;
        public bool IsBrown => Color == OrbColor.Brown;

        public string Key => ((int)Color) + ":" + Tier;

        public Orb Clone() => (Orb)MemberwiseClone();

        public override string ToString() => Color + "T" + Tier + "@(" + X + "," + Y + ")";
    }

    public sealed class Target
    {
        public OrbColor Color;
        public int Tier;
        public int X, Y;
        public string Key => ((int)Color) + ":" + Tier + ":" + X + "," + Y;
        public override string ToString() => Color + "T" + Tier + "@(" + X + "," + Y + ")";
    }

    public sealed class Level
    {
        public string Id;
        public string Name;
        public int Width, Height;
        public int ParMoves;
        public string Difficulty;
        public string Teaches;
        public List<Orb> Orbs = new List<Orb>();
        public List<Target> Targets = new List<Target>();
        public List<(int X, int Y)> Obstacles = new List<(int X, int Y)>();

        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            if (Width < 2 || Height < 2) errors.Add("grid too small");
            foreach (var o in Orbs)
            {
                if (o.Tier < 1 || o.Tier > 5) errors.Add("orb tier out of range: " + o);
                if (o.X < 0 || o.X >= Width || o.Y < 0 || o.Y >= Height) errors.Add("orb out of bounds: " + o);
                if (Obstacles.Contains((o.X, o.Y))) errors.Add("orb on obstacle: " + o);
            }
            foreach (var t in Targets)
            {
                if (t.Tier < 1 || t.Tier > 5) errors.Add("target tier out of range: " + t);
                if (t.X < 0 || t.X >= Width || t.Y < 0 || t.Y >= Height) errors.Add("target out of bounds: " + t);
                if (Obstacles.Contains((t.X, t.Y))) errors.Add("target on obstacle: " + t);
            }
            if (Targets.Count == 0) errors.Add("level has no targets");
            // Duplicate targets must be representable (each needs its own fill).
            return errors.Count == 0;
        }
    }

    /// <summary>
    /// Merge rules per DESIGN_CANON v2.0 §3, overriding the routing-era ColorMixer:
    ///  - Tier Merge: same color + same tier (Tier &lt; 5) -> same color, tier+1
    ///  - Color Mix:  different colors + same tier -> mix result at same tier
    ///      base+base      -> mixed color  (C+M=P, C+Y=G, M+Y=O)
    ///      mixed+mixed    -> BROWN        (canon §3.3; ColorMixer.Mix wrongly normalizes these)
    ///      anything+Brown -> Brown        (Brown absorbs)
    ///  - Brown Clear:   Brown + Brown -> dissolves, no new orb (consumes a move)
    ///  - T5 is the cap: two T5 orbs cannot tier-merge (they can still color-mix or clear).
    ///  - Targets: produced orb matching an unfilled target fills it (consumed).
    /// </summary>
    public static class MergeRules
    {
        public static bool CanMerge(Orb a, Orb b)
        {
            if (a.IsBrown && b.IsBrown) return true;                     // clear
            if (a.Tier != b.Tier) return false;                          // merges require same tier
            if (a.Color == b.Color) return a.Tier < 5;                   // tier merge under cap
            return true;                                                 // color mix (any two colors, same tier)
        }

        /// <summary>Result of merging a and b, or null if illegal. Consumes a & b, produces returned orb(s).</summary>
        public static MergeResult Merge(Orb a, Orb b)
        {
            if (!CanMerge(a, b)) return null;
            int tier = a.Tier;

            if (a.IsBrown && b.IsBrown)
                return new MergeResult(new List<Orb>(), MergeKind.BrownClear);  // dissolve

            if (a.IsBrown || b.IsBrown)
            {
                // Brown absorbs any non-brown same-tier orb -> Brown (wasteful but legal).
                var waste = new Orb { Color = OrbColor.Brown, Tier = tier, X = a.X, Y = a.Y };
                return new MergeResult(new List<Orb> { waste }, MergeKind.ColorMix);
            }

            if (a.Color == b.Color && a.Tier < 5)
            {
                var up = new Orb { Color = a.Color, Tier = tier + 1, X = a.X, Y = a.Y };
                return new MergeResult(new List<Orb> { up }, MergeKind.TierMerge);
            }

            var mix = Mix(a.Color, b.Color);
            var result = new Orb { Color = mix, Tier = tier, X = a.X, Y = a.Y };
            return new MergeResult(new List<Orb> { result }, MergeKind.ColorMix);
        }

        public static OrbColor Mix(OrbColor ca, OrbColor cb)
        {
            if (ca == OrbColor.Brown || cb == OrbColor.Brown) return OrbColor.Brown;
            // Mixed + Mixed -> Brown (canon §3.3). This is the deliberate fix over ColorMixer.Mix.
            if (IsMixed(ca) && IsMixed(cb)) return OrbColor.Brown;
            // Normalize mixed colors to their base components for mixing.
            var ba = NormalizeBase(ca);
            var bb = NormalizeBase(cb);
            if (ba == bb) return ba;                                     // same base -> that base (wasteful, not brown)
            if ((ba == OrbColor.Cyan && bb == OrbColor.Magenta) || (ba == OrbColor.Magenta && bb == OrbColor.Cyan)) return OrbColor.Purple;
            if ((ba == OrbColor.Cyan && bb == OrbColor.Yellow) || (ba == OrbColor.Yellow && bb == OrbColor.Cyan)) return OrbColor.Green;
            if ((ba == OrbColor.Magenta && bb == OrbColor.Yellow) || (ba == OrbColor.Yellow && bb == OrbColor.Magenta)) return OrbColor.Orange;
            return OrbColor.Brown;                                       // unreachable with 3 bases
        }

        private static OrbColor NormalizeBase(OrbColor c)
        {
            switch (c)
            {
                case OrbColor.Purple: return OrbColor.Cyan;
                case OrbColor.Green: return OrbColor.Cyan;
                case OrbColor.Orange: return OrbColor.Magenta;
                default: return c;
            }
        }

        private static bool IsMixed(OrbColor c) => (int)c >= 6 && (int)c <= 8;
    }

    public enum MergeKind { TierMerge, ColorMix, BrownClear }

    public sealed class MergeResult
    {
        public List<Orb> Produced;
        public MergeKind Kind;
        public MergeResult(List<Orb> produced, MergeKind kind) { Produced = produced; Kind = kind; }
    }
}
