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
console.log(`${cases} cases, ${steps} steps, ${solver} solver levels, ${fail} mismatches`);
process.exit(fail ? 1 : 0);
