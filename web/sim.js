// Tilt & Drop rules in JavaScript (mirror of tools/levelgen/tiltdrop/sim.py).
// Parity with Python/C# is enforced by test-golden.mjs over /shared/golden.
"use strict";
const TD = (() => {
  const EMPTY = 0x00, WALL = 0x10, BLOCK = 0x20, STONE = 0x30, ANCHOR = 0x40, ICE2 = 0x50, ICE1 = 0x60;
  const DX = [0, 1, 0, -1], DY = [1, 0, -1, 0];          // Down, Right, Up, Left
  const DIRS = "DRUL", EDGES = "TRBL";
  const GRAVITY_EDGE = [2, 1, 0, 3];                     // Down->B, Right->R, Up->T, Left->L (edge index in TRBL)
  const rotate = (g, rot) => rot === "CW" ? (g + 1) & 3 : (g + 3) & 3;
  const isColored = c => { const k = c & 0xF0; return k === BLOCK || k === ANCHOR || k === ICE2 || k === ICE1; };
  const isPiece = c => c !== EMPTY && c !== WALL;

  const topoCache = new Map();
  function topology(w, h) {
    const key = w * 16 + h;
    if (topoCache.has(key)) return topoCache.get(key);
    const order = [], next = [], lane = [];
    for (let g = 0; g < 4; g++) {
      const idx = [...Array(w * h).keys()];
      const k = i => { const x = i % w, y = (i / w) | 0;
        return g === 0 ? (h - 1 - y) * 64 + x : g === 1 ? (w - 1 - x) * 64 + y : g === 2 ? y * 64 + x : x * 64 + y; };
      idx.sort((a, b) => k(a) - k(b));
      order.push(idx);
      const nx = [], ln = [];
      for (let i = 0; i < w * h; i++) {
        const x = i % w, y = (i / w) | 0, tx = x + DX[g], ty = y + DY[g];
        nx.push(tx >= 0 && tx < w && ty >= 0 && ty < h ? ty * w + tx : -1);
        ln.push(g === 0 || g === 2 ? x : y);
      }
      next.push(nx); lane.push(ln);
    }
    const t = { w, h, order, next, lane };
    topoCache.set(key, t);
    return t;
  }

  // Settles cells in place. ids/events optional. Returns number of exits.
  function settle(cells, topo, g, gateLine, ids, events) {
    const order = topo.order[g], next = topo.next[g], lane = topo.lane[g], edge = EDGES[GRAVITY_EDGE[g]];
    const n = cells.length;
    let exits = 0, pass = 0, changed = true;
    while (changed) {
      changed = false;
      for (const p of order) {
        const c = cells[p], k = c & 0xF0;
        if (k !== BLOCK && k !== STONE) continue;
        let pos = p;
        for (;;) { const nx = next[pos]; if (nx < 0 || cells[nx] !== EMPTY) break; pos = nx; }
        const moved = pos !== p;
        if (moved) {
          cells[pos] = c; cells[p] = EMPTY; changed = true;
          if (ids) { ids[pos] = ids[p]; ids[p] = 0; }
          if (events) events.push({ t: "move", pass, id: ids ? ids[pos] : 0, from: p, to: pos });
        }
        const nn = next[pos];
        if (nn < 0) {
          const col = c & 0x0F;
          if (k === BLOCK && gateLine[lane[pos]] === col) {
            cells[pos] = EMPTY; changed = true; exits++;
            let pid = 0;
            if (ids) { pid = ids[pos]; ids[pos] = 0; }
            if (events) events.push({ t: "exit", pass, id: pid, edge, lane: lane[pos] });
            const anchor = ANCHOR | col;
            for (let i = 0; i < n; i++) if (cells[i] === anchor) {
              cells[i] = BLOCK | col;
              if (events) events.push({ t: "release", pass, id: ids ? ids[i] : 0 });
            }
          }
        } else if (moved) {
          const t = cells[nn], tk = t & 0xF0;
          if (tk === ICE2 || tk === ICE1) {
            let hp;
            if (tk === ICE2) { cells[nn] = ICE1 | (t & 0x0F); hp = 1; } else { cells[nn] = BLOCK | (t & 0x0F); hp = 0; }
            if (events) events.push({ t: "hit", pass, id: ids ? ids[nn] : 0, hp });
          }
        }
      }
      pass++;
    }
    return exits;
  }

  // ---- level parsing ----
  function parseRow(row) {
    const out = [];
    for (let i = 0; i < row.length; i++) {
      const ch = row[i];
      if (ch === "[") { const p = row[i + 1], col = +row[i + 2]; out.push((p === "a" ? ANCHOR : p === "i" ? ICE2 : ICE1) | col); i += 3; continue; }
      out.push(ch === "." ? EMPTY : ch === "#" ? WALL : ch === "@" ? STONE : BLOCK | +ch);
    }
    return out;
  }
  function token(c) {
    const k = c & 0xF0, col = c & 0x0F;
    if (c === EMPTY) return "."; if (c === WALL) return "#"; if (c === STONE) return "@";
    if (k === BLOCK) return String(col);
    return "[" + (k === ANCHOR ? "a" : k === ICE2 ? "i" : "j") + col + "]";
  }
  function parseLevel(d) {
    const cells = [];
    for (const r of d.grid) cells.push(...parseRow(r));
    const gates = {};
    for (const e of EDGES) { const n = e === "T" || e === "B" ? d.w : d.h; const s = (d.gates && d.gates[e]) || ".".repeat(n);
      gates[e] = [...s].map(ch => ch === "." ? 0 : +ch); }
    return { id: d.id || "", w: d.w, h: d.h, cells, gates, moves: d.moves || 0, par: d.par || 0, tier: d.tier || "N" };
  }
  const rows = (cells, w, h) => Array.from({ length: h }, (_, y) => cells.slice(y * w, y * w + w).map(token).join(""));
  const initialIds = cells => { let n = 1; return cells.map(c => isPiece(c) ? n++ : 0); };
  const hasColored = cells => cells.some(isColored);

  class Game {
    constructor(level) {
      this.level = level; this.topo = topology(level.w, level.h);
      this.cells = level.cells.slice(); this.ids = initialIds(level.cells);
      this.gravity = 0; this.movesLeft = level.moves; this.movesUsed = 0; this.update();
    }
    update() { this.status = !hasColored(this.cells) ? "won" : this.movesLeft <= 0 ? "lost" : "playing"; }
    settleNow() { const ev = []; settle(this.cells, this.topo, this.gravity, this.level.gates[EDGES[GRAVITY_EDGE[this.gravity]]], this.ids, ev); return ev; }
    rotate(rot) {
      if (this.status !== "playing") throw new Error("cannot rotate: " + this.status);
      this.gravity = rotate(this.gravity, rot); this.movesLeft--; this.movesUsed++;
      const ev = this.settleNow(); this.update(); return ev;
    }
    hammer(x, y) {
      if (this.status !== "playing") throw new Error("cannot hammer");
      const i = y * this.level.w + x;
      if (!isPiece(this.cells[i])) throw new Error("not a piece");
      const id = this.ids[i]; this.cells[i] = EMPTY; this.ids[i] = 0;
      const ev = [{ t: "hammer", pass: 0, id, from: i }, ...this.settleNow()]; this.update(); return ev;
    }
    addMoves(n) { if (this.status === "won") throw new Error("won"); this.movesLeft += n; this.update(); }
    clone() { const g = Object.create(Game.prototype); Object.assign(g, this, { cells: this.cells.slice(), ids: this.ids.slice() }); return g; }
    colored() { return this.cells.filter(isColored).length; }
  }

  // BFS hint: minimum moves from (cells, gravity) and the first rotation of an optimal line.
  function hint(level, cells, gravity, maxDepth, cap = 80000) {
    const topo = topology(level.w, level.h);
    const key = (c, g) => c.join(",") + "|" + g;
    const start = { c: cells.slice(), g: gravity, first: null };
    if (!hasColored(start.c)) return { moves: 0, first: null };
    const seen = new Set([key(start.c, start.g)]);
    let frontier = [start];
    for (let d = 1; d <= maxDepth && frontier.length; d++) {
      const nxt = [];
      for (const s of frontier) for (const rot of ["CW", "CCW"]) {
        const g = rotate(s.g, rot), c = s.c.slice();
        settle(c, topo, g, level.gates[EDGES[GRAVITY_EDGE[g]]]);
        const first = s.first || rot;
        if (!hasColored(c)) return { moves: d, first };
        const k = key(c, g);
        if (seen.has(k)) continue;
        seen.add(k); nxt.push({ c, g, first });
        if (seen.size > cap) return null;
      }
      frontier = nxt;
    }
    return null;
  }

  function goldenEvent(e, w) {
    switch (e.t) {
      case "move": return `M${e.pass}:${e.id}:${e.from % w},${(e.from / w) | 0}>${e.to % w},${(e.to / w) | 0}`;
      case "exit": return `X${e.pass}:${e.id}:${e.edge}${e.lane}`;
      case "hit": return `H${e.pass}:${e.id}:${e.hp}`;
      case "release": return `R${e.pass}:${e.id}`;
      default: return `K:${e.id}:${e.from % w},${(e.from / w) | 0}`;
    }
  }

  return { EMPTY, WALL, BLOCK, STONE, ANCHOR, ICE2, ICE1, DX, DY, DIRS, EDGES, GRAVITY_EDGE,
    rotate, isColored, isPiece, topology, settle, parseLevel, rows, Game, hint, goldenEvent };
})();
if (typeof module !== "undefined") module.exports = TD;
