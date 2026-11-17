// Slide Out rules in JavaScript (mirror of tools/levelgen/slide/sim.py + solver.py).
"use strict";
const SL = (() => {
  const DX = [1, 0, -1, 0], DY = [0, 1, 0, -1], ARROWS = ">v<^";
  function parseLevel(d) {
    const cells = [];
    for (const r of d.grid) for (let x = 0; x < d.w; x++) {
      const t = r.slice(2 * x, 2 * x + 2);
      cells.push(t === ".." ? null : t === "##" ? "#" : [+t[0], ARROWS.indexOf(t[1])]);
    }
    const lv = { id: d.id || "", w: d.w, h: d.h, cells, capacity: d.capacity || 5, tier: d.tier || "N", solution: (d.meta && d.meta.solution) || d.solution || [] };
    lv.blocks = cells.map((c, i) => Array.isArray(c) ? i : -1).filter(i => i >= 0);
    lv.index = new Map(lv.blocks.map((c, k) => [c, k]));
    return lv;
  }
  // BigInt-free mask: up to 30 blocks fit in a 32-bit int
  const has = (m, k) => (m >>> k) & 1;
  function blocker(lv, removed, k) {
    const i = lv.blocks[k], d = lv.cells[i][1];
    let x = i % lv.w, y = Math.floor(i / lv.w);
    for (;;) {
      x += DX[d]; y += DY[d];
      if (x < 0 || x >= lv.w || y < 0 || y >= lv.h) return null;
      const j = y * lv.w + x, c = lv.cells[j];
      if (c === null) continue;
      if (Array.isArray(c) && has(removed, lv.index.get(j))) continue;
      return j;
    }
  }
  function applyTap(lv, removed, tray, k) {
    if (has(removed, k) || blocker(lv, removed, k) !== null) return null;
    const colour = lv.cells[lv.blocks[k]][0];
    let t = tray.concat([colour]), matched = false;
    if (t.filter(c => c === colour).length >= 3) {
      let n = 0; t = t.filter(c => (c === colour && n < 3) ? (n++, false) : true); matched = true;
    }
    return { removed: (removed | (1 << k)) >>> 0, tray: t, matched };
  }
  const full = lv => lv.blocks.length >= 32 ? 0xFFFFFFFF : ((1 << lv.blocks.length) >>> 0) - 1;
  function statusOf(lv, removed, tray, cap) {
    if ((removed >>> 0) === (full(lv) >>> 0) && tray.length === 0) return "won";
    if (tray.length >= cap) return "lost";
    return "playing";
  }
  class Game {
    constructor(lv) { this.level = lv; this.removed = 0; this.tray = []; this.capacity = lv.capacity; this.taps = 0; this.status = statusOf(lv, 0, [], this.capacity); }
    tapCell(cell) {
      if (this.status !== "playing") throw new Error("cannot tap: " + this.status);
      if (!this.level.index.has(cell)) return null;
      const k = this.level.index.get(cell);
      if (has(this.removed, k)) return null;
      const r = applyTap(this.level, this.removed, this.tray, k);
      if (!r) return "bump";
      this.removed = r.removed; this.tray = r.tray; this.taps++;
      this.status = statusOf(this.level, this.removed, this.tray, this.capacity);
      return r.matched ? "match" : "exit";
    }
    addSlots(n = 2) { if (this.status === "won") throw new Error("won"); this.capacity += n; this.status = statusOf(this.level, this.removed, this.tray, this.capacity); }
    clone() { const g = Object.create(Game.prototype); Object.assign(g, this, { tray: this.tray.slice() }); return g; }
  }
  // memoised DFS (same semantics as solver.py): solvable, reachable state count, one winning order
  function solve(lv, removed = 0, tray = [], cap = null, nodeCap = 400000) {
    cap = cap || lv.capacity;
    const n = lv.blocks.length, memo = new Map();
    const key = (rm, tr) => rm + "|" + tr.slice().sort((a, b) => a - b).join("");
    function win(rm, tr) {
      const k = key(rm, tr);
      if (memo.has(k)) return memo.get(k);
      const st = statusOf(lv, rm, tr, cap);
      if (st !== "playing") { memo.set(k, st === "won"); return st === "won"; }
      if (memo.size > nodeCap) return false;
      memo.set(k, false);
      let ok = false;
      for (let b = 0; b < n; b++) { const r = applyTap(lv, rm, tr, b); if (r && win(r.removed, r.tray)) ok = true; }
      memo.set(k, ok);
      return ok;
    }
    const ok = win(removed, tray), order = [];
    if (ok) {
      let rm = removed, tr = tray;
      while (statusOf(lv, rm, tr, cap) === "playing") {
        let moved = false;
        for (let b = 0; b < n; b++) {
          const r = applyTap(lv, rm, tr, b);
          if (r && (memo.get(key(r.removed, r.tray)) || statusOf(lv, r.removed, r.tray, cap) === "won")) { order.push(b); rm = r.removed; tr = r.tray; moved = true; break; }
        }
        if (!moved) break;
      }
    }
    return { solvable: ok, states: memo.size, order };
  }
  return { DX, DY, parseLevel, blocker, applyTap, statusOf, Game, solve };
})();
if (typeof module !== "undefined") module.exports = SL;
