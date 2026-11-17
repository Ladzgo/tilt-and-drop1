// Fold UI (browser playtest). Uses shared helpers from the main script.
"use strict";
const FLEVELS = /*FOLD_LEVELS*/[];
const FS_ = { stars: store.get("fstars", {}), highest: store.get("fhighest", 0), seen: store.get("fseen", {}) };
const fsave = () => { store.set("fstars", FS_.stars); store.set("fhighest", FS_.highest); store.set("fseen", FS_.seen); };
const FCOL = ["#f4f1ea", "#e5484d", "#3e8ef7", "#f5c400", "#30a46c", "#8e4ec6", "#f76b15"];
const F_INTROS = {
  11: ["Two-sided paper", "Squares have a colour on the back too. A fold shows the back of the part you fold."],
  41: ["Both directions", "Creases run across and down. Fold in any order."],
  81: ["Longer folds", "Four or five folds now. Picture each flip before you make it."],
  121: ["Big sheets", "Bigger sheets, more layers."],
  161: ["Review", "Everything together."],
};
const fc_ = $("fold-canvas"), fx = fc_.getContext("2d");
let FL = { w: 0, h: 0, dpr: 1 };
let flevel = null, fgame = null, fnum = 0, fended = false, fcont = 0, fmoves = [], fanim = null, fhint = null, fhintUntil = 0;

function flayout() { const r = fc_.getBoundingClientRect(), dpr = Math.min(3, window.devicePixelRatio || 1); fc_.width = Math.round(r.width * dpr); fc_.height = Math.round(r.height * dpr); FL = { w: r.width, h: r.height, dpr }; }
function fgeom(sheet) {
  const cardH = Math.min(150, FL.h * .26), avail = FL.h - cardH - 24;
  const cs = Math.min(FL.w * .86 / flevel.w, avail * .9 / flevel.h);
  const h = sheet.length, w = sheet[0].length;
  return { cs, cardH, ox: FL.w / 2 - w * cs / 2, oy: cardH + 24 + (avail - h * cs) / 2 };
}
const colOf = (st) => st[0][1] ? flevel.front[st[0][0]] : flevel.back[st[0][0]];
const underOf = (st) => { const [s, f] = st[st.length - 1]; return f ? flevel.back[s] : flevel.front[s]; };

function startFold(n, replay = []) {
  active = "fold";
  fnum = n; flevel = FD.parseLevel(FLEVELS[n - 1]); fgame = new FD.Game(flevel);
  fended = false; fcont = 0; fmoves = []; fanim = null; fhint = null;
  ["hub", "map", "pmap", "papp", "rmap", "rapp", "smap", "sapp", "gmap", "gapp", "dmap", "dapp", "fmap", "wapp", "wmap"].forEach(id => { $(id).hidden = true; });
  $("fapp").hidden = false; closeOverlay(); flayout();
  for (const m of replay) { if (m === "U") { if (fgame.history.length) { fgame.sheet = fgame.history.pop(); fgame.movesLeft++; fgame.movesUsed--; } } else fgame.fold(...m); fmoves.push(m); }
  $("f-title").textContent = `Level ${n}`;
  const tier = flevel.tier, tt = tier === "SH" ? "Super hard" : tier === "H" ? "Hard" : "";
  $("f-sub").innerHTML = `Fold · world ${Math.ceil(n / 50)}` + (tt ? ` · <span class="tier-${tier}">${tt}</span>` : "");
  fupdate();
  fbanner(n === 1 ? "Tap a dashed crease to fold. Match the pattern in the card." : n === 2 ? "The smaller side always folds over the bigger one." : "");
  if (S.showSol) fbanner(`Par ${flevel.par}: follow the pulsing crease`), fshowSol();
  if (F_INTROS[n] && !FS_.seen[n] && !replay.length) { FS_.seen[n] = 1; fsave(); info(F_INTROS[n][0], F_INTROS[n][1], "Got it"); }
}
function fbanner(t) { $("f-banner").textContent = t; }
function fupdate() { $("f-moves-n").textContent = fgame.movesLeft; $("f-moves").classList.toggle("low", fgame.movesLeft <= 1); }
function fshowSol() { const s = FD.solve(flevel, fgame.sheet); if (s.par) { fhint = s.path[0]; fhintUntil = clock + 1e9; } }

function fdo(axis, k, side) {
  if (!fgame || fended || fanim || !$("overlay").hidden || fgame.status !== "playing") return;
  audio();
  const before = fgame.sheet;
  if (!fgame.fold(axis, k, side)) return;
  fmoves.push([axis, k, side]);
  fanim = { before, axis, k, side, t: clock };
  noise(.22, .14, 800, 3000); buzz(6);
  later(.3 / speed(), () => { fanim = null; tone(180, .06, "sine", .2); buzz(10); fupdate();
    if (S.showSol && fgame.status === "playing") fshowSol(); else fhint = null;
    if (fgame.status === "won") fwin(); else if (fgame.status === "lost") later(.3, ffail); });
}
function fstars() {
  if (fcont) return 1;
  const slack = Math.max(0, flevel.moves - flevel.par);
  return fgame.movesUsed <= flevel.par ? 3 : fgame.movesUsed <= flevel.par + Math.ceil(slack / 2) ? 2 : 1;
}
function fwin() {
  fended = true;
  const stars = fstars();
  FS_.stars[fnum] = Math.max(FS_.stars[fnum] || 0, stars); FS_.highest = Math.max(FS_.highest, fnum); fsave();
  sfx.win(); buzz([20, 40, 20]);
  later(.6, () => {
    const hasNext = fnum < FLEVELS.length;
    showCard(`<h2>Perfect fold!</h2><div class="stars" id="fw-stars">${starSvg(false)}${starSvg(false)}${starSvg(false)}</div>
      <p>${fgame.movesUsed} folds · par ${flevel.par}${fcont ? " · with extra moves" : ""}</p>
      ${hasNext ? `<button class="big go" id="fb-next">Next level</button>` : ""}<button class="big" id="fb-map">Levels</button>`);
    const svgs = document.querySelectorAll("#fw-stars svg");
    for (let i = 0; i < stars; i++) later(.2 + .22 * i, () => { svgs[i].outerHTML = starSvg(true); sfx.star(i); });
    if (hasNext) $("fb-next").onclick = () => startFold(fnum + 1);
    $("fb-map").onclick = showFoldMap;
  });
}
function ffail() {
  fended = true; sfx.fail(); buzz([40, 30, 40]);
  showCard(`<h2>Out of folds</h2><p>Undo is free here; in the app, extra moves cost an ad or coins.</p>
    ${fcont < 2 ? `<button class="big gold" id="fb-cont">+3 moves</button>` : ""}<button class="big blue" id="fb-retry">Retry</button><button class="big" id="fb-map">Levels</button>`);
  if (fcont < 2) $("fb-cont").onclick = () => { closeOverlay(); fgame.addMoves(3); fcont++; fended = false; fupdate(); };
  $("fb-retry").onclick = () => startFold(fnum);
  $("fb-map").onclick = showFoldMap;
}
function fundo() {
  if (fended || fanim || !fgame.history.length) return;
  fgame.sheet = fgame.history.pop(); fgame.movesLeft++; fgame.movesUsed--; fgame.update(); fmoves.push("U"); fupdate(); tone(900, .1, "sine", .08);
}
function fhintNow() {
  if (fended || fanim) return;
  const s = FD.solve(flevel, fgame.sheet, Math.max(1, fgame.movesLeft + 2));
  if (s.par === null) return fbanner("No way to reach the pattern from here. Try Undo.");
  fhint = s.path[0]; fhintUntil = clock + 2.5;
  fbanner(`Fold the glowing crease · ${s.par} fold${s.par > 1 ? "s" : ""} to go`);
}

// ---------------------------------------------------------------- drawing
function frr(x, y, w, h, r) { fx.beginPath(); fx.roundRect ? fx.roundRect(x, y, w, h, r) : fx.rect(x, y, w, h); }
function drawSq(x, y, cs, col, depth) {
  for (let d = Math.min(depth, 4) - 1; d > 0; d--) { fx.fillStyle = "rgba(0,0,0,.22)"; fx.fillRect(x + d * 2, y + d * 2, cs - 1, cs - 1); }
  fx.fillStyle = FCOL[col]; fx.fillRect(x, y, cs - 1, cs - 1);
  fx.fillStyle = "rgba(255,255,255,.12)"; fx.fillRect(x, y, cs - 1, (cs - 1) * .18);
}
function drawSheet(sheet, g, skip) {
  sheet.forEach((row, y) => row.forEach((st, x) => { if (!skip || !skip(x, y)) drawSq(g.ox + x * g.cs, g.oy + y * g.cs, g.cs, colOf(st), st.length); }));
}
function foldDraw() {
  if (!flevel || !fgame) return;
  if (fc_.width === 0) flayout();
  const { w: W, h: H, dpr } = FL;
  fx.setTransform(dpr, 0, 0, dpr, 0, 0); fx.clearRect(0, 0, W, H);
  // target card
  const tg = flevel.target, th = tg.length, tw = tg[0].length;
  const gTmp = fgeom(fgame.sheet), cardH = gTmp.cardH, tcs = Math.min((cardH - 30) / th, (W * .5) / tw);
  const cw = tw * tcs + 24, cx = W / 2 - cw / 2;
  fx.fillStyle = "#211f3a"; frr(cx, 6, cw, th * tcs + 30, 14); fx.fill();
  fx.fillStyle = "#a7a2cc"; fx.font = "12px Figtree, sans-serif"; fx.textAlign = "center"; fx.fillText("TARGET", W / 2, 20);
  tg.forEach((r, y) => [...r].forEach((c, x) => { fx.fillStyle = FCOL[+c]; fx.fillRect(cx + 12 + x * tcs, 26 + y * tcs, tcs - 2, tcs - 2); }));
  if (fanim) {
    const { before, axis, k, side, t } = fanim, p = Math.min(1, (clock - t) / (.3 / speed())), th_ = ease.inOutCubic ? ease.inOutCubic(p) : p, a = th_ * Math.PI;
    const g = fgeom(before), bh = before.length, bw = before[0].length;
    const inFlap = (x, y) => axis === "V" ? (side === 0 ? x < k : x >= k) : (side === 0 ? y < k : y >= k);
    drawSheet(before, g, inFlap);
    const crease = axis === "V" ? g.ox + k * g.cs : g.oy + k * g.cs, c = Math.cos(a), under = a > Math.PI / 2;
    before.forEach((row, y) => row.forEach((st, x) => {
      if (!inFlap(x, y)) return;
      const col = under ? underOf(st) : colOf(st);
      if (axis === "V") {
        const x0 = g.ox + x * g.cs, x1 = x0 + g.cs, nx0 = crease + (x0 - crease) * c, nx1 = crease + (x1 - crease) * c;
        fx.fillStyle = shade(FCOL[col], .75 + .25 * Math.abs(c)); fx.fillRect(Math.min(nx0, nx1), g.oy + y * g.cs, Math.abs(nx1 - nx0), g.cs - 1);
      } else {
        const y0 = g.oy + y * g.cs, y1 = y0 + g.cs, ny0 = crease + (y0 - crease) * c, ny1 = crease + (y1 - crease) * c;
        fx.fillStyle = shade(FCOL[col], .75 + .25 * Math.abs(c)); fx.fillRect(g.ox + x * g.cs, Math.min(ny0, ny1), g.cs - 1, Math.abs(ny1 - ny0));
      }
    }));
    return;
  }
  const sh = fgame.sheet, g = fgeom(sh), h = sh.length, w = sh[0].length;
  drawSheet(sh, g);
  // creases with direction markers
  for (const [axis, k, side] of FD.legalFolds(sh)) {
    const hint = fhint && fhint[0] === axis && fhint[1] === k && fhint[2] === side && clock < fhintUntil;
    fx.strokeStyle = hint ? `rgba(255,197,61,${.6 + .4 * Math.sin(clock * 10)})` : "rgba(20,18,40,.75)";
    fx.lineWidth = hint ? 4 : 2; fx.setLineDash([6, 5]); fx.beginPath();
    if (axis === "V") { const x = g.ox + k * g.cs; fx.moveTo(x, g.oy - 8); fx.lineTo(x, g.oy + h * g.cs + 8); }
    else { const y = g.oy + k * g.cs; fx.moveTo(g.ox - 8, y); fx.lineTo(g.ox + w * g.cs + 8, y); }
    fx.stroke(); fx.setLineDash([]);
    fx.fillStyle = hint ? "#ffc53d" : "#a7a2cc";
    const s = 7;
    if (axis === "V") { const x = g.ox + k * g.cs, y = g.oy - 14, dir = side === 0 ? 1 : -1; fx.beginPath(); fx.moveTo(x + dir * s, y); fx.lineTo(x - dir * s * .4, y - s * .7); fx.lineTo(x - dir * s * .4, y + s * .7); fx.fill(); }
    else { const y = g.oy + k * g.cs, x = g.ox - 14, dir = side === 0 ? 1 : -1; fx.beginPath(); fx.moveTo(x, y + dir * s); fx.lineTo(x - s * .7, y - dir * s * .4); fx.lineTo(x + s * .7, y - dir * s * .4); fx.fill(); }
  }
}

// ---------------------------------------------------------------- map
let fworld = 1;
function showFoldMap() {
  active = "fold"; closeOverlay();
  $("hub").hidden = true; $("fapp").hidden = true; $("fmap").hidden = false;
  const total = Object.values(FS_.stars).reduce((a, b) => a + b, 0);
  $("f-total").textContent = `${total} / ${FLEVELS.length * 3} stars`;
  const next = Math.min(FS_.highest + 1, FLEVELS.length);
  $("f-play").textContent = `Play level ${next}`; $("f-play").onclick = () => startFold(next);
  const tabs = $("f-tabs"); tabs.innerHTML = "";
  for (let w = 1; w <= Math.ceil(FLEVELS.length / 50); w++) {
    const b = document.createElement("button"); b.className = "tab"; b.setAttribute("role", "tab"); b.textContent = `World ${w}`;
    b.setAttribute("aria-selected", String(w === fworld)); b.onclick = () => { fworld = w; showFoldMap(); }; tabs.appendChild(b);
  }
  const grid = $("f-grid"); grid.innerHTML = "";
  for (let n = (fworld - 1) * 50 + 1; n <= Math.min(fworld * 50, FLEVELS.length); n++) {
    const st = FS_.stars[n] || 0, open = S.unlock || n <= FS_.highest + 1, tier = FLEVELS[n - 1].tier;
    const b = document.createElement("button");
    b.className = "lvl fold" + (n === next && !st ? " next" : st ? " done" : "") + (open ? "" : " locked") + (tier === "H" ? " hard" : tier === "SH" ? " super" : "");
    b.innerHTML = open ? `${n}<span class="ls">${st ? "★".repeat(st) : ""}</span>` : `${n}`;
    b.disabled = !open; b.onclick = () => startFold(n);
    b.setAttribute("aria-label", `Fold level ${n}${st ? `, ${st} stars` : ""}${open ? "" : ", locked"}`);
    grid.appendChild(b);
  }
}

fc_.addEventListener("pointerdown", e => {
  if (!flevel || fanim) return;
  const r = fc_.getBoundingClientRect(), px0 = e.clientX - r.left, py0 = e.clientY - r.top;
  const sh = fgame.sheet, g = fgeom(sh), h = sh.length, w = sh[0].length;
  let best = null, bd = g.cs * .45;
  for (let k = 1; k < w; k++) { const d = Math.abs(px0 - (g.ox + k * g.cs)); if (d < bd && py0 > g.oy - 20 && py0 < g.oy + h * g.cs + 20) { bd = d; best = ["V", k, px0]; } }
  for (let k = 1; k < h; k++) { const d = Math.abs(py0 - (g.oy + k * g.cs)); if (d < bd && px0 > g.ox - 20 && px0 < g.ox + w * g.cs + 20) { bd = d; best = ["H", k, py0]; } }
  if (!best) return;
  const [axis, k, pos] = best, n = axis === "V" ? w : h, line = axis === "V" ? g.ox + k * g.cs : g.oy + k * g.cs;
  let side = k < n - k ? 0 : k > n - k ? 1 : (pos < line ? 0 : 1);
  fdo(axis, k, side);
});
window.addEventListener("resize", () => { if (!$("fapp").hidden) flayout(); });
$("f-menu").onclick = () => { audio(); showFoldMap(); };
$("f-undo").onclick = fundo; $("f-hint").onclick = fhintNow; $("f-restart").onclick = () => startFold(fnum);
$("f-back").onclick = showHub;
$("card-fold").onclick = () => { audio(); if (FS_.highest === 0) startFold(1); else showFoldMap(); };
