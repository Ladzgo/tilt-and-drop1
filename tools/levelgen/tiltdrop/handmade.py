"""Hand-authored tutorial levels (scripted hand hints in the game)."""

HANDMADE = {
    # 1: one swipe right - the block slides into its gate
    1: {"w": 4, "h": 4, "grid": ["....", "....", "....", ".1.."], "gates": {"R": "...1"}, "hint": "CW"},
    # 2: two rotations the same way; stacked blocks cascade out of one gate
    2: {"w": 4, "h": 4, "grid": ["#...", "#...", "#..1", "#..1"], "gates": {"T": "...1"}, "hint": "CW,CW"},
    # 3: full spin - teaches that every rotation changes "down"
    3: {"w": 4, "h": 4, "grid": ["....", "#...", "....", ".1.#"], "gates": {"L": "1..."}, "hint": "CW,CW,CW"},
}
