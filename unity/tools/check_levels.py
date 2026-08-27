#!/usr/bin/env python3
"""
Chroma Vale — Level Solvability Checker

Implements the exact merge rules from MergeRules.cs + BoardController.cs
and BFS-searches every level JSON for a valid solution path.

Usage:
    python check_levels.py                    # check all levels
    python check_levels.py --level 4          # check level 4 only
    python check_levels.py --level 4 --trace   # show solution moves
"""

import json
import os
import sys
import argparse
from collections import deque
from itertools import combinations

# ── Color mapping (matches OrbColor enum in OrbData.cs) ──
COLOR_MAP = {
    "cyan": 0, "magenta": 1, "yellow": 2,
    "purple": 6, "green": 7, "orange": 8, "brown": 9,
}
COLOR_NAMES = {v: k for k, v in COLOR_MAP.items()}

# Tier mapping (1-indexed: JSON "tier":1 → T1)
def parse_tier(t):
    return int(t)

def tier_name(t):
    return f"T{t}"

def color_name(c):
    return COLOR_NAMES.get(c, f"?{c}")

# ── Orb representation: (color:int, tier:int) tuple ──
def fmt_orb(orb):
    return f"{color_name(orb[0])}/{tier_name(orb[1])}"

# ── Mix table (from MergeRules.cs MixColors, lines 143-181) ──
def mix_colors(a, b):
    """Mix two OrbColor values. Returns result color or Brown(9)."""
    if a > b:
        a, b = b, a
    table = {
        (0, 1): 6,   # Cyan + Magenta → Purple
        (0, 2): 7,   # Cyan + Yellow → Green
        (1, 2): 8,   # Magenta + Yellow → Orange
        # All other pairs → Brown
    }
    return table.get((a, b), 9)

# ── CanMerge (from MergeRules.cs:53-70) ──
def can_merge(a, b, mixing_enabled):
    """Check if two orbs can merge."""
    if a is None or b is None:
        return False
    # Must be same tier
    if a[1] != b[1]:
        return False
    # Brown + non-Brown = invalid
    a_brown = (a[0] == 9)
    b_brown = (b[0] == 9)
    if a_brown != b_brown:
        return False
    # Same color, both T5 = capped
    if a[0] == b[0] and a[1] == 5:
        return False
    # Mixing gate: when disabled, cross-color pairs cannot merge
    if not mixing_enabled and a[0] != b[0]:
        return False
    return True

# ── TryMerge (from MergeRules.cs:81-132) ──
def try_merge(a, b, mixing_enabled):
    """
    Attempt to merge two orbs. Returns (result_orb_or_None, consumes_source, consumes_target)
    or None if invalid.
    """
    if a is None or b is None:
        return None

    # 1. Brown rules
    a_brown = (a[0] == 9)
    b_brown = (b[0] == 9)
    if a_brown or b_brown:
        if a_brown and b_brown:
            return (None, True, True)  # Brown + Brown → cleared
        return None  # Brown + non-Brown → invalid

    # 2. Must be same tier
    if a[1] != b[1]:
        return None

    # 3. Same color → tier merge
    if a[0] == b[0]:
        if a[1] < 5:
            return ((a[0], a[1] + 1), True, True)
        return None  # T5 capped

    # 4. Mixing gate
    if not mixing_enabled:
        return None

    # 5. Different colors, same tier → color mix (result keeps source tier)
    mixed = mix_colors(a[0], b[0])
    if mixed == 9:  # Brown
        return ((9, a[1]), True, True)  # BrownProduction
    return ((mixed, a[1]), True, True)  # ColorMix

# ── State: sorted tuple of (color, tier) orbs ──
def state_key(orbs):
    return tuple(sorted(orbs))

def apply_merge(orbs, i, j, mixing_enabled):
    """
    Apply a merge between orbs[i] and orbs[j].
    Returns new_orbs list or None if invalid.
    """
    a, b = orbs[i], orbs[j]
    result = try_merge(a, b, mixing_enabled)
    if result is None:
        return None
    new_orb, cons_src, cons_tgt = result
    # Build new orb list: remove both, add result (if any)
    remaining = [orbs[k] for k in range(len(orbs)) if k != i and k != j]
    if new_orb is not None:
        remaining.append(new_orb)
    return remaining

def check_targets_satisfied(orbs, targets):
    """
    Check if all targets are satisfied (position-agnostic, like BoardController.CheckWin).
    Each target must match a DIFFERENT orb.
    """
    orb_counts = {}
    for o in orbs:
        key = (o[0], o[1])
        orb_counts[key] = orb_counts.get(key, 0) + 1

    target_counts = {}
    for t in targets:
        key = (t[0], t[1])
        target_counts[key] = target_counts.get(key, 0) + 1

    for key, needed in target_counts.items():
        available = orb_counts.get(key, 0)
        if available < needed:
            return False
    return True

# ── BFS solvability search ──
def solve_level(orbs, targets, mixing_enabled, max_moves=None):
    """
    BFS to find the minimum-move solution.
    Returns (solvable:bool, min_moves:int, solution_path:list or None)
    """
    if max_moves is None:
        max_moves = len(orbs)  # can't have more merges than initial orb count

    initial = tuple(sorted(orbs))
    if check_targets_satisfied(list(initial), targets):
        return (True, 0, [])

    visited = {state_key(list(initial))}
    queue = deque([(list(initial), 0, [])])

    while queue:
        state, moves, path = queue.popleft()
        if moves >= max_moves:
            continue

        n = len(state)
        for i in range(n):
            for j in range(i + 1, n):
                if not can_merge(state[i], state[j], mixing_enabled):
                    continue
                new_orbs = apply_merge(state, i, j, mixing_enabled)
                if new_orbs is None:
                    continue

                key = state_key(new_orbs)
                if key in visited:
                    continue
                visited.add(key)

                new_path = path + [(state[i], state[j], new_orbs[-1] if new_orbs else None)]

                if check_targets_satisfied(new_orbs, targets):
                    return (True, moves + 1, new_path)

                queue.append((new_orbs, moves + 1, new_path))

    return (False, -1, None)

# ── Dead-end detection: can the level be made unwinnable? ──
def check_dead_ends(orbs, targets, mixing_enabled, max_moves=None):
    """
    Check if there exist move sequences that make the level unsolvable.
    Returns list of dead-end states (as orb multisets) that can be reached
    but from which no solution is possible.
    """
    if max_moves is None:
        max_moves = len(orbs)

    dead_ends = []
    initial = tuple(sorted(orbs))
    visited = {state_key(list(initial))}
    queue = deque([(list(initial), 0)])

    while queue:
        state, moves = queue.popleft()
        if moves >= max_moves:
            continue

        n = len(state)
        has_valid_move = False
        for i in range(n):
            for j in range(i + 1, n):
                if not can_merge(state[i], state[j], mixing_enabled):
                    continue
                new_orbs = apply_merge(state, i, j, mixing_enabled)
                if new_orbs is None:
                    continue
                has_valid_move = True

                key = state_key(new_orbs)
                if key in visited:
                    continue
                visited.add(key)

                if not check_targets_satisfied(new_orbs, targets):
                    queue.append((new_orbs, moves + 1))

        # If no valid moves and targets not satisfied → dead end
        if not has_valid_move and not check_targets_satisfied(state, targets):
            dead_ends.append(state)

    return dead_ends

# ── Level JSON parsing ──
def parse_level_json(filepath):
    """Parse a level JSON file into (orbs, targets, mixing_enabled, par_moves, grid_size)."""
    with open(filepath) as f:
        data = json.load(f)

    # DESIGN_CANON §3.1.1: mixing is the default on; only an explicit
    # false/0 disables it. Accept both camelCase (game fixtures) and
    # snake_case (validator fixtures) spellings.
    raw_mixing = data.get("mixingEnabled", data.get("mixing_enabled", None))
    if raw_mixing is None:
        mixing = True  # canon default: mixing ON
    elif isinstance(raw_mixing, bool):
        mixing = raw_mixing
    else:
        mixing = str(raw_mixing).lower() not in ("false", "0", "off", "no")

    raw_par = data.get("parMoves", data.get("par_moves", None))
    par = int(raw_par) if raw_par is not None else 0
    grid_w = data.get("gridWidth", data.get("grid", {}).get("width", 5))
    grid_h = data.get("gridHeight", data.get("grid", {}).get("height", 5))

    # Parse orbs
    orbs = []
    for orb in data.get("orbs", []):
        color = COLOR_MAP.get(orb["color"].lower())
        tier = parse_tier(orb["tier"])
        orbs.append((color, tier))

    # Parse targets
    targets = []
    for tgt in data.get("targets", []):
        color = COLOR_MAP.get(tgt["color"].lower())
        tier = parse_tier(tgt["tier"])
        targets.append((color, tier))

    return orbs, targets, mixing, par, (grid_w, grid_h)

# ── Main ──
def main():
    parser = argparse.ArgumentParser(description="Chroma Vale level solvability checker")
    parser.add_argument("--level", type=int, help="Check a specific level (1-10)")
    parser.add_argument("--trace", action="store_true", help="Show solution moves")
    parser.add_argument("--deadends", action="store_true", help="Also check for dead-end states")
    parser.add_argument("--all", action="store_true", help="Check all levels")
    args = parser.parse_args()

    # Find level files (repo root is one level up from tools/)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.dirname(script_dir)
    levels_dir = os.path.join(repo_root, "Assets", "_Project", "Resources", "Levels")

    if not os.path.isdir(levels_dir):
        print(f"ERROR: Levels directory not found: {levels_dir}")
        sys.exit(1)

    level_files = {}
    for f in sorted(os.listdir(levels_dir)):
        if f.startswith("level_") and f.endswith(".json"):
            num = int(f.replace("level_", "").replace(".json", ""))
            level_files[num] = os.path.join(levels_dir, f)

    if args.level:
        if args.level not in level_files:
            print(f"ERROR: Level {args.level} not found. Available: {sorted(level_files.keys())}")
            sys.exit(1)
        check_levels = [args.level]
    else:
        check_levels = sorted(level_files.keys())

    print(f"{'Level':<8} {'Name':<20} {'Grid':<8} {'Orbs':<6} {'Targets':<10} {'Mix':<5} {'Par':<5} {'Solvable':<10} {'MinMoves':<10}")
    print("=" * 95)

    all_pass = True
    for num in check_levels:
        filepath = level_files[num]
        orbs, targets, mixing, par, grid = parse_level_json(filepath)

        # Get level name from JSON
        with open(filepath) as f:
            data = json.load(f)
        name = data.get("displayName", data.get("name", "?"))

        # Solve
        solvable, min_moves, solution = solve_level(orbs, targets, mixing)

        status = "✅ YES" if solvable else "❌ NO"
        moves_str = str(min_moves) if solvable else "—"
        par_str = f"{par}" if par else "?"

        print(f"L{num:<7} {name:<20} {grid[0]}×{grid[1]:<5} {len(orbs):<6} {len(targets):<10} {'ON' if mixing else 'OFF':<5} {par_str:<5} {status:<10} {moves_str:<10}")

        if not solvable:
            all_pass = False
            print(f"         ⚠️  UNSOLVABLE! Targets: {', '.join(fmt_orb(t) for t in targets)}")
            print(f"         Initial orbs: {', '.join(fmt_orb(o) for o in orbs)}")
        elif min_moves > par:
            print(f"         ⚠️  Min moves ({min_moves}) exceeds par ({par})! Par is too tight.")

        if args.trace and solution:
            print(f"\n         Solution ({len(solution)} moves):")
            for i, (a, b, result) in enumerate(solution):
                if result:
                    print(f"           {i+1}. {fmt_orb(a)} + {fmt_orb(b)} → {fmt_orb(result)}")
                else:
                    print(f"           {i+1}. {fmt_orb(a)} + {fmt_orb(b)} → (cleared)")
            print()

        if args.deadends:
            dead_ends = check_dead_ends(orbs, targets, mixing)
            if dead_ends:
                print(f"         ⚠️  {len(dead_ends)} dead-end state(s) reachable:")
                for de in dead_ends[:5]:
                    orbs_str = ', '.join(fmt_orb(o) for o in de)
                    print(f"           [{orbs_str}]")
                if len(dead_ends) > 5:
                    print(f"           ... and {len(dead_ends) - 5} more")
            else:
                print(f"         ✅ No dead-ends (every reachable state is either solved or has a path to solution)")

    print()
    if all_pass:
        print("🎉 ALL LEVELS SOLVABLE")
    else:
        print("💥 SOME LEVELS UNSOLVABLE — fix required!")
        sys.exit(1)

if __name__ == "__main__":
    main()
