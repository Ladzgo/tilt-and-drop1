"""Binary level pack format `.tdp` (docs/TECH.md §3.3), gzip-compressed on disk.

Header : b"TDP1" | u16 version | u16 flags | u32 count | u32 index_offset
Record : u8 (w<<4|h) | u8 moves | u8 par | u8 tier | u8 mechanics bitmask
         | u8[w*h] cells | u8[w+h] gates (nibbles, order T(x) R(y) B(x) L(y),
           high nibble first; 2*(w+h) nibbles)
Index  : u32 offset per record (from file start)
Footer : u32 CRC32 of everything before
All integers little-endian.
"""
from __future__ import annotations

import gzip
import struct
import zlib

from .model import Level

MAGIC = b"TDP1"
VERSION = 1
TIER_CODE = {"E": 0, "N": 1, "H": 2, "SH": 3}
CODE_TIER = {v: k for k, v in TIER_CODE.items()}
MECH_BITS = {"stone": 1, "anchor": 2, "ice": 4, "sticky": 8, "oneway": 16, "bomb": 32}


def _gate_nibbles(lv: Level) -> bytes:
    nib = list(lv.gates["T"]) + list(lv.gates["R"]) + list(lv.gates["B"]) + list(lv.gates["L"])
    out = bytearray()
    for i in range(0, len(nib), 2):
        out.append((nib[i] << 4) | nib[i + 1])
    return bytes(out)


def encode_level(lv: Level) -> bytes:
    mech = 0
    for m in lv.mechanics:
        mech |= MECH_BITS.get(m, 0)
    head = bytes(((lv.w << 4) | lv.h, lv.moves, lv.par, TIER_CODE[lv.tier], mech))
    return head + lv.cells + _gate_nibbles(lv)


def decode_level(buf: bytes, off: int = 0, level_id: str = "") -> tuple[Level, int]:
    wh, moves, par, tier, mech = buf[off : off + 5]
    w, h = wh >> 4, wh & 0xF
    off += 5
    cells = bytes(buf[off : off + w * h])
    off += w * h
    n_g = w + h
    nib: list[int] = []
    for b in buf[off : off + n_g]:
        nib += [b >> 4, b & 0xF]
    off += n_g
    t, r = nib[:w], nib[w : w + h]
    bt, lt = nib[w + h : 2 * w + h], nib[2 * w + h : 2 * w + 2 * h]
    gates = {"T": bytes(t), "R": bytes(r), "B": bytes(bt), "L": bytes(lt)}
    mechanics = [m for m, bit in MECH_BITS.items() if mech & bit]
    lv = Level(w, h, cells, gates, moves=moves, par=par, id=level_id, tier=CODE_TIER[tier], mechanics=mechanics)
    return lv, off


def write_pack(levels: list[Level], flags: int = 0) -> bytes:
    body = bytearray()
    offsets = []
    header_len = 16
    for lv in levels:
        offsets.append(header_len + len(body))
        body += encode_level(lv)
    index_offset = header_len + len(body)
    out = bytearray(MAGIC + struct.pack("<HHII", VERSION, flags, len(levels), index_offset))
    out += body
    for o in offsets:
        out += struct.pack("<I", o)
    out += struct.pack("<I", zlib.crc32(bytes(out)))
    return bytes(out)


def read_pack(data: bytes, id_prefix: str = "", first_number: int = 1) -> list[Level]:
    if data[:2] == b"\x1f\x8b":
        data = gzip.decompress(data)
    if data[:4] != MAGIC:
        raise ValueError("not a TDP pack")
    (crc,) = struct.unpack_from("<I", data, len(data) - 4)
    if zlib.crc32(data[:-4]) != crc:
        raise ValueError("pack CRC mismatch")
    version, _flags, count, index_offset = struct.unpack_from("<HHII", data, 4)
    if version != VERSION:
        raise ValueError(f"unsupported pack version {version}")
    levels = []
    for i in range(count):
        (off,) = struct.unpack_from("<I", data, index_offset + 4 * i)
        lv, _ = decode_level(data, off, f"{id_prefix}{first_number + i}")
        levels.append(lv)
    return levels


def write_pack_gz(levels: list[Level], path: str) -> bytes:
    raw = write_pack(levels)
    # mtime=0 -> byte-identical output for identical input (reproducible builds)
    gz = gzip.compress(raw, compresslevel=9, mtime=0)
    with open(path, "wb") as f:
        f.write(gz)
    return gz
