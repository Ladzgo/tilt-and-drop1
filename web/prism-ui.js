// Prism Paths UI (browser playtest). Uses shared helpers from the main script: store, S, $, tween,
// later, ease, clock, sfx, tone, buzz, audio, particles, showCard, closeOverlay, info.
"use strict";
const PLEVELS = /*PRISM_LEVELS*/[];
const PS = { stars: store.get("pstars", {}), highest: store.get("phighest", 0), seen: store.get("pseen", {}) };
const psave = () => { store.set("pstars", PS.stars); store.set("phighest", PS.highest); store.set("pseen", PS.seen); };
const BEAM = { 1: "#ff3b5c", 2: "#2bff88", 4: "#3b8bff" };
const MIX = { 1: "#ff3b5c", 2: "#2bff88", 4: "#3b8bff", 3: "#ffe14d", 5: "#ff52ff", 6: "#3bffff", 7: "#ffffff" };
const MIX_NAME = { 1: "red", 2: "green", 4: "blue", 3: "yellow", 5: "magenta", 6: "cyan", 7: "white" };
const CHIME = { 1: 659.25, 2: 783.99, 4: 987.77 };
const P_INTROS = {
  11: ["Two colours", "Each target wants one exact colour. Route every beam home."],
  41: ["Mixing", "Light adds up. Red + blue = magenta, red + green = yellow, green + blue = cyan."],
  81: ["Fixed mirrors", "Mirrors in a square frame can't be turned. Work around them."],
  121: ["Filters", "A filter only lets light of its own colour through."],
  161: ["Splitters", "A splitter sends light straight on and sideways at the same time."],
};

const pc = $("prism-canvas"), px = pc.getContext("2d");
let PL = { w: 0, h: 0, dpr: 1 };
let plevel = null, pgame = null, pnum = 0, phist = [], pended = false, pcont = 0, ptaps = [];
let mirrorAngle = new Map();             // index -> displayed angle (radians)
let beamSegs = [], prevKeys = new Set(), revealAt = 0;
let litNow = new Set(), ppulse = new Map(), pflash = 0, hintIdx = -1, hintUntil = 0;

function playout() {
  const r = pc.getBoundingClientRect(), dpr = Math.min(3, window.devicePixelRatio || 1);
  pc.width = Math.round(r.width * dpr); pc.height = Math.round(r.height * dpr);
  PL = { w: r.width, h: r.height, dpr };
}
function geom() {
  const cs = Math.min(PL.w, PL.h) / (Math.max(plevel.w, plevel.h) + 0.5);
  return { cs, ox: PL.w / 2 - plevel.w * cs / 2, oy: PL.h / 2 - plevel.h * cs / 2 };
}
const angleFor = st => st === 0 ? -Math.PI / 4 : Math.PI / 4; // '/' and '\' in canvas space (y down)

function computeBeams(animated) {
  const segs = pgame.segments(), ends = new Map(), out = [];
  for (const [c, x0, y0, x1, y1] of segs) {
    const k0 = `${c}:${x0},${y0}`, d0 = ends.has(k0) ? ends.get(k0) : 0, len = Math.abs(x1 - x0) + Math.abs(y1 - y0);
    out.push({ c, x0, y0, x1, y1, d0, len, key: `${c}:${x0},${y0}>${x1},${y1}` });
    ends.set(`${c}:${x1},${y1}`, d0 + len);
  }
  // unchanged segments stay lit; new light grows from the first point where it changed
  const minNew = {};
  for (const s of out) if (!prevKeys.has(s.key)) minNew[s.c] = Math.min(minNew[s.c] ?? 1e9, s.d0);
  for (const s of out) s.offset = animated && !prevKeys.has(s.key) ? s.d0 - (minNew[s.c] ?? 0) : -1;
  beamSegs = out;
  prevKeys = new Set(out.map(s => s.key));
  revealAt = clock;
}

function startPrism(n, replay = []) {
  active = "prism";
  pnum = n; plevel = PR.parseLevel(PLEVELS[n - 1]); pgame = new PR.Game(plevel);
  phist = []; pended = false; pcont = 0; ptaps = []; hintIdx = -1; pflash = 0; ppulse = new Map();
  mirrorAngle = new Map(plevel.rotatables.map(i => [i, angleFor(plevel.cells[i][1])]));
  prevKeys = new Set();
  ["hub", "pmap", "rapp", "rmap", "sapp", "smap", "gapp", "gmap", "dapp", "dmap", "fapp", "fmap", "wapp", "wmap"].forEach(id => { $(id).hidden = true; }); $("papp").hidden = false; closeOverlay();
  playout();
  for (const t of replay) { phist.push(pgame.clone()); pgame.tap(t); ptaps.push(t); mirrorAngle.set(t, angleFor(pgame.states.get(t))); }
  computeBeams(!replay.length);
  litNow = new Set(plevel.targets.filter(i => pgame.received[i] === plevel.cells[i][1]));
  $("p-title").textContent = `Level ${n}`;
  const tier = plevel.tier, tt = tier === "SH" ? "Super hard" : tier === "H" ? "Hard" : "";
  $("p-sub").innerHTML = `Prism · world ${Math.ceil(n / 50)}` + (tt ? ` · <span class="tier-${tier}">${tt}</span>` : "");
  pupdate();
  pbanner(n === 1 ? "Tap a mirror to flip it. Guide the light into the ring of the same colour." :
    n === 2 ? "Every tap costs one move. Plan before you tap." : "");
  if (S.showSol) pbanner(`Par ${plevel.par} · tap the pulsing mirrors`), showSolution();
  if (P_INTROS[n] && !PS.seen[n] && !replay.length) { PS.seen[n] = 1; psave(); info(P_INTROS[n][0], P_INTROS[n][1], "Got it"); }
}
function pbanner(t) { $("p-banner").textContent = t; }
function pupdate() {
  $("p-moves-n").textContent = pgame.movesLeft;
  $("p-moves").classList.toggle("low", pgame.movesLeft <= 1);
  const lit = plevel.targets.filter(i => pgame.received[i] === plevel.cells[i][1]).length;
  $("p-lit").textContent = `${lit} / ${plevel.targets.length} lit`;
}
function showSolution() {
  const s = PR.solve(plevel, pgame.states);
  if (s.par !== null && s.taps.length) { hintIdx = s.taps[0]; hintUntil = clock + 1e9; }
}

function ptap(i) {
  if (!pgame || pended || pgame.status !== "playing" || !$("overlay").hidden) return;
  audio();
  if (!pgame.states.has(i)) return;
  phist.push(pgame.clone());
  const from = mirrorAngle.get(i);
  pgame.tap(i); ptaps.push(i);
  const to = from + Math.PI / 2 * (pgame.states.get(i) === 1 ? 1 : -1);
  tween(.2, p => mirrorAngle.set(i, from + (to - from) * p), ease.outBack, 0, () => mirrorAngle.set(i, angleFor(pgame.states.get(i))));
  tone(1400 + (i % 7) * 90, .12, "sine", .12); tone(2800 + (i % 7) * 180, .08, "sine", .04);
  buzz(8);
  if (hintIdx === i) hintIdx = -1;
  computeBeams(true);
  // targets that just lit / went muddy
  const nowLit = new Set(plevel.targets.filter(t => pgame.received[t] === plevel.cells[t][1]));
  let k = 0;
  for (const t of nowLit) if (!litNow.has(t)) {
    const req = plevel.cells[t][1], delay = .12 + .05 * k++;
    later(delay, () => {
      [1, 2, 4].filter(c => req & c).forEach((c, j) => tone(CHIME[c], .6, "triangle", .14, j * .03));
      ppulse.set(t, clock); buzz(15);
    });
  }
  litNow = nowLit;
  pupdate();
  if (S.showSol && pgame.status === "playing") showSolution();
  if (pgame.status === "won") pwin(); else if (pgame.status === "lost") later(.5, pfail);
}

function pstars() {
  if (pcont) return 1;
  const slack = Math.max(0, plevel.moves - plevel.par);
  return pgame.movesUsed <= plevel.par ? 3 : pgame.movesUsed <= plevel.par + Math.ceil(slack / 2) ? 2 : 1;
}
function pwin() {
  pended = true;
  const stars = pstars();
  PS.stars[pnum] = Math.max(PS.stars[pnum] || 0, stars); PS.highest = Math.max(PS.highest, pnum); psave();
  later(.35, () => {
    sfx.win(); buzz([20, 40, 20]);
    tween(.6, p => { pflash = Math.sin(p * Math.PI); }, ease.lin, 0, () => { pflash = 0; });
    const { cs, ox, oy } = geom();
    for (const t of plevel.targets) {
      const x = ox + (t % plevel.w + .5) * cs, y = oy + (Math.floor(t / plevel.w) + .5) * cs, col = MIX[plevel.cells[t][1]];
      for (let j = 0; j < 14; j++) {
        const a = Math.random() * Math.PI * 2, v = cs * (1 + Math.random() * 2.5);
        particles.push({ x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v - cs, t: 0, life: .6 + Math.random() * .4, size: cs * .08, color: j % 3 ? col : "#fff", star: j % 4 === 0, rot: 0, spin: 6 });
      }
    }
  });
  later(1.0, () => {
    const hasNext = pnum < PLEVELS.length;
    showCard(`<h2>Level complete!</h2><div class="stars" id="pw-stars">${starSvg(false)}${starSvg(false)}${starSvg(false)}</div>
      <p>${pgame.movesUsed} taps · par ${plevel.par}${pcont ? " · with extra moves" : ""}</p>
      ${hasNext ? `<button class="big go" id="pb-next">Next level</button>` : ""}<button class="big" id="pb-map">Levels</button>`);
    const svgs = document.querySelectorAll("#pw-stars svg");
    for (let i = 0; i < stars; i++) later(.2 + .22 * i, () => { svgs[i].outerHTML = starSvg(true); sfx.star(i); });
    if (hasNext) $("pb-next").onclick = () => startPrism(pnum + 1);
    $("pb-map").onclick = showPrismMap;
  });
}
function pfail() {
  pended = true; sfx.fail(); buzz([40, 30, 40]);
  const lit = plevel.targets.filter(i => pgame.received[i] === plevel.cells[i][1]).length;
  showCard(`<h2>Out of moves</h2><p>${lit} of ${plevel.targets.length} targets lit. In the app, extra moves cost an ad or coins; here they are free.</p>
    ${pcont < 2 ? `<button class="big gold" id="pb-cont">+3 moves</button>` : ""}
    <button class="big blue" id="pb-retry">Retry</button><button class="big" id="pb-map">Levels</button>`);
  if (pcont < 2) $("pb-cont").onclick = () => { closeOverlay(); pgame.addMoves(3); pcont++; pended = false; pupdate(); };
  $("pb-retry").onclick = () => startPrism(pnum);
  $("pb-map").onclick = showPrismMap;
}
function pundo() {
  if (pended || !phist.length) return;
  pgame = phist.pop(); ptaps.pop();
  for (const i of plevel.rotatables) mirrorAngle.set(i, angleFor(pgame.states.get(i)));
  computeBeams(false);
  litNow = new Set(plevel.targets.filter(t => pgame.received[t] === plevel.cells[t][1]));
  pupdate(); tone(900, .1, "sine", .08);
}
function phint() {
  if (pended) return;
  const s = PR.solve(plevel, pgame.states);
  if (s.par === null) return pbanner("No solution from here. Try Undo or Restart.");
  if (!s.taps.length) return;
  hintIdx = s.taps[0]; hintUntil = clock + 2.5;
  pbanner(s.par <= pgame.movesLeft ? `Tap the glowing mirror · ${s.par} tap${s.par > 1 ? "s" : ""} to solve` : `Tap the glowing mirror · needs ${s.par} taps, you have ${pgame.movesLeft}`);
}

// ---------------------------------------------------------------- drawing
function prr(x, y, w, h, r) { px.beginPath(); px.roundRect ? px.roundRect(x, y, w, h, r) : px.rect(x, y, w, h); }
function prismDraw() {
  if (!plevel) return;
  if (pc.width === 0) playout();
  const { w: W, h: H, dpr } = PL, { cs, ox, oy } = geom(), lv = plevel;
  px.setTransform(dpr, 0, 0, dpr, 0, 0);
  px.clearRect(0, 0, W, H);
  const cx = i => ox + (i % lv.w + .5) * cs, cy = i => oy + (Math.floor(i / lv.w) + .5) * cs;
  // board
  px.fillStyle = "#14122a"; prr(ox - cs * .2, oy - cs * .2, lv.w * cs + cs * .4, lv.h * cs + cs * .4, cs * .3); px.fill();
  for (let i = 0; i < lv.w * lv.h; i++) {
    const k = lv.cells[i][0];
    px.fillStyle = k === PR.WALL ? "#3a3560" : "#1c1a36";
    prr(cx(i) - cs * .46, cy(i) - cs * .46, cs * .92, cs * .92, cs * .14); px.fill();
  }
  // beams (additive light: overlapping red + blue really reads as magenta)
  const t = (clock - revealAt) * 42;
  px.save();
  px.globalCompositeOperation = "lighter";
  px.lineCap = "round";
  for (const s of beamSegs) {
    const vis = s.offset < 0 ? s.len : Math.max(0, Math.min(s.len, t - s.offset));
    if (vis <= 0) continue;
    const f = s.len ? vis / s.len : 1;
    const x0 = ox + (s.x0 + .5) * cs, y0 = oy + (s.y0 + .5) * cs;
    const x1 = x0 + (s.x1 - s.x0) * cs * f, y1 = y0 + (s.y1 - s.y0) * cs * f;
    const col = BEAM[s.c];
    for (const [lw, a] of [[.42, .16], [.22, .35], [.09, 1]]) {
      px.strokeStyle = col; px.globalAlpha = a * (1 + pflash * .8); px.lineWidth = cs * lw;
      px.beginPath(); px.moveTo(x0, y0); px.lineTo(x1, y1); px.stroke();
    }
  }
  px.restore();
  // pieces
  for (let i = 0; i < lv.w * lv.h; i++) {
    const [k, v] = lv.cells[i], x = cx(i), y = cy(i);
    if (k === PR.SOURCE) {
      const col = BEAM[v >> 2], d = v & 3;
      px.fillStyle = col; prr(x - cs * .36, y - cs * .36, cs * .72, cs * .72, cs * .18); px.fill();
      px.save(); px.translate(x, y); px.rotate(d * Math.PI / 2);
      px.fillStyle = "rgba(255,255,255,.95)"; px.beginPath(); px.moveTo(cs * .26, 0); px.lineTo(-cs * .08, -cs * .18); px.lineTo(-cs * .08, cs * .18); px.closePath(); px.fill();
      px.restore();
    } else if (k === PR.TARGET) {
      const req = v, got = pgame.received[i], ok = got === req, muddy = got && (got & ~req);
      const pt = ppulse.has(i) ? Math.max(0, 1 - (clock - ppulse.get(i)) / .5) : 0;
      const r = cs * (.34 + .08 * pt);
      if (ok) { px.save(); px.shadowColor = MIX[req]; px.shadowBlur = cs * (.5 + pt + pflash); px.fillStyle = MIX[req]; px.beginPath(); px.arc(x, y, r * .8, 0, Math.PI * 2); px.fill(); px.restore(); }
      else if (got && !muddy) { px.globalAlpha = .45; px.fillStyle = MIX[got]; px.beginPath(); px.arc(x, y, r * .55, 0, Math.PI * 2); px.fill(); px.globalAlpha = 1; }
      px.lineWidth = cs * .09; px.strokeStyle = MIX[req]; px.beginPath(); px.arc(x, y, r, 0, Math.PI * 2); px.stroke();
      // colour-blind cue: one dot per required primary
      const parts = [1, 2, 4].filter(c => req & c);
      parts.forEach((c, j) => { px.fillStyle = ok ? "#14122a" : BEAM[c]; px.beginPath(); px.arc(x + (j - (parts.length - 1) / 2) * cs * .15, y, cs * .045, 0, Math.PI * 2); px.fill(); });
      if (muddy) { px.strokeStyle = "#9a96b8"; px.lineWidth = cs * .06; px.beginPath(); px.moveTo(x - r * .5, y - r * .5); px.lineTo(x + r * .5, y + r * .5); px.moveTo(x + r * .5, y - r * .5); px.lineTo(x - r * .5, y + r * .5); px.stroke(); }
    } else if (k === PR.MIRROR || k === PR.SPLITTER || k === PR.FIXED) {
      const rot = k !== PR.FIXED, ang = rot ? mirrorAngle.get(i) : angleFor(v);
      if (rot) {
        const hint = i === hintIdx && clock < hintUntil;
        px.fillStyle = hint ? "rgba(255,197,61,.35)" : "rgba(255,255,255,.06)";
        px.beginPath(); px.arc(x, y, cs * (hint ? .44 + .04 * Math.sin(clock * 10) : .4), 0, Math.PI * 2); px.fill();
        if (hint) { px.strokeStyle = "#ffc53d"; px.lineWidth = cs * .05; px.stroke(); }
      } else { px.strokeStyle = "#6b6696"; px.lineWidth = cs * .05; prr(x - cs * .4, y - cs * .4, cs * .8, cs * .8, cs * .08); px.stroke(); }
      px.save(); px.translate(x, y); px.rotate(ang);
      if (k === PR.SPLITTER) { px.setLineDash([cs * .1, cs * .07]); px.strokeStyle = "rgba(200,230,255,.9)"; }
      else px.strokeStyle = "#e9ecff";
      px.lineWidth = cs * .1; px.lineCap = "round"; px.beginPath(); px.moveTo(-cs * .36, 0); px.lineTo(cs * .36, 0); px.stroke();
      px.setLineDash([]); px.strokeStyle = "rgba(120,140,255,.6)"; px.lineWidth = cs * .03; px.beginPath(); px.moveTo(-cs * .3, cs * .06); px.lineTo(cs * .3, cs * .06); px.stroke();
      px.restore();
    } else if (k === PR.FILTER) {
      const col = BEAM[v];
      px.globalAlpha = .35; px.fillStyle = col; prr(x - cs * .38, y - cs * .38, cs * .76, cs * .76, cs * .12); px.fill(); px.globalAlpha = 1;
      px.strokeStyle = col; px.lineWidth = cs * .05; prr(x - cs * .38, y - cs * .38, cs * .76, cs * .76, cs * .12); px.stroke();
      for (let j = -1; j <= 1; j++) { px.beginPath(); px.moveTo(x + j * cs * .16, y - cs * .28); px.lineTo(x + j * cs * .16, y + cs * .28); px.stroke(); }
    }
  }
  // particles (shared pool)
  for (const p of particles) {
    px.globalAlpha = Math.max(0, 1 - p.t * p.t); px.fillStyle = p.color;
    px.beginPath(); px.arc(p.x, p.y, p.size * (1 - p.t), 0, Math.PI * 2); px.fill();
  }
  px.globalAlpha = 1;
}

// ---------------------------------------------------------------- maps & hub
let pworld = 1;
function showPrismMap() {
  active = "prism"; closeOverlay();
  $("hub").hidden = true; $("pmap").hidden = false;
  const total = Object.values(PS.stars).reduce((a, b) => a + b, 0);
  $("p-total").textContent = `${total} / ${PLEVELS.length * 3} stars`;
  const next = Math.min(PS.highest + 1, PLEVELS.length);
  $("p-play").textContent = `Play level ${next}`;
  $("p-play").onclick = () => startPrism(next);
  const tabs = $("p-tabs"); tabs.innerHTML = "";
  for (let w = 1; w <= Math.ceil(PLEVELS.length / 50); w++) {
    const b = document.createElement("button"); b.className = "tab"; b.setAttribute("role", "tab"); b.textContent = `World ${w}`;
    b.setAttribute("aria-selected", String(w === pworld)); b.onclick = () => { pworld = w; showPrismMap(); }; tabs.appendChild(b);
  }
  const grid = $("p-grid"); grid.innerHTML = "";
  for (let n = (pworld - 1) * 50 + 1; n <= Math.min(pworld * 50, PLEVELS.length); n++) {
    const st = PS.stars[n] || 0, open = S.unlock || n <= PS.highest + 1, tier = PLEVELS[n - 1].tier;
    const b = document.createElement("button");
    b.className = "lvl prism" + (n === next && !st ? " next" : st ? " done" : "") + (open ? "" : " locked") + (tier === "H" ? " hard" : tier === "SH" ? " super" : "");
    b.innerHTML = open ? `${n}<span class="ls">${st ? "★".repeat(st) : ""}</span>` : `${n}`;
    b.disabled = !open; b.onclick = () => startPrism(n);
    b.setAttribute("aria-label", `Prism level ${n}${st ? `, ${st} stars` : ""}${open ? "" : ", locked"}`);
    grid.appendChild(b);
  }
}
function showHub() {
  closeOverlay();
  ["map", "pmap", "papp", "rmap", "rapp", "smap", "sapp", "gmap", "gapp", "dmap", "dapp", "fapp", "fmap", "wapp", "wmap"].forEach(id => { $(id).hidden = true; }); $("hub").hidden = false;
  const ts = Object.values(S.stars).reduce((a, b) => a + b, 0), ps = Object.values(PS.stars).reduce((a, b) => a + b, 0);
  $("hub-tilt").textContent = `${ts} / 900 stars · level ${Math.min(S.highest + 1, LEVELS.length)}`;
  const rsum = Object.values(RS.stars).reduce((a, b) => a + b, 0);
  $("hub-roll").textContent = `${rsum} / ${RLEVELS.length * 3} stars · level ${Math.min(RS.highest + 1, RLEVELS.length)}`;
  const ssum = Object.values(SS.stars).reduce((a, b) => a + b, 0);
  $("hub-slide").textContent = `${ssum} / ${SLEVELS.length * 3} stars · level ${Math.min(SS.highest + 1, SLEVELS.length)}`;
  const gsum = Object.values(GS.stars).reduce((a, b) => a + b, 0);
  $("hub-gears").textContent = `${gsum} / ${GLEVELS.length * 3} stars · level ${Math.min(GS.highest + 1, GLEVELS.length)}`;
  const dsum = Object.values(DS.stars).reduce((a, b) => a + b, 0);
  $("hub-domino").textContent = `${dsum} / ${DLEVELS.length * 3} stars · level ${Math.min(DS.highest + 1, DLEVELS.length)}`;
  const fsum = Object.values(FS_.stars).reduce((a, b) => a + b, 0);
  $("hub-fold").textContent = `${fsum} / ${FLEVELS.length * 3} stars · level ${Math.min(FS_.highest + 1, FLEVELS.length)}`;
  const wsum = Object.values(WS_.stars).reduce((a, b) => a + b, 0);
  $("hub-flow").textContent = `${wsum} / ${WLEVELS.length * 3} stars · level ${Math.min(WS_.highest + 1, WLEVELS.length)}`;
  $("hub-prism").textContent = `${ps} / ${PLEVELS.length * 3} stars · level ${Math.min(PS.highest + 1, PLEVELS.length)}`;
}

pc.addEventListener("pointerdown", e => {
  if (!plevel) return;
  const r = pc.getBoundingClientRect(), { cs, ox, oy } = geom();
  const x = Math.floor((e.clientX - r.left - ox) / cs), y = Math.floor((e.clientY - r.top - oy) / cs);
  if (x >= 0 && y >= 0 && x < plevel.w && y < plevel.h) ptap(y * plevel.w + x);
});
window.addEventListener("resize", () => { if (!$("papp").hidden) playout(); });
$("p-menu").onclick = () => { audio(); showPrismMap(); };
$("p-undo").onclick = pundo;
$("p-hint").onclick = phint;
$("p-restart").onclick = () => startPrism(pnum);
$("p-back").onclick = showHub;
$("btn-games").onclick = showHub;
$("card-tilt").onclick = () => { audio(); active = "tilt"; $("hub").hidden = true; if (S.highest === 0) startLevel(1); else showMap(); };
$("card-prism").onclick = () => { audio(); if (PS.highest === 0) startPrism(1); else showPrismMap(); };
