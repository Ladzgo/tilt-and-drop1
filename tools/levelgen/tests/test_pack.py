import gzip

import pytest

from tiltdrop.model import Level
from tiltdrop.pack import decode_level, encode_level, read_pack, write_pack, write_pack_gz


def lv(rows, gates, **kw):
    return Level.from_rows(rows, gates, **kw)


LEVELS = [
    lv(["....", "....", "11.2"], {"L": "..1", "R": "..2"}, moves=5, par=3, tier="N"),
    lv(["[a1]..#", "@.[i2].", "1.2."], {"T": "1...", "B": "..2."}, moves=9, par=7, tier="SH", mechanics=["stone", "anchor", "ice"]),
    lv(["." * 8] * 7 + ["12345612"], {"T": "65432112"}, moves=20, par=16, tier="E"),
]


def test_roundtrip(tmp_path):
    data = write_pack(LEVELS)
    back = read_pack(data, id_prefix="L", first_number=1)
    assert [b.to_json() | {"id": ""} for b in back] == [a.to_json() | {"id": ""} for a in LEVELS]
    assert back[0].id == "L1"
    gz = write_pack_gz(LEVELS, str(tmp_path / "p.tdp.gz"))
    assert gz == write_pack_gz(LEVELS, str(tmp_path / "q.tdp.gz"))  # reproducible
    assert len(read_pack(gz)) == 3
    assert gzip.decompress(gz) == data


def test_record_size():
    rec = encode_level(LEVELS[2])
    assert len(rec) == 5 + 64 + 16
    back, off = decode_level(rec)
    assert off == len(rec) and back.cells == LEVELS[2].cells


def test_corruption_detected():
    data = bytearray(write_pack(LEVELS))
    data[20] ^= 0xFF
    with pytest.raises(ValueError, match="CRC"):
        read_pack(bytes(data))
    with pytest.raises(ValueError, match="not a TDP"):
        read_pack(b"XXXX" + bytes(20))
