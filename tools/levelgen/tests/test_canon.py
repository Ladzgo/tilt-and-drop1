import random

from tiltdrop.canon import Deduper, canonical_key, images, level_hash, level_key, level_state, mirror, rot90
from tiltdrop.model import ANCHOR, BLOCK, DOWN, EMPTY, GRAVITY_EDGE, ICE2, STONE, WALL, Level
from tiltdrop.sim import settle, topology


def rand_state(rng, special=False):
    w, h = rng.randint(3, 8), rng.randint(3, 8)
    pool = [EMPTY] * 6 + [WALL, STONE] + [BLOCK | c for c in (1, 2, 3)] * 2
    if special:
        pool += [ANCHOR | 1, ICE2 | 2]
    cells = bytes(rng.choice(pool) for _ in range(w * h))
    gates = {e: bytes(rng.choice([0, 0, 1, 2, 3]) for _ in range(w if e in "TB" else h)) for e in "TRBL"}
    return w, h, cells, gates, rng.randrange(4)


def settled(s):
    w, h, cells, gates, g = s
    buf = bytearray(cells)
    settle(buf, topology(w, h), g, gates[GRAVITY_EDGE[g]])
    return w, h, bytes(buf), gates, g


def test_transforms_commute_with_settle():
    """Validates the gate/gravity mapping of rot90 and mirror."""
    rng = random.Random(5)
    for i in range(1500):
        s = rand_state(rng, special=i % 2 == 0)
        for t in (rot90, mirror):
            assert settled(t(s))[2] == t(settled(s))[2]


def test_group_closure():
    rng = random.Random(6)
    s = rand_state(rng)
    cur = s
    for _ in range(4):
        cur = rot90(cur)
    assert cur == s
    assert mirror(mirror(s)) == s
    assert len({(im[2], im[4]) for im in images(s)}) <= 8


def test_key_invariant_under_symmetry_and_color_permutation():
    rng = random.Random(7)
    for _ in range(300):
        s = rand_state(rng)
        k = canonical_key(s)
        for im in images(s):
            assert canonical_key(im) == k
        perm = {1: 3, 2: 1, 3: 2}
        w, h, cells, gates, g = s
        pc = bytes((c & 0xF0) | perm[c & 0xF] if (c & 0xF0) == BLOCK else c for c in cells)
        pg = {e: bytes(perm[v] if v else 0 for v in gates[e]) for e in gates}
        assert canonical_key((w, h, pc, pg, g)) == k


def test_distinct_levels_distinct_keys():
    a = Level.from_rows(["....", "....", "11.2"], {"L": "..1", "R": "..2"})
    b = Level.from_rows(["....", "....", "1.12"], {"L": "..1", "R": "..2"})
    assert level_key(a) != level_key(b)
    # mirror image with colors swapped is the same puzzle
    c = Level.from_rows(["....", "....", "1.22"], {"L": "..1", "R": "..2"})
    assert level_hash(c) == level_hash(Level.from_rows(["....", "....", "11.2"], {"L": "..1", "R": "..2"}))
    assert len(level_hash(a)) == 16


def test_deduper_exact_and_near():
    d = Deduper()
    a = Level.from_rows(["....", "....", "11.2"], {"L": "..1", "R": "..2"})
    assert d.add(a)
    assert not d.add(Level.from_rows(["....", "....", "1.22"], {"L": "..1", "R": "..2"}))
    # the state one move (CCW) after `a`, re-oriented so gravity is Down, as a new level
    from tiltdrop.model import CCW
    from tiltdrop.sim import step

    cells, g, _ = step(a, a.cells, DOWN, CCW)
    st = level_state(a, cells, g)
    while st[4] != DOWN:
        st = rot90(st)
    near = Level(st[0], st[1], st[2], dict(st[3]))
    assert level_key(near) != level_key(a)
    assert d.is_duplicate(near)
    assert d.add(Level.from_rows(["#...", "....", "11.2"], {"L": "..1", "R": "..2"}))
