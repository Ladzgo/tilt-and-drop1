// Gear Works UI (browser playtest). Uses shared helpers from the main script.
"use strict";
const GLEVELS = /*GEAR_LEVELS*/[];
const GS = { stars: store.get("gstars", {}), highest: store.get("ghighest", 0), seen: store.get("gseen", {}) };
const gsave = () => { store.set("gstars", GS.stars); store.set("ghighest", GS.highest); store.set("gseen", GS.seen); };
const G_INTROS = {
  11: ["Directions", "Touching gears turn opposite ways. Each dial shows the way it must turn."],
  41: ["Jams", "Three gears that all touch each other lock up. The whole train stops."],
  81: ["Fixed gears", "Bolted gears can't be moved. Your gear supply is tight."],
  121: ["Two motors", "Both motors turn clockwise. If they meet the wrong way round, everything jams."],
  161: ["Review", "Everything together."],
};
const gc = $("gear-canvas"), gx = gc.getContext("2d");
let GL = { w: 0, h: 0, dpr: 1 };
let glevel = null, ggame = null, gnum = 0, ghist = [], gended = false, gcont = 0, gtaps = [];
let gspin = null, gjam = null, gangle = [], gspeed = [], gdrop = new Map(), ghintCell = -1, ghintUntil = 0, glit = new Set(), gboost = 0, glast = 0;

function glayout() { const r = gc.getBoundingClientRect(), dpr = Math.min(3, window.devicePixelRatio || 1); gc.width = Math.round(r.width * dpr); gc.height = Math.round(r.height * dpr); GL = { w: r.width, h: r.height, dpr }; }
function ggeom() {
  const dx = Math.min(GL.w / (glevel.w + .7), GL.h / ((glevel.h - 1) * .866 + 1.2)), dy = dx * .866;
  const ox = GL.w / 2 - (glevel.w - .5) * dx / 2, oy = GL.h / 2 - (glevel.h - 1) * dy / 2;
  return { dx, dy, ox, oy, r: dx * .5 };
}
const gcenter = (i, g) => ({ x: g.ox + (i % glevel.w + (Math.floor(i / glevel.w) % 2) * .5) * g.dx, y: g.oy + Math.floor(i / glevel.w) * g.dy });

function grefresh(animated) {
  const s = GR.spins(glevel, ggame.layout);
  gspin = s.spin; gjam = s.jam;
  const nowLit = new Set(glevel.outputs.filter(o => gspin[o] === (glevel.cells[o] === "R" ? 1 : -1)));
  if (animated) {
    let k = 0;
    for (const o of nowLit) if (!glit.has(o)) later(.12 + .06 * k++, () => { tone(880 * (1 + .12 * (o % 5)), .5, "triangle", .14); tone(1760 * (1 + .12 * (o % 5)), .25, "sine", .04); buzz(12); });
    if (gjam.some(Boolean)) { tone(70, .35, "sawtooth", .08); buzz([20, 20, 20]); }
  }
  glit = nowLit;
  $("g-hand").textContent = `${ggame.inHand} gear${ggame.inHand === 1 ? "" : "s"} in hand`;
  $("g-moves-n").textContent = ggame.movesLeft;
  $("g-moves").classList.toggle("low", ggame.movesLeft <= 1);
}

function startGears(n, replay = []) {
  active = "gears";
  gnum = n; glevel = GR.parseLevel(GLEVELS[n - 1]); ggame = new GR.Game(glevel);
  ghist = []; gended = false; gcont = 0; gtaps = []; gdrop = new Map(); ghintCell = -1; gboost = 0;
  ["hub", "map", "pmap", "papp", "rmap", "rapp", "smap", "sapp", "gmap", "dapp", "dmap", "fapp", "fmap", "wapp", "wmap"].forEach(id => { $(id).hidden = true; });
  $("gapp").hidden = false; closeOverlay(); glayout();
  for (const c of replay) { ghist.push(ggame.clone()); if (ggame.tap(c)) gtaps.push(c); }
  const N = glevel.w * glevel.h;
  gangle = Array.from({ length: N }, (_, i) => ((i % glevel.w + Math.floor(i / glevel.w)) % 2) * Math.PI / 10);
  gspeed = new Array(N).fill(0);
  glit = new Set();
  grefresh(false);
  $("g-title").textContent = `Level ${n}`;
  const tier = glevel.tier, tt = tier === "SH" ? "Super hard" : tier === "H" ? "Hard" : "";
  $("g-sub").innerHTML = `Gear Works · world ${Math.ceil(n / 50)}` + (tt ? ` · <span class="tier-${tier}">${tt}</span>` : "");
  gbanner(n === 1 ? "Tap the empty pegs to drop gears. Carry the motor's spin to the dial." :
    n === 2 ? "Tap a gear again to take it back. Every tap costs a move." : "");
  if (S.showSol) gbanner(`Par ${glevel.par} · tap the pulsing pegs`), gshowSol();
  if (G_INTROS[n] && !GS.seen[n] && !replay.length) { GS.seen[n] = 1; gsave(); info(G_INTROS[n][0], G_INTROS[n][1], "Got it"); }
}
function gbanner(t) { $("g-banner").textContent = t; }
function gshowSol() { const s = GR.solve(glevel, ggame.layout); if (s.par) { ghintCell = GR.tapOrder(glevel, ggame.layout, s.best)[0]; ghintUntil = clock + 1e9; } }

function gtap(cell) {
  if (!ggame || gended || !$("overlay").hidden || ggame.status !== "playing") return;
  audio();
  if (!glevel.pegIndex.has(cell)) return;
  const before = ggame.clone();
  if (!ggame.tap(cell)) { tone(160, .07, "square", .05); gbanner("No gears in hand: take one back first."); buzz(6); return; }
  ghist.push(before); gtaps.push(cell);
  const placed = (ggame.layout >> glevel.pegIndex.get(cell)) & 1;
  gdrop.set(cell, { t: clock, placed });
  tone(placed ? 420 : 300, .09, "square", .06); noise(.06, .12, 2500, 1200); buzz(8);
  if (ghintCell === cell) ghintCell = -1;
  grefresh(true);
  if (S.showSol && ggame.status === "playing") gshowSol();
  if (ggame.status === "won") gwin(); else if (ggame.status === "lost") later(.5, gfail);
}
function gstars() {
  if (gcont) return 1;
  const slack = Math.max(0, glevel.moves - glevel.par);
  return ggame.movesUsed <= glevel.par ? 3 : ggame.movesUsed <= glevel.par + Math.ceil(slack / 2) ? 2 : 1;
}
function gwin() {
  gended = true;
  const stars = gstars();
  GS.stars[gnum] = Math.max(GS.stars[gnum] || 0, stars); GS.highest = Math.max(GS.highest, gnum); gsave();
  later(.3, () => { sfx.win(); buzz([20, 40, 20]); gboost = clock; });
  later(1.1, () => {
    const hasNext = gnum < GLEVELS.length;
    showCard(`<h2>Machine running!</h2><div class="stars" id="gw-stars">${starSvg(false)}${starSvg(false)}${starSvg(false)}</div>
      <p>${ggame.movesUsed} taps · par ${glevel.par}${gcont ? " · with extra moves" : ""}</p>
      ${hasNext ? `<button class="big go" id="gb-next">Next level</button>` : ""}<button class="big" id="gb-map">Levels</button>`);
    const svgs = document.querySelectorAll("#gw-stars svg");
    for (let i = 0; i < stars; i++) later(.2 + .22 * i, () => { svgs[i].outerHTML = starSvg(true); sfx.star(i); });
    if (hasNext) $("gb-next").onclick = () => startGears(gnum + 1);
    $("gb-map").onclick = showGearMap;
  });
}
function gfail() {
  gended = true; sfx.fail(); buzz([40, 30, 40]);
  showCard(`<h2>Out of moves</h2><p>${glit.size} of ${glevel.outputs.length} dials turning the right way. In the app, extra moves cost an ad or coins; here they are free.</p>
    ${gcont < 2 ? `<button class="big gold" id="gb-cont">+3 moves</button>` : ""}
    <button class="big blue" id="gb-retry">Retry</button><button class="big" id="gb-map">Levels</button>`);
  if (gcont < 2) $("gb-cont").onclick = () => { closeOverlay(); ggame.addMoves(3); gcont++; gended = false; grefresh(false); };
  $("gb-retry").onclick = () => startGears(gnum);
  $("gb-map").onclick = showGearMap;
}
function gundo() { if (gended || !ghist.length) return; ggame = ghist.pop(); gtaps.pop(); grefresh(false); tone(900, .1, "sine", .08); }
function ghint() {
  if (gended) return;
  const s = GR.solve(glevel, ggame.layout);
  if (s.par === null) return gbanner("No solution with these gears.");
  ghintCell = GR.tapOrder(glevel, ggame.layout, s.best)[0]; ghintUntil = clock + 2.5;
  gbanner(`Tap the glowing peg · ${s.par} tap${s.par > 1 ? "s" : ""} to finish${s.par > ggame.movesLeft ? ` (you have ${ggame.movesLeft})` : ""}`);
}

// ---------------------------------------------------------------- drawing
function gearPath(cx, cy, r, teeth, ang) {
  gx.beginPath();
  for (let k = 0; k < teeth * 4; k++) {
    const a = ang + k * Math.PI * 2 / (teeth * 4), rr = (k % 4 === 1 || k % 4 === 2) ? r : r * .82;
    gx[k ? "lineTo" : "moveTo"](cx + Math.cos(a) * rr, cy + Math.sin(a) * rr);
  }
  gx.closePath();
}
function gearsDraw() {
  if (!glevel || !gspin) return;
  if (gc.width === 0) glayout();
  const dt = Math.min(.05, clock - glast); glast = clock;
  const { w: W, h: H, dpr } = GL, g = ggeom(), N = glevel.w * glevel.h;
  gx.setTransform(dpr, 0, 0, dpr, 0, 0); gx.clearRect(0, 0, W, H);
  const boost = clock - gboost < 1.2 ? 3 : 1;
  for (let i = 0; i < N; i++) {
    const target = (gjam[i] ? 0 : gspin[i]) * 1.6 * boost;
    gspeed[i] += (target - gspeed[i]) * Math.min(1, dt * 8);
    gangle[i] += gspeed[i] * dt;
  }
  for (let i = 0; i < N; i++) {          // faint hex backdrop gives the pegboard its shape
    const { x, y } = gcenter(i, g);
    gx.fillStyle = "#1c1a33"; gx.beginPath();
    for (let k = 0; k < 6; k++) { const a = Math.PI / 6 + k * Math.PI / 3, rr = g.dx * .56; gx[k ? "lineTo" : "moveTo"](x + Math.cos(a) * rr, y + Math.sin(a) * rr); }
    gx.closePath(); gx.fill();
  }
  for (let i = 0; i < N; i++) {
    const c = glevel.cells[i];
    if (c === ".") continue;
    const { x, y } = gcenter(i, g);
    gx.fillStyle = "#211f3a"; gx.beginPath(); gx.arc(x, y, g.r * .3, 0, Math.PI * 2); gx.fill();
    if (i === ghintCell && clock < ghintUntil) { gx.strokeStyle = `rgba(255,197,61,${.5 + .4 * Math.sin(clock * 10)})`; gx.lineWidth = 4; gx.beginPath(); gx.arc(x, y, g.r * .9, 0, Math.PI * 2); gx.stroke(); }
  }
  for (let i = 0; i < N; i++) {
    const c = glevel.cells[i];
    const present = "GMRL".includes(c) || ((c === "o" || c === "g") && ((ggame.layout >> glevel.pegIndex.get(i)) & 1));
    const dr = gdrop.get(i), dp = dr ? Math.min(1, (clock - dr.t) / .25) : 1;
    if (!present && !(dr && !dr.placed && dp < 1)) {
      if (c === "o" || c === "g") { gx.fillStyle = "#4a4580"; const { x, y } = gcenter(i, g); gx.beginPath(); gx.arc(x, y, g.r * .14, 0, Math.PI * 2); gx.fill(); }
      continue;
    }
    let { x, y } = gcenter(i, g);
    if (gjam[i]) { x += Math.sin(clock * 60 + i) * 1.5; y += Math.cos(clock * 53 + i) * 1.5; }
    const scale = dr ? (dr.placed ? (dp < 1 ? .6 + .4 * ease.outBack(dp) : 1) : 1 - dp) : 1;
    const r = g.r * .96 * scale;
    const base = c === "M" ? "#f5c400" : c === "R" || c === "L" ? "#3e8ef7" : c === "G" ? "#8c8379" : "#c9c4e6";
    gx.fillStyle = gjam[i] ? "#b8434a" : base;
    gearPath(x, y, r, c === "M" ? 10 : 9, gangle[i]); gx.fill();
    gx.fillStyle = "rgba(0,0,0,.25)"; gx.beginPath(); gx.arc(x, y, r * .55, 0, Math.PI * 2); gx.fill();
    gx.fillStyle = gjam[i] ? "#7a2a30" : shade(base, .75); gx.beginPath(); gx.arc(x, y, r * .45, 0, Math.PI * 2); gx.fill();
    if (c === "G") { gx.fillStyle = "#3a3560"; for (let k = 0; k < 3; k++) { const a = gangle[i] + k * 2.094; gx.beginPath(); gx.arc(x + Math.cos(a) * r * .28, y + Math.sin(a) * r * .28, r * .07, 0, Math.PI * 2); gx.fill(); } }
    if (c === "M") { gx.fillStyle = "#2b2100"; gx.beginPath(); gx.moveTo(x - r * .08, y - r * .3); gx.lineTo(x + r * .14, y - r * .02); gx.lineTo(x, y); gx.lineTo(x + r * .08, y + r * .3); gx.lineTo(x - r * .14, y + r * .02); gx.lineTo(x, y); gx.closePath(); gx.fill(); }
    if (c === "R" || c === "L") {
      const ok = glit.has(i), cw = c === "R";
      if (ok) { gx.save(); gx.shadowColor = "#7dffb0"; gx.shadowBlur = r; gx.fillStyle = "#30a46c"; gx.beginPath(); gx.arc(x, y, r * .4, 0, Math.PI * 2); gx.fill(); gx.restore(); }
      gx.save(); gx.translate(x, y); if (!cw) gx.scale(-1, 1);       // one clockwise arrow, mirrored for CCW dials
      gx.strokeStyle = "#fff"; gx.lineWidth = r * .09; gx.lineCap = "round";
      const a0 = -Math.PI * .9, a1 = Math.PI * .2, ar = r * .26;
      gx.beginPath(); gx.arc(0, 0, ar, a0, a1); gx.stroke();
      const ex = Math.cos(a1) * ar, ey = Math.sin(a1) * ar, td = a1 + Math.PI / 2;
      gx.fillStyle = "#fff"; gx.beginPath(); gx.moveTo(ex + Math.cos(td) * r * .14, ey + Math.sin(td) * r * .14);
      gx.lineTo(ex + Math.cos(td + 2.3) * r * .12, ey + Math.sin(td + 2.3) * r * .12); gx.lineTo(ex + Math.cos(td - 2.3) * r * .12, ey + Math.sin(td - 2.3) * r * .12); gx.closePath(); gx.fill();
      gx.restore();
    }
  }
  for (const p of particles) { gx.globalAlpha = Math.max(0, 1 - p.t * p.t); gx.fillStyle = p.color; gx.beginPath(); gx.arc(p.x, p.y, p.size * (1 - p.t), 0, Math.PI * 2); gx.fill(); }
  gx.globalAlpha = 1;
}

// ---------------------------------------------------------------- map
let gworld = 1;
function showGearMap() {
  active = "gears"; closeOverlay();
  $("hub").hidden = true; $("gapp").hidden = true; $("gmap").hidden = false;
  const total = Object.values(GS.stars).reduce((a, b) => a + b, 0);
  $("g-total").textContent = `${total} / ${GLEVELS.length * 3} stars`;
  const next = Math.min(GS.highest + 1, GLEVELS.length);
  $("g-play").textContent = `Play level ${next}`; $("g-play").onclick = () => startGears(next);
  const tabs = $("g-tabs"); tabs.innerHTML = "";
  for (let w = 1; w <= Math.ceil(GLEVELS.length / 50); w++) {
    const b = document.createElement("button"); b.className = "tab"; b.setAttribute("role", "tab"); b.textContent = `World ${w}`;
    b.setAttribute("aria-selected", String(w === gworld)); b.onclick = () => { gworld = w; showGearMap(); }; tabs.appendChild(b);
  }
  const grid = $("g-grid"); grid.innerHTML = "";
  for (let n = (gworld - 1) * 50 + 1; n <= Math.min(gworld * 50, GLEVELS.length); n++) {
    const st = GS.stars[n] || 0, open = S.unlock || n <= GS.highest + 1, tier = GLEVELS[n - 1].tier;
    const b = document.createElement("button");
    b.className = "lvl gears" + (n === next && !st ? " next" : st ? " done" : "") + (open ? "" : " locked") + (tier === "H" ? " hard" : tier === "SH" ? " super" : "");
    b.innerHTML = open ? `${n}<span class="ls">${st ? "★".repeat(st) : ""}</span>` : `${n}`;
    b.disabled = !open; b.onclick = () => startGears(n);
    b.setAttribute("aria-label", `Gear level ${n}${st ? `, ${st} stars` : ""}${open ? "" : ", locked"}`);
    grid.appendChild(b);
  }
}

gc.addEventListener("pointerdown", e => {
  if (!glevel) return;
  const r = gc.getBoundingClientRect(), g = ggeom(), px0 = e.clientX - r.left, py0 = e.clientY - r.top;
  let best = -1, bd = 1e9;
  for (const c of glevel.pegs) { const p = gcenter(c, g), d = Math.hypot(p.x - px0, p.y - py0); if (d < bd) { bd = d; best = c; } }
  if (best >= 0 && bd < g.r) gtap(best);
});
window.addEventListener("resize", () => { if (!$("gapp").hidden) glayout(); });
$("g-menu").onclick = () => { audio(); showGearMap(); };
$("g-undo").onclick = gundo; $("g-hint").onclick = ghint; $("g-restart").onclick = () => startGears(gnum);
$("g-back").onclick = showHub;
$("card-gears").onclick = () => { audio(); if (GS.highest === 0) startGears(1); else showGearMap(); };
