// Roll & Print rules in JavaScript (mirror of tools/levelgen/roll/sim.py + solver.py).
"use strict";
const RO = (() => {
  const FLOOR = 0, WALL = 1, TARGET = 2, POOL = 3, ERASER = 4, ICE = 5;
  const T = 0, B = 1, N = 2, S = 3, E = 4, W = 5;
  const DX = [1, 0, -1, 0], DY = [0, 1, 0, -1];
  const PERM = [[W, E, N, S, T, B], [N, S, B, T, E, W], [E, W, N, S, B, T], [S, N, T, B, E, W]];
  const rollFaces = (f, d) => PERM[d].map(i => f[i]);
  function parseTile(ch) {
    if (ch === ".") return [FLOOR, 0]; if (ch === "#") return [WALL, 0]; if (ch === "*") return [ERASER, 0]; if (ch === "~") return [ICE, 0];
    if ("123456".includes(ch)) return [TARGET, +ch];
    return [POOL, "abcdef".indexOf(ch) + 1];
  }
  function parseLevel(d) {
    const tiles = [];
    for (const r of d.grid) for (const ch of r) tiles.push(parseTile(ch));
    const lv = { id: d.id || "", w: d.w, h: d.h, tiles, cube: [d.cube.x, d.cube.y], faces: [...d.cube.faces].map(Number),
      moves: d.moves || 0, par: d.par || 0, tier: d.tier || "N", solution: (d.meta && d.meta.solution) || d.solution || "" };
    lv.targets = tiles.map((t, i) => t[0] === TARGET ? i : -1).filter(i => i >= 0);
    return lv;
  }
  // returns {pos, faces, prints} or null if blocked; prints: Map target -> colour (copied)
  function step(lv, pos, faces, prints, d, trail) {
    const { w, h, tiles } = lv;
    let [x, y] = pos, nx = x + DX[d], ny = y + DY[d];
    if (nx < 0 || nx >= w || ny < 0 || ny >= h || tiles[ny * w + nx][0] === WALL) return null;
    prints = new Map(prints);
    for (;;) {
      x = nx; y = ny;
      faces = rollFaces(faces, d);
      const i = y * w + x, [k, c] = tiles[i];
      if (k === POOL) { faces = faces.slice(); faces[B] = c; }
      else if (k === TARGET && faces[B]) prints.set(i, faces[B]);
      else if (k === ERASER) { faces = faces.slice(); faces[B] = 0; }
      if (trail) trail.push([i, faces]);
      if (k !== ICE) break;
      nx = x + DX[d]; ny = y + DY[d];
      if (nx < 0 || nx >= w || ny < 0 || ny >= h || tiles[ny * w + nx][0] === WALL) break;
    }
    return { pos: [x, y], faces, prints };
  }
  const solved = (lv, prints) => lv.targets.length > 0 && lv.targets.every(i => (prints.get(i) || 0) === lv.tiles[i][1]);
  const key = (lv, pos, faces, prints) => pos.join(",") + "|" + faces.join("") + "|" + lv.targets.map(i => prints.get(i) || 0).join("");

  class Game {
    constructor(lv) { this.level = lv; this.pos = lv.cube.slice(); this.faces = lv.faces.slice(); this.prints = new Map(); this.movesLeft = lv.moves; this.movesUsed = 0; this.update(); }
    update() { this.status = solved(this.level, this.prints) ? "won" : this.movesLeft <= 0 ? "lost" : "playing"; }
    roll(d, trail) {
      if (this.status !== "playing") throw new Error("cannot roll: " + this.status);
      const r = step(this.level, this.pos, this.faces, this.prints, d, trail);
      if (!r) return false;
      this.pos = r.pos; this.faces = r.faces; this.prints = r.prints; this.movesLeft--; this.movesUsed++; this.update(); return true;
    }
    addMoves(n) { if (this.status === "won") throw new Error("won"); this.movesLeft += n; this.update(); }
    clone() { const g = Object.create(Game.prototype); Object.assign(g, this, { pos: this.pos.slice(), faces: this.faces.slice(), prints: new Map(this.prints) }); return g; }
  }

  // BFS: shortest solution from a state (par, number of shortest solutions, first move)
  function solve(lv, pos, faces, prints, maxDepth = 30, cap = 300000) {
    pos = pos || lv.cube; faces = faces || lv.faces; prints = prints || new Map();
    if (solved(lv, prints)) return { par: 0, solutions: 1, path: [] };
    const k0 = key(lv, pos, faces, prints), depth = new Map([[k0, 0]]), paths = new Map([[k0, 1]]), parent = new Map([[k0, null]]);
    let frontier = [[k0, { pos, faces, prints }]], winDepth = null, wins = [];
    for (let dpt = 0; dpt < maxDepth && frontier.length && winDepth === null; dpt++) {
      const next = [];
      for (const [k, s] of frontier) for (let d = 0; d < 4; d++) {
        const r = step(lv, s.pos, s.faces, s.prints, d);
        if (!r) continue;
        const nk = key(lv, r.pos, r.faces, r.prints);
        if (!depth.has(nk)) {
          depth.set(nk, dpt + 1); paths.set(nk, paths.get(k)); parent.set(nk, [k, d]); next.push([nk, r]);
          if (solved(lv, r.prints)) { winDepth = dpt + 1; wins.push(nk); }
        } else if (depth.get(nk) === dpt + 1) paths.set(nk, paths.get(nk) + paths.get(k));
        if (depth.size > cap) return null;
      }
      frontier = next;
    }
    if (winDepth === null) return { par: null, solutions: 0, path: [] };
    const path = []; let k = wins[0];
    while (parent.get(k)) { const [pk, d] = parent.get(k); path.push(d); k = pk; }
    return { par: winDepth, solutions: wins.reduce((a, w) => a + paths.get(w), 0), path: path.reverse() };
  }
  return { FLOOR, WALL, TARGET, POOL, ERASER, ICE, T, B, N, S, E, W, DX, DY, parseLevel, step, solved, Game, solve, rollFaces };
})();
if (typeof module !== "undefined") module.exports = RO;
