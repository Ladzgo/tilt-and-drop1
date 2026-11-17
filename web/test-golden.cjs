// node web/test-golden.cjs  -> replays /shared/golden through the JS port; exits 1 on any mismatch.
const fs = require("fs"), path = require("path");
const TD = require("./sim.js");
const dir = path.join(__dirname, "..", "shared", "golden");
let cases = 0, steps = 0, fail = 0;
for (const file of ["rules.json", "random.json"]) {
  for (const c of JSON.parse(fs.readFileSync(path.join(dir, file))).cases) {
    const lv = TD.parseLevel(c.level), g = new TD.Game(lv);
    const check = (cond, msg) => { if (!cond) { fail++; if (fail < 10) console.error(`${c.name}: ${msg}`); } };
    check(g.status === c.initial.status, "initial status");
    for (const s of c.steps) {
      let ev = [];
      if (s.input === "CW" || s.input === "CCW") ev = g.rotate(s.input);
      else if (s.input.startsWith("H:")) { const [x, y] = s.input.slice(2).split(",").map(Number); ev = g.hammer(x, y); }
      else g.addMoves(+s.input.slice(1));
      const got = ev.map(e => TD.goldenEvent(e, lv.w));
      check(JSON.stringify(got) === JSON.stringify(s.events), `events after ${s.input}: ${got} vs ${s.events}`);
      check(TD.DIRS[g.gravity] === s.gravity, "gravity");
      check(g.movesLeft === s.movesLeft, "movesLeft");
      check(g.status === s.status, "status");
      if (s.grid) check(JSON.stringify(TD.rows(g.cells, lv.w, lv.h)) === JSON.stringify(s.grid), "grid");
      steps++;
    }
    cases++;
  }
}
let solver = 0;
for (const c of JSON.parse(fs.readFileSync(path.join(dir, "solver.json"))).solver) {
  const lv = TD.parseLevel(c.level), h = TD.hint(lv, lv.cells, 0, 30, 400000);
  if (!h || h.moves !== c.minMoves) { fail++; console.error(`${c.name}: min moves ${h && h.moves} vs ${c.minMoves}`); }
  solver++;
}
console.log(`tilt & drop: ${cases} cases, ${steps} steps, ${solver} solver levels`);

// ---- Prism Paths
const PR = require("./prism.js");
const pg = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "shared", "prism", "golden.json")));
let pc = 0, ps = 0;
const r2 = v => Math.round(v * 100) / 100;
for (const c of pg.cases) {
  const lv = PR.parseLevel(c.level), g = new PR.Game(lv);
  if (g.status !== c.initialStatus) { fail++; console.error("prism initial status"); }
  for (const s of c.steps) {
    const spent = g.tap(s.tap);
    const segs = g.segments().map(x => [x[0], r2(x[1]), r2(x[2]), r2(x[3]), r2(x[4])]);
    const got = JSON.stringify([spent, g.movesLeft, g.status, lv.targets.map(i => g.received[i]), segs]);
    const exp = JSON.stringify([s.spent, s.movesLeft, s.status, s.received, s.segments]);
    if (got !== exp) { fail++; if (fail < 10) console.error("prism step mismatch", got, exp); }
    ps++;
  }
  pc++;
}
for (const c of pg.solver) {
  const r = PR.solve(PR.parseLevel(c.level));
  if (r.par !== c.par || r.solutions !== c.solutions) { fail++; console.error("prism solver", r.par, c.par, r.solutions, c.solutions); }
}
console.log(`prism: ${pc} cases, ${ps} steps, ${pg.solver.length} solver levels`);
// ---- Roll & Print
const RO = require("./roll.js");
const rg = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "shared", "roll", "golden.json")));
let rc = 0, rs = 0;
for (const c of rg.cases) {
  const lv = RO.parseLevel(c.level), g = new RO.Game(lv);
  if (g.status !== c.initialStatus) { fail++; console.error("roll initial status"); }
  for (const s of c.steps) {
    const trail = [], ok = g.roll(s.dir, trail);
    const got = JSON.stringify([ok, g.pos, g.faces.join(""), lv.targets.map(i => g.prints.get(i) || 0), trail.map(t => t[0]), g.movesLeft, g.status]);
    const exp = JSON.stringify([s.ok, s.pos, s.faces, s.prints, s.trail, s.movesLeft, s.status]);
    if (got !== exp) { fail++; if (fail < 10) console.error("roll step mismatch", got, exp); }
    rs++;
  }
  rc++;
}
for (const c of rg.solver) {
  const r = RO.solve(RO.parseLevel(c.level), null, null, null, 14);
  if (r.par !== c.par || r.solutions !== c.solutions) { fail++; console.error("roll solver", r.par, c.par, r.solutions, c.solutions); }
}
console.log(`roll: ${rc} cases, ${rs} steps, ${rg.solver.length} solver levels`);
// ---- Slide Out
const SL = require("./slide.js");
const sg = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "shared", "slide", "golden.json")));
let sc = 0, ss = 0;
for (const c of sg.cases) {
  const lv = SL.parseLevel(c.level), g = new SL.Game(lv);
  for (const s of c.steps) {
    const res = g.tapCell(s.cell);
    const got = JSON.stringify([res, g.removed >>> 0, g.tray, g.status]), exp = JSON.stringify([s.result, s.removed, s.tray, s.status]);
    if (got !== exp) { fail++; if (fail < 10) console.error("slide step mismatch", got, exp); }
    ss++;
  }
  sc++;
}
for (const c of sg.solver) {
  const r = SL.solve(SL.parseLevel(c.level));
  if (r.solvable !== c.solvable || r.states !== c.states || JSON.stringify(r.order) !== JSON.stringify(c.order)) { fail++; console.error("slide solver", r.solvable, c.solvable, r.states, c.states); }
}
console.log(`slide: ${sc} cases, ${ss} steps, ${sg.solver.length} solver levels`);
// ---- Gear Works
const GR = require("./gears.js");
const gg = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "shared", "gears", "golden.json")));
let gc = 0, gs = 0;
for (const c of gg.cases) {
  const lv = GR.parseLevel(c.level), g = new GR.Game(lv);
  if (g.status !== c.initialStatus) { fail++; console.error("gears initial status"); }
  for (const s of c.steps) {
    const ok = g.tap(s.cell), { spin, jam } = GR.spins(lv, g.layout);
    const got = JSON.stringify([ok, g.layout, spin, jam.map(Number), g.movesLeft, g.status]), exp = JSON.stringify([s.ok, s.layout, s.spin, s.jam, s.movesLeft, s.status]);
    if (got !== exp) { fail++; if (fail < 10) console.error("gears step mismatch", got, exp); }
    gs++;
  }
  gc++;
}
for (const c of gg.solver) {
  const r = GR.solve(GR.parseLevel(c.level));
  if (r.par !== c.par || r.solutions !== c.solutions) { fail++; console.error("gears solver", r.par, c.par, r.solutions, c.solutions); }
}
console.log(`gears: ${gc} cases, ${gs} steps, ${gg.solver.length} solver levels`);
// ---- Domino Run
const DM = require("./domino.js");
const dg = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "shared", "domino", "golden.json")));
let dc = 0, ds = 0;
for (const c of dg.cases) {
  const lv = DM.parseLevel(c.level), g = new DM.Game(lv);
  for (const s of c.steps) {
    let got, exp;
    if (s.act === "P") { const r = g.push(); got = JSON.stringify([r.falls, [...r.rung].sort((a, b) => a - b), g.attemptsLeft, g.status]); exp = JSON.stringify([s.falls, s.rung, s.attempts, s.status]); }
    else { const ok = g.rotate(s.act); got = JSON.stringify([ok, g.config, g.status]); exp = JSON.stringify([s.ok, s.config, s.status]); }
    if (got !== exp) { fail++; if (fail < 10) console.error("domino step mismatch", got, exp); }
    ds++;
  }
  dc++;
}
for (const c of dg.solver) {
  const r = DM.solve(DM.parseLevel(c.level));
  if (r.par !== c.par || r.solutions !== c.solutions) { fail++; console.error("domino solver", r.par, c.par, r.solutions, c.solutions); }
}
console.log(`domino: ${dc} cases, ${ds} steps, ${dg.solver.length} solver levels`);
console.log(`${fail} mismatches`);
process.exit(fail ? 1 : 0);
