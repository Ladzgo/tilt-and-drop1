// Domino Run rules in JavaScript (mirror of tools/levelgen/domino/sim.py + solver.py).
"use strict";
const DM = (() => {
  const DX = [1, 0, -1, 0], DY = [0, 1, 0, -1], ARROWS = ">v<^";
  function parseLevel(d) {
    const cells = [];
    for (const r of d.grid) for (let x = 0; x < d.w; x++) {
      const k = r[2 * x], a = r[2 * x + 1];
      cells.push([k, "tTS".includes(k) ? ARROWS.indexOf(a) : 0]);
    }
    const lv = { id: d.id || "", w: d.w, h: d.h, cells, attempts: d.attempts || 3, par: d.par || 0, tier: d.tier || "N",
      solution: (d.meta && d.meta.solution) || d.solution || [] };
    lv.turners = cells.map((c, i) => c[0] === "t" ? i : -1).filter(i => i >= 0);
    lv.bells = cells.map((c, i) => c[0] === "B" ? i : -1).filter(i => i >= 0);
    lv.start = cells.findIndex(c => c[0] === "S");
    return lv;
  }
  const less = (a, b) => a[0] !== b[0] ? a[0] < b[0] : a[1] !== b[1] ? a[1] < b[1] : a[2] < b[2];
  function runChain(lv, config) {
    const { w, h, cells } = lv, tdir = new Map(lv.turners.map((c, k) => [c, config[k]]));
    const fallen = new Set(), falls = [], rung = new Set(), heap = [[0, lv.start, cells[lv.start][1]]];
    while (heap.length) {
      let mi = 0;
      for (let i = 1; i < heap.length; i++) if (less(heap[i], heap[mi])) mi = i;
      const [t, c, d] = heap.splice(mi, 1)[0];
      if (fallen.has(c)) continue;
      fallen.add(c);
      const [k, a] = cells[c];
      if (k === "B") { rung.add(c); falls.push([t, c, []]); continue; }
      const outs = k === "d" ? [d] : k === "t" ? [tdir.get(c)] : (k === "T" || k === "S") ? [a] : [(d + 1) & 3, (d + 3) & 3];
      falls.push([t, c, outs]);
      const x = c % w, y = Math.floor(c / w);
      for (const o of outs) {
        const nx = x + DX[o], ny = y + DY[o];
        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
        const n = ny * w + nx;
        if ("dtTYBS".includes(cells[n][0]) && !fallen.has(n)) heap.push([t + 1, n, o]);
      }
    }
    return { falls, rung };
  }
  const wins = (lv, cfg) => { const { rung } = runChain(lv, cfg); return lv.bells.every(b => rung.has(b)); };
  class Game {
    constructor(lv) { this.level = lv; this.config = lv.turners.map(c => lv.cells[c][1]); this.attemptsLeft = lv.attempts; this.pushes = 0; this.taps = 0; this.status = "playing"; }
    rotate(cell) {
      if (this.status !== "playing") throw new Error("cannot rotate: " + this.status);
      const k = this.level.turners.indexOf(cell);
      if (k < 0) return false;
      this.config[k] = (this.config[k] + 1) & 3; this.taps++; return true;
    }
    push() {
      if (this.status !== "playing") throw new Error("cannot push: " + this.status);
      const r = runChain(this.level, this.config);
      this.pushes++; this.attemptsLeft--;
      if (this.level.bells.every(b => r.rung.has(b))) this.status = "won";
      else if (this.attemptsLeft <= 0) this.status = "lost";
      return r;
    }
    addAttempt() { if (this.status === "won") throw new Error("won"); this.attemptsLeft++; this.status = "playing"; }
  }
  const tapsBetween = (a, b) => a.reduce((s, x, i) => s + ((b[i] - x) & 3), 0);
  function solve(lv, start) {
    start = start || lv.turners.map(c => lv.cells[c][1]);
    const t = lv.turners.length, total = 4 ** t, win = [];
    for (let m = 0; m < total; m++) {
      const cfg = []; let v = m;
      for (let k = t - 1; k >= 0; k--) { cfg[k] = v & 3; v >>= 2; }
      if (wins(lv, cfg)) win.push(cfg);
    }
    if (!win.length) return { par: null, solutions: 0, total, best: null };
    let best = win[0];
    for (const c of win) { const a = tapsBetween(start, c), b = tapsBetween(start, best); if (a < b || (a === b && c.join() < best.join())) best = c; }
    return { par: tapsBetween(start, best), solutions: win.length, total, best };
  }
  return { DX, DY, parseLevel, runChain, wins, Game, solve, tapsBetween };
})();
if (typeof module !== "undefined") module.exports = DM;
