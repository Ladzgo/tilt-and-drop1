// Gear Works rules in JavaScript (mirror of tools/levelgen/gears/sim.py + solver.py). Hex grid, odd rows shifted right.
"use strict";
const GR = (() => {
  function neighbours(w, h, i) {
    const x = i % w, y = Math.floor(i / w);
    const c = y % 2 === 0
      ? [[x + 1, y], [x - 1, y], [x - 1, y - 1], [x, y - 1], [x - 1, y + 1], [x, y + 1]]
      : [[x + 1, y], [x - 1, y], [x, y - 1], [x + 1, y - 1], [x, y + 1], [x + 1, y + 1]];
    return c.filter(([a, b]) => a >= 0 && a < w && b >= 0 && b < h).map(([a, b]) => b * w + a);
  }
  function parseLevel(d) {
    const cells = d.grid.join("").split("");
    const lv = { id: d.id || "", w: d.w, h: d.h, cells, inventory: d.inventory, moves: d.moves || 0, par: d.par || 0, tier: d.tier || "N",
      solution: (d.meta && d.meta.solution) || d.solution || [] };
    lv.pegs = cells.map((c, i) => (c === "o" || c === "g") ? i : -1).filter(i => i >= 0);
    lv.pegIndex = new Map(lv.pegs.map((c, k) => [c, k]));
    lv.outputs = cells.map((c, i) => (c === "R" || c === "L") ? i : -1).filter(i => i >= 0);
    lv.start = lv.pegs.reduce((m, c, k) => cells[c] === "g" ? m | (1 << k) : m, 0);
    return lv;
  }
  const isGear = (lv, layout, i) => { const c = lv.cells[i]; return "GMRL".includes(c) || ((c === "o" || c === "g") && ((layout >> lv.pegIndex.get(i)) & 1) === 1); };
  function spins(lv, layout) {
    const { w, h } = lv, n = w * h, gear = Array.from({ length: n }, (_, i) => isGear(lv, layout, i));
    const spin = new Array(n).fill(0), jam = new Array(n).fill(false), seen = new Array(n).fill(false);
    for (let s = 0; s < n; s++) {
      if (!gear[s] || seen[s]) continue;
      const comp = [], stack = [s]; seen[s] = true;
      while (stack.length) { const i = stack.pop(); comp.push(i); for (const j of neighbours(w, h, i)) if (gear[j] && !seen[j]) { seen[j] = true; stack.push(j); } }
      const motors = comp.filter(i => lv.cells[i] === "M").sort((a, b) => a - b);
      if (!motors.length) continue;
      const val = new Map(), queue = [];
      for (const m of motors) { val.set(m, 1); queue.push(m); }
      let ok = true;
      for (let head = 0; head < queue.length && ok; head++) {
        const i = queue[head];
        for (const j of neighbours(w, h, i)) {
          if (!gear[j]) continue;
          if (val.has(j)) { if (val.get(j) === val.get(i)) { ok = false; break; } }
          else { val.set(j, -val.get(i)); queue.push(j); }
        }
      }
      for (const i of comp) { if (ok) spin[i] = val.get(i); else jam[i] = true; }
    }
    return { spin, jam };
  }
  const isWin = (lv, layout) => { const { spin } = spins(lv, layout); return lv.outputs.length > 0 && lv.outputs.every(o => spin[o] === (lv.cells[o] === "R" ? 1 : -1)); };
  const popcount = m => { let c = 0; while (m) { m &= m - 1; c++; } return c; };
  class Game {
    constructor(lv) { this.level = lv; this.layout = lv.start; this.movesLeft = lv.moves; this.movesUsed = 0; this.update(); }
    get inHand() { return this.level.inventory - popcount(this.layout); }
    update() { this.status = isWin(this.level, this.layout) ? "won" : this.movesLeft <= 0 ? "lost" : "playing"; }
    tap(cell) {
      if (this.status !== "playing") throw new Error("cannot tap: " + this.status);
      if (!this.level.pegIndex.has(cell)) return false;
      const k = this.level.pegIndex.get(cell);
      if (!((this.layout >> k) & 1) && this.inHand <= 0) return false;
      this.layout ^= 1 << k; this.movesLeft--; this.movesUsed++; this.update(); return true;
    }
    addMoves(n) { if (this.status === "won") throw new Error("won"); this.movesLeft += n; this.update(); }
    clone() { const g = Object.create(Game.prototype); Object.assign(g, this); return g; }
  }
  // enumerate layouts within inventory (same order as Python's combinations): par, solutions, best layout
  function solve(lv, start) {
    start = start === undefined ? lv.start : start;
    const m = lv.pegs.length, wins = [];
    const rec = (from, r, lay) => {
      if (r === 0) { if (isWin(lv, lay)) wins.push(lay); return; }
      for (let k = from; k < m; k++) rec(k + 1, r - 1, lay | (1 << k));
    };
    for (let r = 0; r <= lv.inventory; r++) rec(0, r, 0);
    if (!wins.length) return { par: null, solutions: 0, taps: [], layouts: [] };
    let best = wins[0];
    for (const l of wins) { const a = popcount(l ^ start), b = popcount(best ^ start); if (a < b || (a === b && l < best)) best = l; }
    const diff = best ^ start;
    return { par: popcount(diff), solutions: wins.length, taps: lv.pegs.filter((c, k) => (diff >> k) & 1), layouts: wins, best };
  }
  function tapOrder(lv, start, target) {
    const rem = lv.pegs.filter((c, k) => ((start >> k) & 1) && !((target >> k) & 1));
    const add = lv.pegs.filter((c, k) => !((start >> k) & 1) && ((target >> k) & 1));
    return rem.concat(add);
  }
  return { neighbours, parseLevel, spins, isWin, Game, solve, tapOrder };
})();
if (typeof module !== "undefined") module.exports = GR;
