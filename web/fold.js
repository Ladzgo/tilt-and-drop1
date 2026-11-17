// Fold rules in JavaScript (mirror of tools/levelgen/fold/sim.py + solver.py).
"use strict";
const FD = (() => {
  function parseLevel(d) {
    return { id: d.id || "", w: d.w, h: d.h, front: d.front.join("").split("").map(Number), back: d.back.join("").split("").map(Number),
      target: d.target, moves: d.moves || 0, par: d.par || 0, tier: d.tier || "N", solution: (d.meta && d.meta.solution) || d.solution || [] };
  }
  // sheet: array of rows; row: array of stacks; stack: array of [square, faceUp] top first
  const initial = lv => Array.from({ length: lv.h }, (_, y) => Array.from({ length: lv.w }, (_, x) => [[y * lv.w + x, 1]]));
  const flip = st => st.slice().reverse().map(([s, f]) => [s, 1 - f]);
  const transpose = sh => sh[0].map((_, x) => sh.map(row => row[x]));
  function legalFolds(sh) {
    const h = sh.length, w = sh[0].length, out = [];
    for (let k = 1; k < w; k++) { if (k <= w - k) out.push(["V", k, 0]); if (w - k <= k) out.push(["V", k, 1]); }
    for (let k = 1; k < h; k++) { if (k <= h - k) out.push(["H", k, 0]); if (h - k <= k) out.push(["H", k, 1]); }
    return out;
  }
  function fold(sh, axis, k, side) {
    const h = sh.length, w = sh[0].length;
    if (axis === "V") {
      if (k < 1 || k >= w) return null;
      if (side === 0) {
        if (k > w - k) return null;
        return sh.map(row => Array.from({ length: w - k }, (_, xn) => { const m = k - 1 - xn; return m >= 0 ? flip(row[m]).concat(row[k + xn]) : row[k + xn]; }));
      }
      if (w - k > k) return null;
      return sh.map(row => Array.from({ length: k }, (_, xn) => { const m = 2 * k - 1 - xn; return m < w ? flip(row[m]).concat(row[xn]) : row[xn]; }));
    }
    if (axis === "H") { const r = fold(transpose(sh), "V", k, side); return r === null ? null : transpose(r); }
    return null;
  }
  const visible = (lv, sh) => sh.map(row => row.map(st => String(st[0][1] ? lv.front[st[0][0]] : lv.back[st[0][0]])).join(""));
  const solved = (lv, sh) => JSON.stringify(visible(lv, sh)) === JSON.stringify(lv.target);
  const key = sh => JSON.stringify(sh);
  class Game {
    constructor(lv) { this.level = lv; this.sheet = initial(lv); this.history = []; this.movesLeft = lv.moves; this.movesUsed = 0; this.update(); }
    update() { this.status = solved(this.level, this.sheet) ? "won" : this.movesLeft <= 0 ? "lost" : "playing"; }
    fold(axis, k, side) {
      if (this.status !== "playing") throw new Error("cannot fold: " + this.status);
      const r = fold(this.sheet, axis, k, side);
      if (!r) return false;
      this.history.push(this.sheet); this.sheet = r; this.movesLeft--; this.movesUsed++; this.update(); return true;
    }
    addMoves(n) { if (this.status === "won") throw new Error("won"); this.movesLeft += n; this.update(); }
  }
  function solve(lv, start, maxDepth = 12) {
    start = start || initial(lv);
    if (solved(lv, start)) return { par: 0, solutions: 1, path: [] };
    const depth = new Map([[key(start), 0]]), paths = new Map([[key(start), 1]]), parent = new Map([[key(start), null]]);
    let frontier = [start], winDepth = null; const wins = [];
    for (let d = 0; d < maxDepth && frontier.length && winDepth === null; d++) {
      const next = [];
      for (const s of frontier) {
        const ks = key(s);
        for (const mv of legalFolds(s)) {
          const ns = fold(s, ...mv), kn = key(ns);
          if (!depth.has(kn)) {
            depth.set(kn, d + 1); paths.set(kn, paths.get(ks)); parent.set(kn, [ks, mv]); next.push(ns);
            if (solved(lv, ns)) { winDepth = d + 1; wins.push(kn); }
          } else if (depth.get(kn) === d + 1) paths.set(kn, paths.get(kn) + paths.get(ks));
        }
      }
      frontier = next;
    }
    if (winDepth === null) return { par: null, solutions: 0, path: [] };
    const path = []; let k = wins[0];
    while (parent.get(k)) { const [pk, mv] = parent.get(k); path.push(mv); k = pk; }
    return { par: winDepth, solutions: wins.reduce((a, w) => a + paths.get(w), 0), path: path.reverse() };
  }
  return { parseLevel, initial, legalFolds, fold, visible, solved, Game, solve };
})();
if (typeof module !== "undefined") module.exports = FD;
