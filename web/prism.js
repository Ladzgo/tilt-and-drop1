// Prism Paths rules in JavaScript (mirror of tools/levelgen/prism/sim.py + solver.py).
// Parity enforced by test-golden.cjs against /shared/prism/golden.json.
"use strict";
const PR = (() => {
  const EMPTY = 0, WALL = 1, SOURCE = 2, TARGET = 3, MIRROR = 4, FIXED = 5, FILTER = 6, SPLITTER = 7;
  const DX = [1, 0, -1, 0], DY = [0, 1, 0, -1], DIR_CH = ">v<^";
  const CH = { R: 1, G: 2, B: 4, Y: 3, M: 5, C: 6, W: 7 };
  const REFLECT = [[3, 2, 1, 0], [1, 0, 3, 2]];
  const rotatable = k => k === MIRROR || k === SPLITTER;

  function parseRow(row) {
    const out = [];
    for (let i = 0; i < row.length; i++) {
      const ch = row[i];
      if (ch === "[") { const j = row.indexOf("]", i); const t = row.slice(i + 1, j);
        if (t === "/" || t === "\\") out.push([SPLITTER, t === "/" ? 0 : 1]); else out.push([SOURCE, CH[t[0]] * 4 + DIR_CH.indexOf(t[1])]); i = j; }
      else if (ch === "(") { const j = row.indexOf(")", i); out.push([TARGET, CH[row.slice(i + 1, j)]]); i = j; }
      else if (ch === "{") { const j = row.indexOf("}", i); out.push([FIXED, row.slice(i + 1, j) === "/" ? 0 : 1]); i = j; }
      else if (ch === "<") { out.push([FILTER, CH[row[i + 1]]]); i += 2; }
      else out.push(ch === "." ? [EMPTY, 0] : ch === "#" ? [WALL, 0] : ch === "/" ? [MIRROR, 0] : [MIRROR, 1]);
    }
    return out;
  }
  function parseLevel(d) {
    const cells = [];
    for (const r of d.grid) cells.push(...parseRow(r));
    const lv = { id: d.id || "", w: d.w, h: d.h, cells, moves: d.moves || 0, par: d.par || 0, tier: d.tier || "N", meta: d.meta || {} };
    lv.rotatables = cells.map((c, i) => rotatable(c[0]) ? i : -1).filter(i => i >= 0);
    lv.targets = cells.map((c, i) => c[0] === TARGET ? i : -1).filter(i => i >= 0);
    lv.sources = cells.map((c, i) => c[0] === SOURCE ? i : -1).filter(i => i >= 0);
    return lv;
  }

  // states: Map index->state (missing = level state). onRot(i) -> true aborts (returns null).
  function trace(lv, states, segments, onRot) {
    const { w, h, cells } = lv, received = new Array(w * h).fill(0);
    for (const s of lv.sources) {
      const a = cells[s][1], color = a >> 2;
      const stack = [[s % w, Math.floor(s / w), a & 3]], seen = new Set();
      while (stack.length) {
        let [x, y, d] = stack.pop(), sx = x, sy = y;
        for (;;) {
          const nx = x + DX[d], ny = y + DY[d];
          if (nx < 0 || nx >= w || ny < 0 || ny >= h) { if (segments) segments.push([color, sx, sy, nx - DX[d] * .5, ny - DY[d] * .5]); break; }
          x = nx; y = ny;
          const i = y * w + x, key = i * 4 + d;
          if (seen.has(key)) { if (segments) segments.push([color, sx, sy, x, y]); break; }
          seen.add(key);
          const [k, v] = cells[i];
          if (k === EMPTY) continue;
          if (k === FILTER) { if (v === color) continue; if (segments) segments.push([color, sx, sy, x, y]); break; }
          if (k === WALL || k === SOURCE || k === TARGET) { if (k === TARGET) received[i] |= color; if (segments) segments.push([color, sx, sy, x, y]); break; }
          let st;
          if (k === MIRROR || k === SPLITTER) {
            if (states && states.has(i)) st = states.get(i);
            else if (onRot && onRot(i)) return null;
            else st = v;
          } else st = v;
          if (segments) segments.push([color, sx, sy, x, y]);
          if (k === SPLITTER) stack.push([x, y, d]);
          d = REFLECT[st][d]; sx = x; sy = y;
        }
      }
    }
    return received;
  }
  const isWin = (lv, rec) => lv.targets.length > 0 && lv.targets.every(i => rec[i] === lv.cells[i][1]);

  class Game {
    constructor(lv) {
      this.level = lv; this.states = new Map(lv.rotatables.map(i => [i, lv.cells[i][1]]));
      this.movesLeft = lv.moves; this.movesUsed = 0; this.received = trace(lv, this.states); this.update();
    }
    update() { this.status = isWin(this.level, this.received) ? "won" : this.movesLeft <= 0 ? "lost" : "playing"; }
    tap(i) {
      if (this.status !== "playing") throw new Error("cannot tap: " + this.status);
      if (!this.states.has(i)) return false;
      this.states.set(i, this.states.get(i) ^ 1); this.movesLeft--; this.movesUsed++;
      this.received = trace(this.level, this.states); this.update(); return true;
    }
    addMoves(n) { if (this.status === "won") throw new Error("won"); this.movesLeft += n; this.update(); }
    segments() { const s = []; trace(this.level, this.states, s); return s; }
    clone() { const g = Object.create(Game.prototype); Object.assign(g, this, { states: new Map(this.states), received: this.received.slice() }); return g; }
  }

  // exact solver (taps commute): min pieces to flip from `start`, number of winning configurations, taps
  function solve(lv, start, cap = 200000) {
    start = start || new Map(lv.rotatables.map(i => [i, lv.cells[i][1]]));
    let best = null, bestTaps = [], count = 0, nodes = 0;
    const assign = new Map();
    (function rec() {
      if (++nodes > cap) return;
      let pending = -1;
      const r = trace(lv, assign, null, i => { pending = i; return true; });
      if (r === null) {
        for (const st of [start.get(pending), start.get(pending) ^ 1]) { assign.set(pending, st); rec(); assign.delete(pending); }
        return;
      }
      if (isWin(lv, r)) {
        count++;
        const taps = [...assign].filter(([i, st]) => st !== start.get(i)).map(([i]) => i).sort((a, b) => a - b);
        if (best === null || taps.length < best) { best = taps.length; bestTaps = taps; }
      }
    })();
    return { par: best, solutions: count, taps: bestTaps };
  }

  return { EMPTY, WALL, SOURCE, TARGET, MIRROR, FIXED, FILTER, SPLITTER, DX, DY, parseLevel, trace, isWin, Game, solve };
})();
if (typeof module !== "undefined") module.exports = PR;
