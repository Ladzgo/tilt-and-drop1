// Domino Run UI (browser playtest). Uses shared helpers from the main script.
"use strict";
const DLEVELS = /*DOMINO_LEVELS*/[];
const DS = { stars: store.get("dstars", {}), highest: store.get("dhighest", 0), seen: store.get("dseen", {}) };
const dsave = () => { store.set("dstars", DS.stars); store.set("dhighest", DS.highest); store.set("dseen", DS.seen); };
const D_INTROS = {
  11: ["Corners", "Several turners now. Some of them are decoys: not every arrow is on the path."],
  41: ["Splitters", "A splitter sends the chain both ways at once. Two bells, one push."],
  81: ["Fixed turners", "Framed turners can't be rotated. Plan around them."],
  121: ["Long chains", "Long runs and forks. Count where each branch goes."],
  161: ["Review", "Everything together."],
};
const dc_ = $("domino-canvas"), dx_ = dc_.getContext("2d");
let DL = { w: 0, h: 0, dpr: 1 };
let dlevel = null, dgame = null, dnum = 0, dended = false, drunning = false, dacts = [];
let dfallen = new Map(), drung = new Map(), dspin = new Map(), dhintCell = -1, dhintUntil = 0;

function dlayout() { const r = dc_.getBoundingClientRect(), dpr = Math.min(3, window.devicePixelRatio || 1); dc_.width = Math.round(r.width * dpr); dc_.height = Math.round(r.height * dpr); DL = { w: r.width, h: r.height, dpr }; }
function dgeom() { const cs = Math.min(DL.w / (dlevel.w + .4), DL.h / (dlevel.h + .4)); return { cs, ox: DL.w / 2 - dlevel.w * cs / 2, oy: DL.h / 2 - dlevel.h * cs / 2 }; }

function startDomino(n, replay = []) {
  active = "domino";
  dnum = n; dlevel = DM.parseLevel(DLEVELS[n - 1]); dgame = new DM.Game(dlevel);
  dended = false; drunning = false; dacts = []; dfallen = new Map(); drung = new Map(); dspin = new Map(); dhintCell = -1;
  ["hub", "map", "pmap", "papp", "rmap", "rapp", "smap", "sapp", "gmap", "gapp", "dmap"].forEach(id => { $(id).hidden = true; });
  $("dapp").hidden = false; closeOverlay(); dlayout();
  for (const a of replay) { if (a === "P") { if (dgame.status === "playing") dgame.push(); } else dgame.rotate(a); dacts.push(a); }
  $("d-title").textContent = `Level ${n}`;
  const tier = dlevel.tier, tt = tier === "SH" ? "Super hard" : tier === "H" ? "Hard" : "";
  $("d-sub").innerHTML = `Domino Run · world ${Math.ceil(n / 50)}` + (tt ? ` · <span class="tier-${tier}">${tt}</span>` : "");
  dupdate();
  dbanner(n === 1 ? "Tap the arrow domino to turn it, then press Push. Ring every bell." :
    n === 2 ? "Turning is free. You get three pushes." : "");
  if (S.showSol) dbanner(`Solution: set the pulsing turners, ${dlevel.par} tap${dlevel.par === 1 ? "" : "s"}`), dshowSol();
  if (D_INTROS[n] && !DS.seen[n] && !replay.length) { DS.seen[n] = 1; dsave(); info(D_INTROS[n][0], D_INTROS[n][1], "Got it"); }
}
function dbanner(t) { $("d-banner").textContent = t; }
function dupdate() {
  $("d-push-n").textContent = dgame.attemptsLeft;
  $("d-bells").textContent = `${dlevel.bells.length} bell${dlevel.bells.length > 1 ? "s" : ""} to ring`;
  $("d-push").disabled = drunning || dended;
}
function dshowSol() {
  const s = DM.solve(dlevel, dgame.config);
  if (s.best) { const k = s.best.findIndex((v, i) => v !== dgame.config[i]); dhintCell = k >= 0 ? dlevel.turners[k] : -1; dhintUntil = clock + 1e9; }
}

function dtap(cell) {
  if (!dgame || dended || drunning || !$("overlay").hidden || dgame.status !== "playing") return;
  audio();
  if (dfallen.size) { dfallen = new Map(); drung = new Map(); }
  if (!dgame.rotate(cell)) return;
  dacts.push(cell);
  const from = dspin.has(cell) ? dspin.get(cell).to : 0;
  dspin.set(cell, { t: clock, to: from + Math.PI / 2 });
  tone(1200, .05, "triangle", .08); buzz(5);
  if (dhintCell === cell) dhintCell = -1;
  if (S.showSol) dshowSol();
}
function dpush() {
  if (!dgame || dended || drunning || dgame.status !== "playing") return;
  audio();
  dfallen = new Map(); drung = new Map();
  const { falls, rung } = dgame.push();
  dacts.push("P");
  drunning = true; dupdate();
  const step = .1 / speed();
  let lastT = 0;
  falls.forEach(([t, c, outs], j) => {
    lastT = Math.max(lastT, t);
    later(t * step, () => {
      dfallen.set(c, { t: clock, dir: outs.length ? outs[0] : -1, outs });
      if (dlevel.cells[c][0] === "B") { drung.set(c, clock); [1046.5, 1318.5, 1568].forEach((f, k) => tone(f * (1 + .05 * (c % 4)), .9, "sine", .1, k * .02)); buzz(15); }
      else { tone(500 + 18 * Math.min(t, 30), .05, "square", .07); noise(.04, .1, 3000, 1500); if (j % 2 === 0) buzz(4); }
    });
  });
  later(lastT * step + .35, () => {
    drunning = false; dupdate();
    const rang = dlevel.bells.filter(b => rung.has(b)).length;
    if (dgame.status === "won") return dwin();
    dbanner(`${rang} of ${dlevel.bells.length} bells rang. ${dgame.attemptsLeft} push${dgame.attemptsLeft === 1 ? "" : "es"} left.`);
    if (dgame.status === "lost") return later(.4, dfail);
  });
}
function dwin() {
  dended = true;
  const stars = Math.max(1, 4 - dgame.pushes);
  DS.stars[dnum] = Math.max(DS.stars[dnum] || 0, stars); DS.highest = Math.max(DS.highest, dnum); dsave();
  sfx.win(); buzz([20, 40, 20]);
  later(.7, () => {
    const hasNext = dnum < DLEVELS.length;
    showCard(`<h2>All bells rang!</h2><div class="stars" id="dw-stars">${starSvg(false)}${starSvg(false)}${starSvg(false)}</div>
      <p>Solved on push ${dgame.pushes}</p>
      ${hasNext ? `<button class="big go" id="db-next">Next level</button>` : ""}<button class="big" id="db-map">Levels</button>`);
    const svgs = document.querySelectorAll("#dw-stars svg");
    for (let i = 0; i < stars; i++) later(.2 + .22 * i, () => { svgs[i].outerHTML = starSvg(true); sfx.star(i); });
    if (hasNext) $("db-next").onclick = () => startDomino(dnum + 1);
    $("db-map").onclick = showDominoMap;
  });
}
function dfail() {
  dended = true; sfx.fail(); buzz([40, 30, 40]);
  showCard(`<h2>Out of pushes</h2><p>Not every bell rang. In the app, another push costs an ad or coins; here it is free.</p>
    <button class="big gold" id="db-cont">+1 push</button><button class="big blue" id="db-retry">Retry</button><button class="big" id="db-map">Levels</button>`);
  $("db-cont").onclick = () => { closeOverlay(); dgame.addAttempt(); dended = false; dfallen = new Map(); drung = new Map(); dupdate(); };
  $("db-retry").onclick = () => startDomino(dnum);
  $("db-map").onclick = showDominoMap;
}
function dhint() {
  if (dended || drunning) return;
  const s = DM.solve(dlevel, dgame.config);
  if (!s.best) return dbanner("No arrangement rings every bell.");
  const k = s.best.findIndex((v, i) => v !== dgame.config[i]);
  if (k < 0) return dbanner("Your arrows are right. Press Push!");
  dhintCell = dlevel.turners[k]; dhintUntil = clock + 2.5;
  dbanner(`Turn the glowing domino · ${s.par} tap${s.par > 1 ? "s" : ""} from a solution`);
}

// ---------------------------------------------------------------- drawing
function drr(x, y, w, h, r) { dx_.beginPath(); dx_.roundRect ? dx_.roundRect(x, y, w, h, r) : dx_.rect(x, y, w, h); }
function dominoDraw() {
  if (!dlevel) return;
  if (dc_.width === 0) dlayout();
  const { w: W, h: H, dpr } = DL, { cs, ox, oy } = dgeom();
  dx_.setTransform(dpr, 0, 0, dpr, 0, 0); dx_.clearRect(0, 0, W, H);
  dx_.fillStyle = "#3b2a22"; drr(ox - cs * .15, oy - cs * .15, dlevel.w * cs + cs * .3, dlevel.h * cs + cs * .3, cs * .25); dx_.fill();
  dx_.fillStyle = "#4a352b";
  for (let i = 0; i < dlevel.w * dlevel.h; i++) if ((i % dlevel.w + Math.floor(i / dlevel.w)) % 2) dx_.fillRect(ox + (i % dlevel.w) * cs, oy + Math.floor(i / dlevel.w) * cs, cs, cs);
  for (let i = 0; i < dlevel.w * dlevel.h; i++) {
    const [k, a] = dlevel.cells[i];
    if (k === ".") continue;
    const cx = ox + (i % dlevel.w + .5) * cs, cy = oy + (Math.floor(i / dlevel.w) + .5) * cs;
    if (k === "#") { dx_.fillStyle = "#2a1d17"; drr(cx - cs * .46, cy - cs * .46, cs * .92, cs * .92, cs * .1); dx_.fill(); continue; }
    const f = dfallen.get(i);
    if (k === "B") {
      const rt = drung.has(i) ? clock - drung.get(i) : -1, wob = rt >= 0 && rt < 1 ? Math.sin(rt * 30) * (1 - rt) * .35 : 0;
      if (rt >= 0 && rt < 1) { dx_.strokeStyle = `rgba(255,197,61,${1 - rt})`; dx_.lineWidth = 3; dx_.beginPath(); dx_.arc(cx, cy, cs * (.35 + rt * .5), 0, Math.PI * 2); dx_.stroke(); }
      dx_.save(); dx_.translate(cx, cy - cs * .05); dx_.rotate(wob);
      dx_.fillStyle = rt >= 0 ? "#ffd75a" : "#c9a227"; dx_.beginPath(); dx_.moveTo(-cs * .28, cs * .22); dx_.quadraticCurveTo(-cs * .26, -cs * .3, 0, -cs * .3); dx_.quadraticCurveTo(cs * .26, -cs * .3, cs * .28, cs * .22); dx_.closePath(); dx_.fill();
      dx_.fillStyle = "#8a6d1f"; dx_.beginPath(); dx_.arc(0, cs * .26, cs * .07, 0, Math.PI * 2); dx_.fill();
      dx_.restore();
      continue;
    }
    // standing piece or fallen piece
    let dirAng = 0, tilt = 0;
    if (f) { const p = Math.min(1, (clock - f.t) / (.09 / speed())); tilt = ease.outQuad(p); dirAng = f.dir >= 0 ? f.dir * Math.PI / 2 : 0; }
    dx_.save(); dx_.translate(cx, cy);
    if (f && f.dir >= 0) {
      dx_.rotate(dirAng);
      const len = cs * (.5 + .45 * tilt), wid = cs * .5;
      dx_.fillStyle = "rgba(0,0,0,.35)"; drr(-wid * .5 + 3, -wid / 2 + 4, len, wid, cs * .08); dx_.fill();
      dx_.fillStyle = k === "S" ? "#30a46c" : k === "Y" ? "#8e4ec6" : k === "t" || k === "T" ? "#3e8ef7" : "#f2ede4";
      drr(-wid * .5, -wid / 2, len, wid, cs * .08); dx_.fill();
      dx_.fillStyle = "rgba(0,0,0,.25)"; dx_.fillRect(-wid * .5 + len / 2 - 1, -wid / 2 + 3, 2, wid - 6);
    } else {
      const s = cs * .62;
      dx_.fillStyle = "rgba(0,0,0,.35)"; drr(-s / 2 + 3, -s / 2 + 5, s, s, cs * .1); dx_.fill();
      const base = k === "S" ? "#30a46c" : k === "Y" ? "#8e4ec6" : k === "t" || k === "T" ? "#3e8ef7" : "#f2ede4";
      if (i === dhintCell && clock < dhintUntil) { dx_.fillStyle = `rgba(255,197,61,${.4 + .3 * Math.sin(clock * 10)})`; drr(-cs * .45, -cs * .45, cs * .9, cs * .9, cs * .15); dx_.fill(); }
      dx_.fillStyle = base; drr(-s / 2, -s / 2, s, s, cs * .1); dx_.fill();
      if (k === "T") { dx_.strokeStyle = "#1d3c66"; dx_.lineWidth = 3; drr(-s / 2 + 2, -s / 2 + 2, s - 4, s - 4, cs * .08); dx_.stroke(); }
      if (k === "d") { dx_.fillStyle = "#3b2a22"; for (const [px, py] of [[-.12, -.12], [.12, .12], [.12, -.12], [-.12, .12]]) { dx_.beginPath(); dx_.arc(px * cs, py * cs, cs * .045, 0, Math.PI * 2); dx_.fill(); } }
      if (k === "Y") { dx_.strokeStyle = "#fff"; dx_.lineWidth = cs * .07; dx_.lineCap = "round"; dx_.beginPath(); dx_.moveTo(0, cs * .16); dx_.lineTo(0, 0); dx_.lineTo(-cs * .14, -cs * .15); dx_.moveTo(0, 0); dx_.lineTo(cs * .14, -cs * .15); dx_.stroke(); }
      if (k === "t" || k === "T" || k === "S") {
        let ang = (k === "t" ? dgame.config[dlevel.turners.indexOf(i)] : a) * Math.PI / 2;
        const sp = dspin.get(i);
        if (k === "t" && sp) { const p = Math.min(1, (clock - sp.t) / .16); ang -= (1 - ease.outBack(p)) * Math.PI / 2; }
        dx_.rotate(ang);
        dx_.fillStyle = "#fff"; dx_.beginPath(); dx_.moveTo(s * .32, 0); dx_.lineTo(-s * .16, -s * .26); dx_.lineTo(-s * .04, 0); dx_.lineTo(-s * .16, s * .26); dx_.closePath(); dx_.fill();
      }
    }
    dx_.restore();
  }
}

// ---------------------------------------------------------------- map
let dworld = 1;
function showDominoMap() {
  active = "domino"; closeOverlay();
  $("hub").hidden = true; $("dapp").hidden = true; $("dmap").hidden = false;
  const total = Object.values(DS.stars).reduce((a, b) => a + b, 0);
  $("d-total").textContent = `${total} / ${DLEVELS.length * 3} stars`;
  const next = Math.min(DS.highest + 1, DLEVELS.length);
  $("d-play").textContent = `Play level ${next}`; $("d-play").onclick = () => startDomino(next);
  const tabs = $("d-tabs"); tabs.innerHTML = "";
  for (let w = 1; w <= Math.ceil(DLEVELS.length / 50); w++) {
    const b = document.createElement("button"); b.className = "tab"; b.setAttribute("role", "tab"); b.textContent = `World ${w}`;
    b.setAttribute("aria-selected", String(w === dworld)); b.onclick = () => { dworld = w; showDominoMap(); }; tabs.appendChild(b);
  }
  const grid = $("d-grid"); grid.innerHTML = "";
  for (let n = (dworld - 1) * 50 + 1; n <= Math.min(dworld * 50, DLEVELS.length); n++) {
    const st = DS.stars[n] || 0, open = S.unlock || n <= DS.highest + 1, tier = DLEVELS[n - 1].tier;
    const b = document.createElement("button");
    b.className = "lvl domino" + (n === next && !st ? " next" : st ? " done" : "") + (open ? "" : " locked") + (tier === "H" ? " hard" : tier === "SH" ? " super" : "");
    b.innerHTML = open ? `${n}<span class="ls">${st ? "★".repeat(st) : ""}</span>` : `${n}`;
    b.disabled = !open; b.onclick = () => startDomino(n);
    b.setAttribute("aria-label", `Domino level ${n}${st ? `, ${st} stars` : ""}${open ? "" : ", locked"}`);
    grid.appendChild(b);
  }
}

dc_.addEventListener("pointerdown", e => {
  if (!dlevel) return;
  const r = dc_.getBoundingClientRect(), { cs, ox, oy } = dgeom();
  const x = Math.floor((e.clientX - r.left - ox) / cs), y = Math.floor((e.clientY - r.top - oy) / cs);
  if (x >= 0 && y >= 0 && x < dlevel.w && y < dlevel.h) dtap(y * dlevel.w + x);
});
window.addEventListener("resize", () => { if (!$("dapp").hidden) dlayout(); });
$("d-menu").onclick = () => { audio(); showDominoMap(); };
$("d-push").onclick = dpush; $("d-hint").onclick = dhint; $("d-restart").onclick = () => startDomino(dnum);
$("d-back").onclick = showHub;
$("card-domino").onclick = () => { audio(); if (DS.highest === 0) startDomino(1); else showDominoMap(); };
