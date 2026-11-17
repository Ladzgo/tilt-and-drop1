"""Static HTML review page for curated levels (open in a browser, pick overrides)."""
from __future__ import annotations

import html

from .model import ANCHOR, BLOCK, ICE1, ICE2, STONE, WALL, Level

PALETTE = {1: "#e5484d", 2: "#3e8ef7", 3: "#f5c400", 4: "#30a46c", 5: "#8e4ec6", 6: "#f76b15"}
SYMBOL = {1: "●", 2: "▲", 3: "■", 4: "◆", 5: "★", 6: "✚"}


def _cell(c: int) -> str:
    k, col = c & 0xF0, c & 0x0F
    if c == WALL:
        return '<td class="w"></td>'
    if c == STONE:
        return '<td class="s"></td>'
    if k == BLOCK:
        return f'<td style="background:{PALETTE[col]}">{SYMBOL[col]}</td>'
    if k == ANCHOR:
        return f'<td class="a" style="background:{PALETTE[col]}">⚓</td>'
    if k in (ICE2, ICE1):
        return f'<td class="i" style="background:{PALETTE[col]}">❄</td>'
    return "<td></td>"


def board_html(lv: Level) -> str:
    def gate(e: str, i: int) -> str:
        v = lv.gates[e][i]
        return f'<td class="g" style="background:{PALETTE[v]}"></td>' if v else '<td class="n"></td>'

    rows = ['<tr><td class="n"></td>' + "".join(gate("T", x) for x in range(lv.w)) + '<td class="n"></td></tr>']
    for y in range(lv.h):
        rows.append("<tr>" + gate("L", y) + "".join(_cell(lv.cells[y * lv.w + x]) for x in range(lv.w)) + gate("R", y) + "</tr>")
    rows.append('<tr><td class="n"></td>' + "".join(gate("B", x) for x in range(lv.w)) + '<td class="n"></td></tr>')
    return '<table class="b">' + "".join(rows) + "</table>"


def review_page(levels: list[Level], title: str = "Tilt & Drop – level review") -> str:
    cards = []
    for lv in levels:
        m = lv.meta
        cards.append(
            f'<div class="card t{lv.tier}"><div class="hd">{html.escape(lv.id)} · {lv.tier} · par {lv.par} / {lv.moves} moves</div>'
            f"{board_html(lv)}<div class=\"ft\">{html.escape(str(m.get('beat', '')))} · diff {m.get('difficulty')} · bot fail {m.get('bot_fail')} · "
            f"sol {m.get('solutions_at_par')} · dead {m.get('dead_end_ratio')} · {html.escape(str(m.get('solution', '')))}</div></div>"
        )
    css = """body{font:13px system-ui;background:#111;color:#ddd;margin:16px}.grid{display:flex;flex-wrap:wrap;gap:12px}
.card{background:#1d1d1f;border-radius:8px;padding:8px;border-top:4px solid #555}.tE{border-color:#30a46c}.tN{border-color:#3e8ef7}
.tH{border-color:#f5c400}.tSH{border-color:#e5484d}.b{border-collapse:collapse;margin:6px 0}.b td{width:20px;height:20px;text-align:center;
font-size:12px;color:#fff;border:1px solid #2a2a2a;background:#2b2b30}.b td.w{background:#55555c}.b td.s{background:#8a7f72;border-radius:40%}
.b td.n{background:transparent;border-color:transparent}.b td.g{height:8px}.b td.a{outline:2px solid #ccc}.b td.i{outline:2px solid #bdf}
.hd{font-weight:600}.ft{color:#999;max-width:260px}"""
    return f"<!doctype html><meta charset=utf-8><title>{html.escape(title)}</title><style>{css}</style><h1>{html.escape(title)}</h1><div class=grid>{''.join(cards)}</div>"
