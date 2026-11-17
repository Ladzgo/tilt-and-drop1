// Roll & Print UI (browser playtest). Uses shared helpers from the main script.
"use strict";
const RLEVELS = /*ROLL_LEVELS*/[];
const RS = { stars: store.get("rstars", {}), highest: store.get("rhighest", 0), seen: store.get("rseen", {}) };
const rsave = () => { store.set("rstars", RS.stars); store.set("rhighest", RS.highest); store.set("rseen", RS.seen); };
const PAINT = [null, "#e5484d", "#3e8ef7", "#f5c400", "#30a46c", "#8e4ec6", "#f76b15"];
const BLANKFACE = "#ece9f7";
const R_INTROS = {
  11: ["More colours", "The cube now carries several colours. Plan which face lands where."],
  41: ["Paint pools", "Rolling onto a pool paints the face underneath in that colour."],
  81: ["Corridors", "Walls make tight paths. Count the rolls before you go."],
  121: ["Ice", "On ice the cube keeps rolling until it reaches solid ground or a wall."],
  161: ["Review", "Everything together. Take your time."],
};
const rc = $("roll-canvas"), rx = rc.getContext("2d"), nc = $("roll-net"), nx = nc.getContext("2d");
let RL = { w: 0, h: 0, dpr: 1 };
let rlevel = null, rgame = null, rnum = 0, rhist = [], rended = false, rcont = 0, rdirs = [], rbusy = false, rqueue = null;
let view = null;           // displayed state {pos, faces, prints}
let tip = null;            // {from:[x,y], dir, theta, faces}
let splash = new Map(), rhintDir = -1, rhintUntil = 0;

function rlayout() {
  const r = rc.getBoundingClientRect(), dpr = Math.min(3, window.devicePixelRatio || 1);
  rc.width = Math.round(r.width * dpr); rc.height = Math.round(r.height * dpr); RL = { w: r.width, h: r.height, dpr };
  const n = nc.getBoundingClientRect(); nc.width = Math.round(n.width * dpr); nc.height = Math.round(n.height * dpr);
}
function rgeom() {
  const cs = Math.min(RL.w / (rlevel.w + .4), RL.h / (rlevel.h + .4));
  return { cs, ox: RL.w / 2 - rlevel.w * cs / 2, oy: RL.h / 2 - rlevel.h * cs / 2 };
}

function startRoll(n, replay = []) {
  active = "roll";
  rnum = n; rlevel = RO.parseLevel(RLEVELS[n - 1]); rgame = new RO.Game(rlevel);
  rhist = []; rended = false; rcont = 0; rdirs = []; rbusy = false; rqueue = null; tip = null; splash = new Map(); rhintDir = -1;
  ["hub", "map", "pmap", "papp", "rmap", "sapp", "smap", "gapp", "gmap"].forEach(id => { $(id).hidden = true; });
  $("rapp").hidden = false; closeOverlay(); rlayout();
  for (const d of replay) { rhist.push(rgame.clone()); if (rgame.roll(d)) rdirs.push(d); }
  view = { pos: rgame.pos.slice(), faces: rgame.faces.slice(), prints: new Map(rgame.prints) };
  $("r-title").textContent = `Level ${n}`;
  const tier = rlevel.tier, tt = tier === "SH" ? "Super hard" : tier === "H" ? "Hard" : "";
  $("r-sub").innerHTML = `Roll &amp; Print · world ${Math.ceil(n / 50)}` + (tt ? ` · <span class="tier-${tier}">${tt}</span>` : "");
  rupdate();
  rbanner(n === 1 ? "Swipe to roll the cube. The face underneath prints on the tile." :
    n === 2 ? "Watch the cube net: the bottom face is the one that prints." : "");
  if (S.showSol) rbanner(`Par ${rlevel.par} · solution ${rlevel.solution.replace(/E/g, "→").replace(/W/g, "←").replace(/N/g, "↑").replace(/S/g, "↓")}`);
  if (R_INTROS[n] && !RS.seen[n] && !replay.length) { RS.seen[n] = 1; rsave(); info(R_INTROS[n][0], R_INTROS[n][1], "Got it"); }
}
function rbanner(t) { $("r-banner").textContent = t; }
function rupdate() {
  $("r-moves-n").textContent = rgame.movesLeft;
  $("r-moves").classList.toggle("low", rgame.movesLeft <= 2);
  const done = rlevel.targets.filter(i => (rgame.prints.get(i) || 0) === rlevel.tiles[i][1]).length;
  $("r-done").textContent = `${done} / ${rlevel.targets.length} printed`;
}

function rinput(d) {
  if (!rgame || rended || !$("overlay").hidden || rgame.status !== "playing") return;
  audio();
  if (rbusy) { rqueue = d; return; }
  const before = rgame.clone(), trail = [];
  if (!rgame.roll(d, trail)) { tone(140, .08, "sine", .12); buzz(5); return; }
  rhist.push(before); rdirs.push(d);
  if (rhintDir === d) rhintDir = -1;
  rbusy = true;
  rupdate();
  // chain one tip per landing (ice keeps it rolling)
  let from = before.pos.slice(), faces = before.faces.slice(), t0 = 0;
  const dur = (S.speed === 3 ? .09 : S.speed === 2 ? .12 : .16);
  trail.forEach(([idx, after], j) => {
    const f0 = from.slice(), fc = faces.slice(), last = j === trail.length - 1;
    later(t0, () => { tip = { from: f0, dir: d, theta: 0, faces: fc }; });
    tween(dur * (j ? .8 : 1), p => { if (tip) tip.theta = p * Math.PI / 2; }, j ? ease.lin : ease.inQuad, t0, () => {
      tip = null;
      view.pos = [idx % rlevel.w, Math.floor(idx / rlevel.w)];
      view.faces = after.slice();
      land(idx, after, last);
    });
    t0 += dur * (j ? .8 : 1);
    from = [idx % rlevel.w, Math.floor(idx / rlevel.w)]; faces = after;
  });
  later(t0 + .02, () => {
    rbusy = false;
    if (rgame.status === "won") return rwin();
    if (rgame.status === "lost") return later(.3, rfail);
    if (rqueue !== null) { const q = rqueue; rqueue = null; rinput(q); }
  });
}
function land(idx, faces, last) {
  const [k, c] = rlevel.tiles[idx];
  tone(150, .08, "sine", .3, 0, -60); buzz(6);
  if (k === RO.POOL) { tone(520, .2, "sine", .1); splash.set(idx, { t: clock, color: PAINT[c] }); }
  if (k === RO.TARGET && faces[RO.B]) {
    const prev = view.prints.get(idx) || 0, col = faces[RO.B];
    view.prints.set(idx, col);
    splash.set(idx, { t: clock, color: PAINT[col] });
    const { cs, ox, oy } = rgeom(), x = ox + (idx % rlevel.w + .5) * cs, y = oy + (Math.floor(idx / rlevel.w) + .5) * cs;
    for (let j = 0; j < 12; j++) { const a = Math.random() * Math.PI * 2, v = cs * (1 + Math.random() * 2);
      particles.push({ x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v - cs * .8, t: 0, life: .4 + Math.random() * .3, size: cs * .07, color: PAINT[col], rot: 0, spin: 0 }); }
    noise(.12, .18, 900, 300);
    if (col === c) [523.25, 783.99].forEach((f, j) => tone(f * (1 + .06 * (idx % 4)), .4, "triangle", .14, j * .05));
    else if (prev === c) tone(220, .3, "sawtooth", .06);
  }
  if (k === RO.ERASER) noise(.15, .1, 2000, 4000);
}
function rstars() {
  if (rcont) return 1;
  const slack = Math.max(0, rlevel.moves - rlevel.par);
  return rgame.movesUsed <= rlevel.par ? 3 : rgame.movesUsed <= rlevel.par + Math.ceil(slack / 2) ? 2 : 1;
}
function rwin() {
  rended = true;
  const stars = rstars();
  RS.stars[rnum] = Math.max(RS.stars[rnum] || 0, stars); RS.highest = Math.max(RS.highest, rnum); rsave();
  sfx.win(); buzz([20, 40, 20]);
  later(.6, () => {
    const hasNext = rnum < RLEVELS.length;
    showCard(`<h2>Level complete!</h2><div class="stars" id="rw-stars">${starSvg(false)}${starSvg(false)}${starSvg(false)}</div>
      <p>${rgame.movesUsed} rolls · par ${rlevel.par}${rcont ? " · with extra moves" : ""}</p>
      ${hasNext ? `<button class="big go" id="rb-next">Next level</button>` : ""}<button class="big" id="rb-map">Levels</button>`);
    const svgs = document.querySelectorAll("#rw-stars svg");
    for (let i = 0; i < stars; i++) later(.2 + .22 * i, () => { svgs[i].outerHTML = starSvg(true); sfx.star(i); });
    if (hasNext) $("rb-next").onclick = () => startRoll(rnum + 1);
    $("rb-map").onclick = showRollMap;
  });
}
function rfail() {
  rended = true; sfx.fail(); buzz([40, 30, 40]);
  const done = rlevel.targets.filter(i => (rgame.prints.get(i) || 0) === rlevel.tiles[i][1]).length;
  showCard(`<h2>Out of moves</h2><p>${done} of ${rlevel.targets.length} tiles printed right. In the app, extra moves cost an ad or coins; here they are free.</p>
    ${rcont < 2 ? `<button class="big gold" id="rb-cont">+3 moves</button>` : ""}
    <button class="big blue" id="rb-retry">Retry</button><button class="big" id="rb-map">Levels</button>`);
  if (rcont < 2) $("rb-cont").onclick = () => { closeOverlay(); rgame.addMoves(3); rcont++; rended = false; rupdate(); };
  $("rb-retry").onclick = () => startRoll(rnum);
  $("rb-map").onclick = showRollMap;
}
function rundo() {
  if (rended || rbusy || !rhist.length) return;
  rgame = rhist.pop(); rdirs.pop();
  view = { pos: rgame.pos.slice(), faces: rgame.faces.slice(), prints: new Map(rgame.prints) };
  rupdate(); tone(900, .1, "sine", .08);
}
function rhint() {
  if (rended || rbusy) return;
  const s = RO.solve(rlevel, rgame.pos, rgame.faces, rgame.prints, Math.min(30, rgame.movesLeft + 4), 200000);
  if (!s || s.par === null) return rbanner("No solution from here. Try Undo or Restart.");
  rhintDir = s.path[0]; rhintUntil = clock + 2.5;
  const name = ["right", "down", "left", "up"][rhintDir];
  rbanner(`Roll ${name} · ${s.par} roll${s.par > 1 ? "s" : ""} to finish${s.par > rgame.movesLeft ? ` (you have ${rgame.movesLeft})` : ""}`);
  const btn = $(["r-right", "r-down", "r-left", "r-up"][rhintDir]);
  btn.animate && btn.animate([{ transform: "scale(1)" }, { transform: "scale(1.2)" }, { transform: "scale(1)" }, { transform: "scale(1.2)" }, { transform: "scale(1)" }], { duration: 900 });
}

// ---------------------------------------------------------------- drawing
function rrr(c, x, y, w, h, r) { c.beginPath(); c.roundRect ? c.roundRect(x, y, w, h, r) : c.rect(x, y, w, h); }
function shade(hex, k) {
  const v = parseInt(hex.slice(1), 16), f = ch => Math.round(Math.min(255, ((v >> ch) & 255) * k));
  return `rgb(${f(16)},${f(8)},${f(0)})`;
}
const faceCol = c => c ? PAINT[c] : BLANKFACE;
function drawTile(i, x, y, cs) {
  const [k, c] = rlevel.tiles[i];
  if (k === RO.WALL) { rx.fillStyle = "#3d3866"; rrr(rx, x + cs * .03, y + cs * .03, cs * .94, cs * .94, cs * .12); rx.fill();
    rx.fillStyle = "#4f4982"; rrr(rx, x + cs * .1, y + cs * .08, cs * .8, cs * .3, cs * .08); rx.fill(); return; }
  rx.fillStyle = k === RO.ICE ? "#2f4a6e" : "#25223f"; rrr(rx, x + cs * .04, y + cs * .04, cs * .92, cs * .92, cs * .14); rx.fill();
  if (k === RO.ICE) { rx.strokeStyle = "rgba(200,235,255,.35)"; rx.lineWidth = cs * .04; rx.beginPath(); rx.moveTo(x + cs * .25, y + cs * .7); rx.lineTo(x + cs * .5, y + cs * .3); rx.moveTo(x + cs * .5, y + cs * .75); rx.lineTo(x + cs * .72, y + cs * .4); rx.stroke(); }
  if (k === RO.ERASER) { rx.fillStyle = "#d7d2ea"; rrr(rx, x + cs * .3, y + cs * .35, cs * .4, cs * .3, cs * .06); rx.fill(); }
  if (k === RO.POOL) {
    const sp = splash.get(i), pt = sp ? Math.max(0, 1 - (clock - sp.t) / .5) : 0;
    rx.fillStyle = PAINT[c]; rx.globalAlpha = .85; rx.beginPath(); rx.ellipse(x + cs / 2, y + cs / 2, cs * (.34 + .06 * pt), cs * (.28 + .05 * pt), 0, 0, Math.PI * 2); rx.fill(); rx.globalAlpha = 1;
    rx.fillStyle = "rgba(255,255,255,.35)"; rx.beginPath(); rx.ellipse(x + cs * .4, y + cs * .42, cs * .1, cs * .05, -.4, 0, Math.PI * 2); rx.fill();
  }
  if (k === RO.TARGET) {
    const got = view.prints.get(i) || 0, ok = got === c, sp = splash.get(i), pt = sp ? Math.max(0, 1 - (clock - sp.t) / .45) : 0;
    if (got) { rx.fillStyle = PAINT[got]; rrr(rx, x + cs * (.1 - .05 * pt), y + cs * (.1 - .05 * pt), cs * (.8 + .1 * pt), cs * (.8 + .1 * pt), cs * .12); rx.fill();
      rx.fillStyle = "rgba(255,255,255,.18)"; rrr(rx, x + cs * .16, y + cs * .14, cs * .5, cs * .14, cs * .07); rx.fill(); }
    rx.strokeStyle = PAINT[c]; rx.lineWidth = cs * .07; rx.setLineDash(got ? [] : [cs * .12, cs * .08]);
    rrr(rx, x + cs * .12, y + cs * .12, cs * .76, cs * .76, cs * .12); rx.stroke(); rx.setLineDash([]);
    if (ok) { rx.strokeStyle = "rgba(255,255,255,.95)"; rx.lineWidth = cs * .07; rx.beginPath(); rx.moveTo(x + cs * .32, y + cs * .52); rx.lineTo(x + cs * .45, y + cs * .64); rx.lineTo(x + cs * .7, y + cs * .38); rx.stroke(); }
    else { rx.fillStyle = PAINT[c]; rx.beginPath(); rx.arc(x + cs / 2, y + cs / 2, cs * .09, 0, Math.PI * 2); rx.fill(); }
  }
}
function drawCube(cs, ox, oy) {
  const s = cs * .82;
  if (tip) {
    const { from, dir, theta, faces } = tip, ang = [0, Math.PI / 2, Math.PI, -Math.PI / 2][dir];
    const trail = [RO.W, RO.N, RO.E, RO.S][dir];
    const cx = ox + (from[0] + .5) * cs, cy = oy + (from[1] + .5) * cs;
    rx.save(); rx.translate(cx, cy); rx.rotate(ang);
    const P = s / 2 + (cs - s) / 2 * 0, c0 = Math.cos(theta), s0 = Math.sin(theta);
    const lift = Math.sin(theta * 2) * cs * .05;
    rx.fillStyle = "rgba(0,0,0,.3)"; rrr(rx, P - s * c0 + lift, -s / 2 + lift, s * (c0 + s0), s, s * .18); rx.fill();
    rx.fillStyle = shade(faceCol(faces[trail]), .7 + .3 * s0); rrr(rx, P - s * c0, -s / 2, Math.max(1, s * s0), s, s * .12); rx.fill();
    rx.fillStyle = shade(faceCol(faces[RO.T]), .7 + .3 * c0); rrr(rx, P - s * c0 + s * s0, -s / 2, Math.max(1, s * c0), s, s * .12); rx.fill();
    rx.restore();
    return;
  }
  const [px0, py0] = view.pos, x = ox + (px0 + .5) * cs, y = oy + (py0 + .5) * cs;
  rx.fillStyle = "rgba(0,0,0,.3)"; rrr(rx, x - s / 2 + cs * .04, y - s / 2 + cs * .06, s, s, s * .18); rx.fill();
  rx.fillStyle = shade(faceCol(view.faces[RO.S]), .62); rrr(rx, x - s / 2, y - s / 2 + s * .14, s, s, s * .18); rx.fill();
  rx.fillStyle = faceCol(view.faces[RO.T]); rrr(rx, x - s / 2, y - s / 2, s, s, s * .18); rx.fill();
  rx.fillStyle = "rgba(255,255,255,.22)"; rrr(rx, x - s * .38, y - s * .4, s * .76, s * .16, s * .08); rx.fill();
}
function drawNet() {
  const W = nc.width / RL.dpr, H = nc.height / RL.dpr, q = Math.min(W / 5.6, H / 3.2), f = view.faces;
  nx.setTransform(RL.dpr, 0, 0, RL.dpr, 0, 0); nx.clearRect(0, 0, W, H);
  const ox = W / 2 - q * 2.3, oy = H / 2 - q * 1.5;
  const cells = [[RO.N, 1, 0], [RO.W, 0, 1], [RO.T, 1, 1], [RO.E, 2, 1], [RO.S, 1, 2], [RO.B, 3.6, 1]];
  for (const [fi, cx, cy] of cells) {
    const x = ox + cx * q, y = oy + cy * q;
    nx.fillStyle = faceCol(f[fi]); rrr(nx, x + 2, y + 2, q - 4, q - 4, q * .18); nx.fill();
    if (fi === RO.B) { nx.strokeStyle = "#ffc53d"; nx.lineWidth = 3; rrr(nx, x, y, q, q, q * .2); nx.stroke(); }
  }
  nx.fillStyle = "#a7a2cc"; nx.font = `${Math.round(q * .3)}px Figtree, sans-serif`; nx.textAlign = "center";
  nx.fillText("top", ox + 1.5 * q, oy + 3.25 * q); nx.fillText("prints", ox + 4.1 * q, oy + 2.4 * q);
}
function rollDraw() {
  if (!rlevel || !view) return;
  if (rc.width === 0) rlayout();
  const { w: W, h: H, dpr } = RL, { cs, ox, oy } = rgeom();
  rx.setTransform(dpr, 0, 0, dpr, 0, 0); rx.clearRect(0, 0, W, H);
  rx.fillStyle = "#1a1830"; rrr(rx, ox - cs * .15, oy - cs * .15, rlevel.w * cs + cs * .3, rlevel.h * cs + cs * .3, cs * .25); rx.fill();
  for (let i = 0; i < rlevel.w * rlevel.h; i++) drawTile(i, ox + (i % rlevel.w) * cs, oy + Math.floor(i / rlevel.w) * cs, cs);
  drawCube(cs, ox, oy);
  if (rhintDir >= 0 && clock < rhintUntil && !tip) {
    const [x, y] = view.pos, cx = ox + (x + .5) * cs + RO.DX[rhintDir] * cs * .75, cy = oy + (y + .5) * cs + RO.DY[rhintDir] * cs * .75;
    rx.fillStyle = `rgba(255,197,61,${.5 + .4 * Math.sin(clock * 10)})`; rx.beginPath(); rx.arc(cx, cy, cs * .14, 0, Math.PI * 2); rx.fill();
  }
  for (const p of particles) { rx.globalAlpha = Math.max(0, 1 - p.t * p.t); rx.fillStyle = p.color; rx.beginPath(); rx.arc(p.x, p.y, p.size * (1 - p.t), 0, Math.PI * 2); rx.fill(); }
  rx.globalAlpha = 1;
  drawNet();
}

// ---------------------------------------------------------------- map
let rworld = 1;
function showRollMap() {
  active = "roll"; closeOverlay();
  $("hub").hidden = true; $("rapp").hidden = true; $("rmap").hidden = false;
  const total = Object.values(RS.stars).reduce((a, b) => a + b, 0);
  $("r-total").textContent = `${total} / ${RLEVELS.length * 3} stars`;
  const next = Math.min(RS.highest + 1, RLEVELS.length);
  $("r-play").textContent = `Play level ${next}`; $("r-play").onclick = () => startRoll(next);
  const tabs = $("r-tabs"); tabs.innerHTML = "";
  for (let w = 1; w <= Math.ceil(RLEVELS.length / 50); w++) {
    const b = document.createElement("button"); b.className = "tab"; b.setAttribute("role", "tab"); b.textContent = `World ${w}`;
    b.setAttribute("aria-selected", String(w === rworld)); b.onclick = () => { rworld = w; showRollMap(); }; tabs.appendChild(b);
  }
  const grid = $("r-grid"); grid.innerHTML = "";
  for (let n = (rworld - 1) * 50 + 1; n <= Math.min(rworld * 50, RLEVELS.length); n++) {
    const st = RS.stars[n] || 0, open = S.unlock || n <= RS.highest + 1, tier = RLEVELS[n - 1].tier;
    const b = document.createElement("button");
    b.className = "lvl roll" + (n === next && !st ? " next" : st ? " done" : "") + (open ? "" : " locked") + (tier === "H" ? " hard" : tier === "SH" ? " super" : "");
    b.innerHTML = open ? `${n}<span class="ls">${st ? "★".repeat(st) : ""}</span>` : `${n}`;
    b.disabled = !open; b.onclick = () => startRoll(n);
    b.setAttribute("aria-label", `Roll level ${n}${st ? `, ${st} stars` : ""}${open ? "" : ", locked"}`);
    grid.appendChild(b);
  }
}

// ---------------------------------------------------------------- input
let rdown = null;
rc.addEventListener("pointerdown", e => { rdown = { x: e.clientX, y: e.clientY }; rc.setPointerCapture(e.pointerId); });
rc.addEventListener("pointerup", e => {
  if (!rdown) return;
  const dx = e.clientX - rdown.x, dy = e.clientY - rdown.y, min = Math.max(24, RL.w * .05);
  rdown = null;
  if (Math.max(Math.abs(dx), Math.abs(dy)) < min) return;
  rinput(Math.abs(dx) > Math.abs(dy) ? (dx > 0 ? 0 : 2) : (dy > 0 ? 1 : 3));
});
window.addEventListener("keydown", e => {
  if (active !== "roll" || $("rapp").hidden) return;
  const m = { ArrowRight: 0, d: 0, ArrowDown: 1, s: 1, ArrowLeft: 2, a: 2, ArrowUp: 3, w: 3 };
  if (e.key in m) { e.preventDefault(); rinput(m[e.key]); } else if (e.key === "u") rundo();
});
window.addEventListener("resize", () => { if (!$("rapp").hidden) rlayout(); });
$("r-right").onclick = () => rinput(0); $("r-down").onclick = () => rinput(1);
$("r-left").onclick = () => rinput(2); $("r-up").onclick = () => rinput(3);
$("r-menu").onclick = () => { audio(); showRollMap(); };
$("r-undo").onclick = rundo; $("r-hint").onclick = rhint; $("r-restart").onclick = () => startRoll(rnum);
$("r-back").onclick = showHub;
$("card-roll").onclick = () => { audio(); if (RS.highest === 0) startRoll(1); else showRollMap(); };
