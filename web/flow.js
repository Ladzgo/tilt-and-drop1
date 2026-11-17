// Flow Fill rules in JavaScript (mirror of tools/levelgen/flow/sim.py + solver.py).
"use strict";
const FW = (() => {
  const DX = [1, 0, -1, 0], DY = [0, 1, 0, -1];
  const BASE = { i: 0b0101, l: 0b1001, t: 0b1101, x: 0b1111 };
  const rotMask = (m, r) => { r &= 3; return ((m << r) | (m >> (4 - r))) & 15; };
  function parseLevel(d) {
    const pipes = [];
    for (const row of d.grid) for (let x = 0; x < d.w; x++) {
      const t = row[2 * x], k = row[2 * x + 1];
      pipes.push(t === "." ? null : [t.toLowerCase(), +k, t === t.toLowerCase() && t !== "x"]);
    }
    return { id: d.id || "", w: d.w, h: d.h, pipes, sources: d.sources.map(s => s.slice()), cups: d.cups.map(c => c.slice()),
      moves: d.moves || 0, par: d.par || 0, tier: d.tier || "N", solution: (d.meta && d.meta.solution) || [] };
  }
  const rotatables = lv => lv.pipes.map((p, i) => (p && p[2] ? i : -1)).filter(i => i >= 0);
  function mask(lv, i, rot) {
    const p = lv.pipes[i];
    if (p) return rotMask(BASE[p[0]], rot && rot.has(i) ? rot.get(i) : p[1]);
    for (const [x, y, , d] of lv.sources.concat(lv.cups)) if (y * lv.w + x === i) return 1 << d;
    return 0;
  }
  const ABORT = {};
  // returns {colour, leaks: [[cell, dir]] sorted, dist} or null if onRotatable(i) aborts
  function flood(lv, rot, onRotatable) {
    const w = lv.w, h = lv.h, n = w * h, colour = new Array(n).fill(0), dist = new Array(n).fill(-1), leaks = new Set(), queue = [];
    for (const [x, y, c] of lv.sources) { const i = y * w + x; colour[i] |= 1 << c; dist[i] = 0; queue.push([i, c]); }
    const cache = new Map();
    const m = i => {
      if (!cache.has(i)) {
        const p = lv.pipes[i];
        if (p && p[2] && !(rot && rot.has(i)) && onRotatable && onRotatable(i)) throw ABORT;
        cache.set(i, mask(lv, i, rot));
      }
      return cache.get(i);
    };
    try {
      for (let head = 0; head < queue.length; head++) {
        const [i, c] = queue[head], mi = m(i), x = i % w, y = (i / w) | 0;
        for (let d = 0; d < 4; d++) {
          if (!(mi >> d & 1)) continue;
          const nx = x + DX[d], ny = y + DY[d];
          if (nx < 0 || nx >= w || ny < 0 || ny >= h) { leaks.add(i * 4 + d); continue; }
          const j = ny * w + nx;
          if (!(m(j) >> ((d + 2) & 3) & 1)) { leaks.add(i * 4 + d); continue; }
          if (colour[j] >> c & 1) continue;
          colour[j] |= 1 << c;
          if (dist[j] < 0) dist[j] = dist[i] + 1;
          queue.push([j, c]);
        }
      }
    } catch (e) { if (e === ABORT) return null; throw e; }
    return { colour, leaks: [...leaks].sort((a, b) => a - b).map(k => [k >> 2, k & 3]), dist };
  }
  function evaluate(lv, rot) {
    const { colour, leaks, dist } = flood(lv, rot);
    const contaminated = colour.map((cm, i) => (cm & (cm - 1) ? i : -1)).filter(i => i >= 0);
    const cups = lv.cups.map(([x, y, c]) => colour[y * lv.w + x] === 1 << c);
    return { colour, leaks, contaminated, cups, win: cups.every(Boolean) && !leaks.length && !contaminated.length, dist };
  }
  class Game {
    constructor(lv) {
      this.level = lv; this.rot = new Map(rotatables(lv).map(i => [i, lv.pipes[i][1]]));
      this.movesLeft = lv.moves; this.movesUsed = 0; this.update();
    }
    update() { this.state = evaluate(this.level, this.rot); this.status = this.state.win ? "won" : this.movesLeft <= 0 ? "lost" : "playing"; }
    tap(cell) {
      if (this.status !== "playing") throw new Error("cannot tap: " + this.status);
      if (!this.rot.has(cell)) return false;
      this.rot.set(cell, (this.rot.get(cell) + 1) & 3); this.movesLeft--; this.movesUsed++; this.update(); return true;
    }
    addMoves(n) { if (this.status === "won") throw new Error("won"); this.movesLeft += n; this.update(); }
  }
  function options(lv, i, startR) {
    const seen = new Map();
    for (let k = 0; k < 4; k++) { const r = (startR + k) & 3, mk = rotMask(BASE[lv.pipes[i][0]], r); if (!seen.has(mk)) seen.set(mk, [r, k]); }
    return [...seen.values()].sort((a, b) => a[1] - b[1] || a[0] - b[0]);
  }
  function solve(lv, start, nodeCap = 300000) {
    start = start || new Map(rotatables(lv).map(i => [i, lv.pipes[i][1]]));
    let best = null, bestRot = null, count = 0, nodes = 0;
    const assign = new Map();
    const rec = cost => {
      if (++nodes > nodeCap) return;
      const pending = [];
      const r = flood(lv, assign, i => { pending.push(i); return true; });
      if (r === null) {
        const i = pending[0];
        for (const [rr, c] of options(lv, i, start.get(i))) { assign.set(i, rr); rec(cost + c); assign.delete(i); }
        return;
      }
      if (r.leaks.length || r.colour.some(cm => cm & (cm - 1))) return;
      if (lv.cups.every(([x, y, c]) => r.colour[y * lv.w + x] === 1 << c)) {
        count++;
        if (best === null || cost < best) { best = cost; bestRot = new Map(assign); }
      }
    };
    rec(0);
    return { par: best, solutions: count, rot: bestRot, nodes };
  }
  return { DX, DY, BASE, rotMask, parseLevel, rotatables, mask, flood, evaluate, Game, solve };
})();
if (typeof module !== "undefined") module.exports = FW;
