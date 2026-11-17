// Slide Out UI (browser playtest). Uses shared helpers from the main script.
"use strict";
const SLEVELS = /*SLIDE_LEVELS*/[];
const SS = { stars: store.get("sstars", {}), highest: store.get("shighest", 0), seen: store.get("sseen", {}) };
const ssave = () => { store.set("sstars", SS.stars); store.set("shighest", SS.highest); store.set("sseen", SS.seen); };
const SCOL = [null, "#e5484d", "#3e8ef7", "#f5c400", "#30a46c", "#8e4ec6", "#f76b15"];
const S_INTROS = {
  11: ["More colours", "Three or four colours now. Keep at most two of a colour waiting in the tray."],
  41: ["Walls", "Walls never move. Some blocks can only leave in a certain order."],
  81: ["Crowds", "Bigger boards, more blocks. Look for the block that frees the most lanes."],
  121: ["Tight tray", "Only four slots. Every tap counts."],
  161: ["Review", "Everything together."],
};
const sc = $("slide-canvas"), sx = sc.getContext("2d");
let SLL = { w: 0, h: 0, dpr: 1 };
let slevel = null, sgame = null, snum = 0, shist = [], sended = false, sassist = 0, staps = [], sbusy = false, squeue = null;
let sv = null, fly = [], bumpFx = null, shintCell = -1, shintUntil = 0, trayPop = 0;

function slayout() { const r = sc.getBoundingClientRect(), dpr = Math.min(3, window.devicePixelRatio || 1); sc.width = Math.round(r.width * dpr); sc.height = Math.round(r.height * dpr); SLL = { w: r.width, h: r.height, dpr }; }
function sgeom() {
  const trayH = Math.min(70, SLL.h * .16), avail = SLL.h - trayH - 16;
  const cs = Math.min(SLL.w / (slevel.w + .4), avail / (slevel.h + .4));
  return { cs, ox: SLL.w / 2 - slevel.w * cs / 2, oy: (avail - slevel.h * cs) / 2, trayY: SLL.h - trayH, trayH };
}
function slotPos(i) {
  const { trayY, trayH } = sgeom(), cap = sgame.capacity, sw = Math.min(trayH * .9, (SLL.w - 32) / cap);
  return { x: SLL.w / 2 + (i - (cap - 1) / 2) * sw, y: trayY + trayH / 2, s: sw * .8 };
}

function startSlide(n, replay = []) {
  active = "slide";
  snum = n; slevel = SL.parseLevel(SLEVELS[n - 1]); sgame = new SL.Game(slevel);
  shist = []; sended = false; sassist = 0; staps = []; sbusy = false; squeue = null; fly = []; bumpFx = null; shintCell = -1;
  ["hub", "map", "pmap", "papp", "rmap", "rapp", "smap", "gapp", "gmap", "dapp", "dmap"].forEach(id => { $(id).hidden = true; });
  $("sapp").hidden = false; closeOverlay(); slayout();
  for (const c of replay) { shist.push(sgame.clone()); sgame.tapCell(c); staps.push(c); }
  sv = { removed: sgame.removed, tray: sgame.tray.slice() };
  $("s-title").textContent = `Level ${n}`;
  const tier = slevel.tier, tt = tier === "SH" ? "Super hard" : tier === "H" ? "Hard" : "";
  $("s-sub").innerHTML = `Slide Out · world ${Math.ceil(n / 50)}` + (tt ? ` · <span class="tier-${tier}">${tt}</span>` : "");
  supdate();
  sbanner(n === 1 ? "Tap a block to send it off along its arrow. Three of a colour in the tray pop." :
    n === 2 ? "A block can only leave if nothing is in its way." : "");
  if (S.showSol) sbanner("Solution: tap the pulsing block each time."), sshowSol();
  if (S_INTROS[n] && !SS.seen[n] && !replay.length) { SS.seen[n] = 1; ssave(); info(S_INTROS[n][0], S_INTROS[n][1], "Got it"); }
}
function sbanner(t) { $("s-banner").textContent = t; }
function supdate() {
  const left = slevel.blocks.length - sgame.taps;
  $("s-left-n").textContent = left;
  $("s-tray").textContent = `Tray ${sgame.tray.length} / ${sgame.capacity}`;
}
function sshowSol() { const s = SL.solve(slevel, sgame.removed, sgame.tray, sgame.capacity); if (s.solvable && s.order.length) { shintCell = slevel.blocks[s.order[0]]; shintUntil = clock + 1e9; } }

function stap(cell) {
  if (!sgame || sended || !$("overlay").hidden || sgame.status !== "playing") return;
  audio();
  if (sbusy) { squeue = cell; return; }
  const before = sgame.clone(), res = sgame.tapCell(cell);
  if (res === null) return;
  if (res === "bump") {
    const k = slevel.index.get(cell), bl = SL.blocker(slevel, sgame.removed, k);
    bumpFx = { cell, blocker: bl, t: clock, dir: slevel.cells[cell][1] };
    tone(180, .07, "square", .05); buzz(8);
    return;
  }
  shist.push(before); staps.push(cell);
  if (shintCell === cell) shintCell = -1;
  sbusy = true; supdate();
  const { cs, ox, oy } = sgeom(), [colour, d] = slevel.cells[cell];
  const x0 = ox + (cell % slevel.w + .5) * cs, y0 = oy + (Math.floor(cell / slevel.w) + .5) * cs;
  const edgeX = d === 0 ? SLL.w + cs : d === 2 ? -cs : x0, edgeY = d === 1 ? sgeom().trayY - cs * .2 : d === 3 ? -cs : y0;
  const slot = slotPos(sv.tray.length), f = { x: x0, y: y0, s: cs * .84, colour, dir: d, a: 1 };
  fly.push(f);
  sv.removed = sgame.removed;
  noise(.16, .14, 600, 2400);
  const out = .16 / speed(), into = .22 / speed();
  tween(out, p => { f.x = x0 + (edgeX - x0) * p; f.y = y0 + (edgeY - y0) * p; }, ease.inQuad, 0);
  tween(into, p => { f.x = edgeX + (slot.x - edgeX) * p; f.y = edgeY + (slot.y - edgeY) * p - Math.sin(p * Math.PI) * cs * 1.2; f.s = cs * .84 + (slot.s - cs * .84) * p; }, ease.outCubic, out, () => {
    fly.splice(fly.indexOf(f), 1);
    sv.tray.push(colour);
    tone(900 + 60 * colour, .06, "sine", .1); buzz(6);
    if (res === "match") later(.08, () => {
      for (let i = 0; i < sv.tray.length; i++) if (sv.tray[i] === colour) { const p = slotPos(i);
        for (let j = 0; j < 8; j++) { const a = Math.random() * Math.PI * 2, v = 60 + Math.random() * 160;
          particles.push({ x: p.x, y: p.y, vx: Math.cos(a) * v, vy: Math.sin(a) * v - 120, t: 0, life: .5, size: 5, color: SCOL[colour], rot: 0, spin: 0 }); } }
      [659.25, 783.99, 1046.5].forEach((fr, j) => tone(fr, .3, "triangle", .14, j * .05));
      trayPop = clock; sv.tray = sgame.tray.slice(); buzz(15);
    });
    later(res === "match" ? .12 : .01, () => {
      sbusy = false; supdate();
      if (sgame.status === "won") return swin();
      if (sgame.status === "lost") return later(.25, sfail);
      if (squeue !== null) { const q = squeue; squeue = null; stap(q); }
    });
  });
}
function sstars() { return sassist === 0 ? 3 : sassist === 1 ? 2 : 1; }
function swin() {
  sended = true;
  const stars = sstars();
  SS.stars[snum] = Math.max(SS.stars[snum] || 0, stars); SS.highest = Math.max(SS.highest, snum); ssave();
  sfx.win(); buzz([20, 40, 20]);
  later(.5, () => {
    const hasNext = snum < SLEVELS.length;
    showCard(`<h2>Board cleared!</h2><div class="stars" id="sw-stars">${starSvg(false)}${starSvg(false)}${starSvg(false)}</div>
      <p>${sassist ? `${sassist} assist${sassist > 1 ? "s" : ""} (undo, hint or extra slots)` : "No assists: perfect clear"}</p>
      ${hasNext ? `<button class="big go" id="sb-next">Next level</button>` : ""}<button class="big" id="sb-map">Levels</button>`);
    const svgs = document.querySelectorAll("#sw-stars svg");
    for (let i = 0; i < stars; i++) later(.2 + .22 * i, () => { svgs[i].outerHTML = starSvg(true); sfx.star(i); });
    if (hasNext) $("sb-next").onclick = () => startSlide(snum + 1);
    $("sb-map").onclick = showSlideMap;
  });
}
function sfail() {
  sended = true; sfx.fail(); buzz([40, 30, 40]);
  showCard(`<h2>Tray full</h2><p>${slevel.blocks.length - sgame.taps} blocks were still on the board. In the app, extra slots cost an ad or coins; here they are free.</p>
    <button class="big gold" id="sb-cont">+2 tray slots</button><button class="big blue" id="sb-retry">Retry</button><button class="big" id="sb-map">Levels</button>`);
  $("sb-cont").onclick = () => { closeOverlay(); sgame.addSlots(2); sassist++; sended = false; supdate(); };
  $("sb-retry").onclick = () => startSlide(snum);
  $("sb-map").onclick = showSlideMap;
}
function sundo() {
  if (sended || sbusy || !shist.length) return;
  sgame = shist.pop(); staps.pop(); sassist++;
  sv = { removed: sgame.removed, tray: sgame.tray.slice() }; supdate(); tone(900, .1, "sine", .08);
}
function shint() {
  if (sended || sbusy) return;
  const s = SL.solve(slevel, sgame.removed, sgame.tray, sgame.capacity);
  if (!s.solvable) return sbanner("No way to clear the board from here. Try Undo or Restart.");
  sassist++; shintCell = slevel.blocks[s.order[0]]; shintUntil = clock + 2.5;
  sbanner("Tap the glowing block.");
}

// ---------------------------------------------------------------- drawing
function srr(x, y, w, h, r) { sx.beginPath(); sx.roundRect ? sx.roundRect(x, y, w, h, r) : sx.rect(x, y, w, h); }
function drawBlock(cx, cy, s, colour, d, alpha = 1) {
  sx.globalAlpha = alpha;
  sx.fillStyle = shade(SCOL[colour], .7); srr(cx - s / 2, cy - s / 2 + s * .06, s, s, s * .2); sx.fill();
  sx.fillStyle = SCOL[colour]; srr(cx - s / 2, cy - s / 2, s, s * .94, s * .2); sx.fill();
  if (d !== null && d !== undefined) {
    sx.save(); sx.translate(cx, cy - s * .03); sx.rotate(d * Math.PI / 2);
    sx.fillStyle = "rgba(255,255,255,.95)"; sx.beginPath(); sx.moveTo(s * .3, 0); sx.lineTo(-s * .12, -s * .24); sx.lineTo(-s * .02, 0); sx.lineTo(-s * .12, s * .24); sx.closePath(); sx.fill();
    sx.restore();
  }
  sx.globalAlpha = 1;
}
function slideDraw() {
  if (!slevel || !sv) return;
  if (sc.width === 0) slayout();
  const { w: W, h: H, dpr } = SLL, { cs, ox, oy, trayY, trayH } = sgeom();
  sx.setTransform(dpr, 0, 0, dpr, 0, 0); sx.clearRect(0, 0, W, H);
  sx.fillStyle = "#1a1830"; srr(ox - cs * .15, oy - cs * .15, slevel.w * cs + cs * .3, slevel.h * cs + cs * .3, cs * .25); sx.fill();
  for (let i = 0; i < slevel.w * slevel.h; i++) {
    const x = ox + (i % slevel.w) * cs, y = oy + Math.floor(i / slevel.w) * cs, c = slevel.cells[i];
    if (c === "#") { sx.fillStyle = "#3d3866"; srr(x + cs * .04, y + cs * .04, cs * .92, cs * .92, cs * .12); sx.fill(); continue; }
    sx.fillStyle = "#25223f"; srr(x + cs * .06, y + cs * .06, cs * .88, cs * .88, cs * .14); sx.fill();
  }
  for (const cell of slevel.blocks) {
    const k = slevel.index.get(cell);
    if ((sv.removed >>> k) & 1) continue;
    let cx = ox + (cell % slevel.w + .5) * cs, cy = oy + (Math.floor(cell / slevel.w) + .5) * cs;
    const [colour, d] = slevel.cells[cell];
    if (bumpFx && bumpFx.cell === cell) { const p = (clock - bumpFx.t) / .25; if (p < 1) { const k2 = Math.sin(p * Math.PI) * cs * .12; cx += SL.DX[d] * k2; cy += SL.DY[d] * k2; } }
    if (cell === shintCell && clock < shintUntil) { sx.fillStyle = `rgba(255,197,61,${.35 + .25 * Math.sin(clock * 10)})`; srr(cx - cs * .5, cy - cs * .5, cs, cs, cs * .24); sx.fill(); }
    drawBlock(cx, cy, cs * .84, colour, d);
    if (bumpFx && bumpFx.blocker === cell && clock - bumpFx.t < .4) { sx.strokeStyle = "#fff"; sx.lineWidth = 3; srr(cx - cs * .44, cy - cs * .44, cs * .88, cs * .88, cs * .2); sx.stroke(); }
  }
  if (bumpFx && bumpFx.blocker !== null && clock - bumpFx.t < .4 && slevel.cells[bumpFx.blocker] === "#") {
    const b = bumpFx.blocker, x = ox + (b % slevel.w) * cs, y = oy + Math.floor(b / slevel.w) * cs;
    sx.strokeStyle = "#fff"; sx.lineWidth = 3; srr(x + cs * .06, y + cs * .06, cs * .88, cs * .88, cs * .14); sx.stroke();
  }
  // tray
  const cap = sgame.capacity, danger = sv.tray.length >= cap - 1;
  const tw = Math.min(W - 16, slotPos(0).s / .8 * cap + 16);
  sx.fillStyle = danger ? "rgba(247,107,21,.22)" : "#211f3a"; srr(W / 2 - tw / 2, trayY + 4, tw, trayH - 8, 16); sx.fill();
  if (danger) { sx.strokeStyle = "rgba(247,107,21,.8)"; sx.lineWidth = 2; srr(W / 2 - tw / 2, trayY + 4, tw, trayH - 8, 16); sx.stroke(); }
  for (let i = 0; i < cap; i++) { const p = slotPos(i); sx.fillStyle = "rgba(255,255,255,.06)"; srr(p.x - p.s / 2, p.y - p.s / 2, p.s, p.s, p.s * .2); sx.fill(); }
  sv.tray.forEach((c, i) => { const p = slotPos(i); drawBlock(p.x, p.y, p.s, c, null); });
  for (const f of fly) drawBlock(f.x, f.y, f.s, f.colour, f.dir, f.a);
  for (const p of particles) { sx.globalAlpha = Math.max(0, 1 - p.t * p.t); sx.fillStyle = p.color; sx.beginPath(); sx.arc(p.x, p.y, p.size * (1 - p.t), 0, Math.PI * 2); sx.fill(); }
  sx.globalAlpha = 1;
}

// ---------------------------------------------------------------- map
let sworld = 1;
function showSlideMap() {
  active = "slide"; closeOverlay();
  $("hub").hidden = true; $("sapp").hidden = true; $("smap").hidden = false;
  const total = Object.values(SS.stars).reduce((a, b) => a + b, 0);
  $("s-total").textContent = `${total} / ${SLEVELS.length * 3} stars`;
  const next = Math.min(SS.highest + 1, SLEVELS.length);
  $("s-play").textContent = `Play level ${next}`; $("s-play").onclick = () => startSlide(next);
  const tabs = $("s-tabs"); tabs.innerHTML = "";
  for (let w = 1; w <= Math.ceil(SLEVELS.length / 50); w++) {
    const b = document.createElement("button"); b.className = "tab"; b.setAttribute("role", "tab"); b.textContent = `World ${w}`;
    b.setAttribute("aria-selected", String(w === sworld)); b.onclick = () => { sworld = w; showSlideMap(); }; tabs.appendChild(b);
  }
  const grid = $("s-grid"); grid.innerHTML = "";
  for (let n = (sworld - 1) * 50 + 1; n <= Math.min(sworld * 50, SLEVELS.length); n++) {
    const st = SS.stars[n] || 0, open = S.unlock || n <= SS.highest + 1, tier = SLEVELS[n - 1].tier;
    const b = document.createElement("button");
    b.className = "lvl slide" + (n === next && !st ? " next" : st ? " done" : "") + (open ? "" : " locked") + (tier === "H" ? " hard" : tier === "SH" ? " super" : "");
    b.innerHTML = open ? `${n}<span class="ls">${st ? "★".repeat(st) : ""}</span>` : `${n}`;
    b.disabled = !open; b.onclick = () => startSlide(n);
    b.setAttribute("aria-label", `Slide level ${n}${st ? `, ${st} stars` : ""}${open ? "" : ", locked"}`);
    grid.appendChild(b);
  }
}

sc.addEventListener("pointerdown", e => {
  if (!slevel) return;
  const r = sc.getBoundingClientRect(), { cs, ox, oy } = sgeom();
  const x = Math.floor((e.clientX - r.left - ox) / cs), y = Math.floor((e.clientY - r.top - oy) / cs);
  if (x >= 0 && y >= 0 && x < slevel.w && y < slevel.h) stap(y * slevel.w + x);
});
window.addEventListener("resize", () => { if (!$("sapp").hidden) slayout(); });
$("s-menu").onclick = () => { audio(); showSlideMap(); };
$("s-undo").onclick = sundo; $("s-hint").onclick = shint; $("s-restart").onclick = () => startSlide(snum);
$("s-back").onclick = showHub;
$("card-slide").onclick = () => { audio(); if (SS.highest === 0) startSlide(1); else showSlideMap(); };
