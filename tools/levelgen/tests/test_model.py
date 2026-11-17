import pytest

from tiltdrop.model import ANCHOR, BLOCK, ICE1, Level, cell_token, parse_row


def test_tokens_roundtrip():
    row = "#.@1[a2][i3][j4]6"
    cells = parse_row(row)
    assert "".join(cell_token(c) for c in cells) == row
    assert cells[4] == ANCHOR | 2 and cells[6] == ICE1 | 4 and cells[3] == BLOCK | 1


@pytest.mark.parametrize("row", ["x", "7", "[z1]", "[a9]", "[a]"])
def test_bad_rows(row):
    with pytest.raises(ValueError):
        parse_row(row)


def test_level_validation():
    with pytest.raises(ValueError):
        Level.from_rows(["11"] * 2)  # too small
    with pytest.raises(ValueError):
        Level.from_json({"w": 3, "h": 3, "grid": ["...", "..."]})
    with pytest.raises(ValueError):
        Level.from_json({"w": 3, "h": 3, "grid": ["...", "...", "...."]})
    with pytest.raises(ValueError):
        Level.from_rows(["...", "...", "..."], {"T": "...."})
    with pytest.raises(ValueError):
        Level.from_rows(["...", "...", "..."], {"T": "..x"})
    with pytest.raises(ValueError):
        cell_token(0x99)


def test_json_roundtrip_and_helpers():
    lv = Level.from_rows(["1.[a2]", "@.[i1]", "..."], {"L": "1..", "R": "..2"}, moves=4, par=2, id="x", meta={"k": 1})
    back = Level.from_json(lv.to_json())
    assert back == lv
    assert lv.colors() == {1, 2} and lv.gate_colors() == {1, 2}
    assert lv.count_colored() == 3
    assert lv.detected_mechanics() == ["stone", "anchor", "ice"]
