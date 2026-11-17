// Flow Fill UI (browser playtest). Uses shared helpers from the main script.
"use strict";
const WLEVELS = /*FLOW_LEVELS*/[];
const WS_ = { stars: store.get("wstars", {}), highest: store.get("whighest", 0), seen: store.get("wseen", {}) };
const wsave = () => { store.set("wstars", WS_.stars); store.set("whighest", WS_.highest); store.set("wseen", WS_.seen); };
const WCOL = ["#888", "#e5484d", "#3e8ef7", "#f5c400", "#30a46c", "#b07de8", "#f76b15"];
const WMUD = "#7a5a3a";
const W_INTROS = {
  11: ["Two colours", "Each colour fills only its own cups. If two colours meet, the water turns muddy."],
  41: ["Tees", "A tee splits the water three ways: one source can fill two cups."],
  81: ["Bolted pipes", "Dark pipes are bolted and can't turn. Crosses carry water straight through in both directions."],
  121: ["Three colours", "Three colours on crowded boards. Keep them apart."],
  161: ["Review", "Everything together."],
};
const wc_ = $("flow-canvas"), wx = wc_.getContext("2d");
let WL = { w: 0, h: 0, dpr: 1 };
let wlevel = null, wgame = null, wnum = 0, wended = false, wcont = 0, wtaps = [], wspin = new Map(), wappear = [], wprev = [], whint = -1, whintUntil = 0, wcupT = [];

function wlayout() { const r = wc_.getBoundingClientRect(), dpr = Math.min(3, window.devicePixelRatio || 1); wc_.width = Math.round(r.width * dpr); wc_.height = Math.round(r.height * dpr); WL = { w: r.width, h: r.height, dpr }; }
function wgeom() {
  const cs = Math.min(WL.w * .94 / wlevel.w, WL.h * .94 / wlevel.h);
  return { cs, ox: WL.w / 2 - wlevel.w * cs / 2, oy: WL.h / 2 - wlevel.h * cs / 2 };
}
const WRATE = () => 12 * speed();

// water reveal: unchanged cells stay, changed cells appear outward from the closest changed one
function wflood(instant) {
  const st = wgame.state, n = wlevel.w * wlevel.h;
  let dmin = 1e9;
  for (let i = 0; i < n; i++) if (st.colour[i] && st.colour[i] !== wprev[i]) dmin = Math.min(dmin, st.dist[i]);
  let last = clock;
  for (let i = 0; i < n; i++) {
    if (!st.colour[i]) wappear[i] = Infinity;
    else if (instant) wappear[i] = clock - 1;
    else if (st.colour[i] !== wprev[i] || !(wappear[i] < Infinity)) { wappear[i] = clock + .12 + Math.max(0, st.dist[i] - dmin) / WRATE(); last = Math.max(last, wappear[i]); }
  }
  wlevel.cups.forEach(([x, y], k) => { const a = wappear[y * wlevel.w + x]; wcupT[k] = st.cups[k] ? a : Infinity; });
  wprev = st.colour.slice();
  return last - clock;
}

function startFlow(n, replay = []) {
  active = "flow";
  wnum = n; wlevel = FW.parseLevel(WLEVELS[n - 1]); wgame = new FW.Game(wlevel);
  wended = false; wcont = 0; wtaps = []; wspin = new Map(); whint = -1; wprev = []; wappear = []; wcupT = [];
  ["hub", "map", "pmap", "papp", "rmap", "rapp", "smap", "sapp", "gmap", "gapp", "dmap", "dapp", "fmap", "fapp", "wmap"].forEach(id => { $(id).hidden = true; });
  $("wapp").hidden = false; closeOverlay(); wlayout();
  for (const c of replay) { if (wgame.status === "playing" && wgame.tap(c)) wtaps.push(c); }
  wflood(replay.length > 0);
  $("w-title").textContent = `Level ${n}`;
  const tier = wlevel.tier, tt = tier === "SH" ? "Super hard" : tier === "H" ? "Hard" : "";
  $("w-sub").innerHTML = `Flow Fill · world ${Math.ceil(n / 50)}` + (tt ? ` · <span class="tier-${tier}">${tt}</span>` : "");
  wupdate();
  wbanner(n === 1 ? "Tap pipes to turn them. Lead the water into its cup without spilling." : n === 2 ? "Any open pipe end the water reaches leaks. Close them all." : "");
  if (S.showSol) wbanner(`Par ${wlevel.par}: tap the pulsing pipe`), wshowSol();
  if (W_INTROS[n] && !WS_.seen[n] && !replay.length) { WS_.seen[n] = 1; wsave(); info(W_INTROS[n][0], W_INTROS[n][1], "Got it"); }
}
function wbanner(t) { $("w-banner").textContent = t; }
function wupdate() { $("w-moves-n").textContent = wgame.movesLeft; $("w-moves").classList.toggle("low", wgame.movesLeft <= 1); }
function wnextTap(cap) {
  const s = FW.solve(wlevel, new Map(wgame.rot), cap);
  if (s.par === null) return null;
  for (const [i, r] of [...s.rot].sort((a, b) => a[0] - b[0])) if (((r - wgame.rot.get(i)) & 3) !== 0) return { cell: i, par: s.par };
  return { cell: -1, par: 0 };
}
function wshowSol() { const h = wnextTap(300000); whint = h && h.cell >= 0 ? h.cell : -1; whintUntil = clock + 1e9; }

function wtap(cell) {
  if (!wgame || wended || !$("overlay").hidden || wgame.status !== "playing") return;
  audio();
  if (!wgame.tap(cell)) { tone(160, .06, "square", .05); return; }
  wtaps.push(cell);
  wspin.set(cell, clock);
  tone(2100, .03, "square", .05); tone(900, .05, "sine", .08, .02); buzz(6);
  const dur = wflood(false), st = wgame.state;
  if (st.leaks.length) later(Math.min(dur, .4), () => noise(.25, .06, 4000, 7000));
  if (st.contaminated.length) later(Math.min(dur, .4), () => tone(110, .25, "sine", .18, 0, -50));
  st.cups.forEach((ok, k) => { if (ok && wcupT[k] < Infinity) later(Math.max(0, wcupT[k] - clock), () => tone(500 + 120 * k, .35, "triangle", .12, 0, 400)); });
  wupdate();
  if (S.showSol && wgame.status === "playing") wshowSol(); else whint = -1;
  if (wgame.status === "won") { wended = true; later(dur + .35, wwin); }
  else if (wgame.status === "lost") { wended = true; later(dur + .4, wfail); }
}
function wstars() {
  if (wcont) return 1;
  const slack = Math.max(0, wlevel.moves - wlevel.par);
  return wgame.movesUsed <= wlevel.par ? 3 : wgame.movesUsed <= wlevel.par + Math.ceil(slack / 2) ? 2 : 1;
}
function wwin() {
  const stars = wstars();
  WS_.stars[wnum] = Math.max(WS_.stars[wnum] || 0, stars); WS_.highest = Math.max(WS_.highest, wnum); wsave();
  sfx.win(); buzz([20, 40, 20]);
  for (let k = 0; k < 6; k++) tone(700 + Math.random() * 900, .08, "sine", .05, .1 + k * .07, 300);
  later(.6, () => {
    const hasNext = wnum < WLEVELS.length;
    showCard(`<h2>All cups full!</h2><div class="stars" id="ww-stars">${starSvg(false)}${starSvg(false)}${starSvg(false)}</div>
      <p>${wgame.movesUsed} turns · par ${wlevel.par}${wcont ? " · with extra moves" : ""}</p>
      ${hasNext ? `<button class="big go" id="wb-next">Next level</button>` : ""}<button class="big" id="wb-map">Levels</button>`);
    const svgs = document.querySelectorAll("#ww-stars svg");
    for (let i = 0; i < stars; i++) later(.2 + .22 * i, () => { svgs[i].outerHTML = starSvg(true); sfx.star(i); });
    if (hasNext) $("wb-next").onclick = () => startFlow(wnum + 1);
    $("wb-map").onclick = showFlowMap;
  });
}
function wfail() {
  sfx.fail(); buzz([40, 30, 40]);
  showCard(`<h2>Out of turns</h2><p>In the app, extra moves cost an ad or coins.</p>
    ${wcont < 2 ? `<button class="big gold" id="wb-cont">+3 moves</button>` : ""}<button class="big blue" id="wb-retry">Retry</button><button class="big" id="wb-map">Levels</button>`);
  if (wcont < 2) $("wb-cont").onclick = () => { closeOverlay(); wgame.addMoves(3); wcont++; wended = false; wupdate(); };
  $("wb-retry").onclick = () => startFlow(wnum);
  $("wb-map").onclick = showFlowMap;
}
function whintNow() {
  if (wended) return;
  const h = wnextTap(200000);
  if (!h) return wbanner("No solution found from here. Try Restart.");
  if (h.cell < 0) return;
  whint = h.cell; whintUntil = clock + 2.5;
  wbanner(`Turn the glowing pipe · ${h.par} turn${h.par > 1 ? "s" : ""} to go`);
}

// ---------------------------------------------------------------- drawing
function wrr(x, y, w, h, r) { wx.beginPath(); wx.roundRect ? wx.roundRect(x, y, w, h, r) : wx.rect(x, y, w, h); }
const wcolOf = cm => !cm ? null : (cm & (cm - 1)) ? WMUD : WCOL[Math.log2(cm & -cm) | 0];
// openings drawn from the centre, mask m in local frame rotated by ang
function wpipe(cx, cy, cs, m, ang, outer, fill, wobble) {
  wx.save(); wx.translate(cx, cy); wx.rotate(ang);
  const L = cs / 2, tw = cs * .36, iw = cs * .2;
  wx.lineCap = "butt";
  const segs = (w, col) => {
    wx.strokeStyle = col; wx.lineWidth = w; wx.beginPath();
    for (let d = 0; d < 4; d++) if (m >> d & 1) { wx.moveTo(0, 0); wx.lineTo(FW.DX[d] * L, FW.DY[d] * L); }
    wx.stroke();
    wx.fillStyle = col; wx.beginPath(); wx.arc(0, 0, w / 2, 0, Math.PI * 2); wx.fill();
  };
  segs(tw, outer);
  segs(iw, fill || "#15132b");
  if (fill && wobble) {   // surface highlight
    wx.strokeStyle = "rgba(255,255,255,.35)"; wx.lineWidth = iw * .25; wx.beginPath();
    for (let d = 0; d < 4; d++) if (m >> d & 1) {
      const k = .3 + .15 * Math.sin(clock * 6 + d), px = -FW.DY[d] * iw * .22, py = FW.DX[d] * iw * .22;
      wx.moveTo(FW.DX[d] * L * k + px, FW.DY[d] * L * k + py); wx.lineTo(FW.DX[d] * L * (k + .35) + px, FW.DY[d] * L * (k + .35) + py);
    }
    wx.stroke();
  }
  // flanges at the openings
  wx.fillStyle = shade(outer, .8);
  for (let d = 0; d < 4; d++) if (m >> d & 1) {
    const ex = FW.DX[d] * (L - cs * .05), ey = FW.DY[d] * (L - cs * .05);
    if (FW.DX[d]) wx.fillRect(ex - cs * .05, ey - tw * .62, cs * .1, tw * 1.24); else wx.fillRect(ex - tw * .62, ey - cs * .05, tw * 1.24, cs * .1);
  }
  wx.restore();
}
function flowDraw() {
  if (!wlevel || !wgame) return;
  if (wc_.width === 0) wlayout();
  const { w: W, h: H, dpr } = WL;
  wx.setTransform(dpr, 0, 0, dpr, 0, 0); wx.clearRect(0, 0, W, H);
  const g = wgeom(), cs = g.cs, st = wgame.state, w = wlevel.w;
  wx.fillStyle = "#1d1b36"; wrr(g.ox - 6, g.oy - 6, w * cs + 12, wlevel.h * cs + 12, 14); wx.fill();
  const ends = new Map();
  wlevel.sources.forEach(s => ends.set(s[1] * w + s[0], ["s", s]));
  wlevel.cups.forEach((c, k) => ends.set(c[1] * w + c[0], ["c", c, k]));
  for (let i = 0; i < w * wlevel.h; i++) {
    const x = i % w, y = (i / w) | 0, cx = g.ox + (x + .5) * cs, cy = g.oy + (y + .5) * cs, p = wlevel.pipes[i];
    wx.fillStyle = p && !p[2] ? "#25223f" : "#2c2950"; wrr(g.ox + x * cs + 1.5, g.oy + y * cs + 1.5, cs - 3, cs - 3, cs * .14); wx.fill();
    const wet = clock >= wappear[i] ? wcolOf(st.colour[i]) : null;
    if (p) {
      const r = p[2] ? wgame.rot.get(i) : p[1], t0 = wspin.get(i);
      let ang = r * Math.PI / 2;
      if (t0 !== undefined) { const q = Math.min(1, (clock - t0) / (.22 / speed())); ang -= (1 - ease.outBack(q)) * Math.PI / 2; if (q >= 1) wspin.delete(i); }
      const hint = i === whint && clock < whintUntil;
      const outer = hint ? `rgb(255,${180 + 40 * Math.sin(clock * 10) | 0},70)` : p[2] ? "#8f8ab8" : "#55507a";
      wpipe(cx, cy, cs, FW.BASE[p[0]], ang, outer, wet, !REDUCED);
      if (!p[2]) { wx.fillStyle = "#3a3658"; for (const [a, b] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) { wx.beginPath(); wx.arc(cx + a * cs * .36, cy + b * cs * .36, cs * .04, 0, Math.PI * 2); wx.fill(); } }
    } else if (ends.has(i)) {
      const [kind, e, k] = ends.get(i), col = WCOL[e[2]];
      wpipe(cx, cy, cs, 1 << e[3], 0, "#8f8ab8", wet, false);
      if (kind === "s") {
        wx.fillStyle = shade(col, .7); wx.beginPath(); wx.arc(cx, cy, cs * .34, 0, Math.PI * 2); wx.fill();
        wx.fillStyle = col; wx.beginPath(); wx.arc(cx, cy, cs * .27, 0, Math.PI * 2); wx.fill();
        wx.fillStyle = "rgba(255,255,255,.35)"; wx.beginPath(); wx.arc(cx - cs * .08, cy - cs * .09, cs * .07, 0, Math.PI * 2); wx.fill();
      } else {
        const bw = cs * .56, bh = cs * .5, bx = cx - bw / 2, by = cy - bh / 2;
        wx.fillStyle = "#15132b"; wrr(bx, by, bw, bh, cs * .1); wx.fill();
        const tt = wcupT[k], f = tt < Infinity ? Math.max(0, Math.min(1, (clock - tt) / .5)) : 0;
        const bad = clock >= wappear[i] && st.colour[i] && !st.cups[k];
        if (f > 0 || bad) { wx.fillStyle = bad ? WMUD : col; const fh = bad ? bh * .35 : bh * f; wrr(bx + 2, by + bh - fh, bw - 4, fh - 2, cs * .06); wx.fill(); }
        wx.strokeStyle = col; wx.lineWidth = cs * .07; wrr(bx, by, bw, bh, cs * .1); wx.stroke();
        if (f >= 1) { wx.fillStyle = "#fff"; wx.font = `700 ${cs * .26}px Figtree, sans-serif`; wx.textAlign = "center"; wx.textBaseline = "middle"; wx.fillText("✓", cx, cy + 1); }
      }
    }
  }
  // leaks: droplets spraying from open ends the water has reached
  for (const [i, d] of st.leaks) {
    if (clock < wappear[i]) continue;
    const x = i % w, y = (i / w) | 0, ex = g.ox + (x + .5 + FW.DX[d] * .5) * cs, ey = g.oy + (y + .5 + FW.DY[d] * .5) * cs;
    wx.fillStyle = wcolOf(st.colour[i]) || "#fff";
    for (let k = 0; k < 7; k++) {
      const ph = (clock * 1.8 + k / 7) % 1, spread = Math.sin(k * 2.39) * .5;
      const px = ex + (FW.DX[d] * ph + -FW.DY[d] * spread * ph) * cs * .45, py = ey + (FW.DY[d] * ph + FW.DX[d] * spread * ph) * cs * .45 + ph * ph * cs * .15;
      wx.globalAlpha = 1 - ph; wx.beginPath(); wx.arc(px, py, cs * .045 * (1 - ph * .5), 0, Math.PI * 2); wx.fill();
    }
    wx.globalAlpha = 1;
  }
}

// ---------------------------------------------------------------- map
let wworld = 1;
function showFlowMap() {
  active = "flow"; closeOverlay();
  $("hub").hidden = true; $("wapp").hidden = true; $("wmap").hidden = false;
  const total = Object.values(WS_.stars).reduce((a, b) => a + b, 0);
  $("w-total").textContent = `${total} / ${WLEVELS.length * 3} stars`;
  const next = Math.min(WS_.highest + 1, WLEVELS.length);
  $("w-play").textContent = `Play level ${next}`; $("w-play").onclick = () => startFlow(next);
  const tabs = $("w-tabs"); tabs.innerHTML = "";
  for (let k = 1; k <= Math.ceil(WLEVELS.length / 50); k++) {
    const b = document.createElement("button"); b.className = "tab"; b.setAttribute("role", "tab"); b.textContent = `World ${k}`;
    b.setAttribute("aria-selected", String(k === wworld)); b.onclick = () => { wworld = k; showFlowMap(); }; tabs.appendChild(b);
  }
  const grid = $("w-grid"); grid.innerHTML = "";
  for (let n = (wworld - 1) * 50 + 1; n <= Math.min(wworld * 50, WLEVELS.length); n++) {
    const st = WS_.stars[n] || 0, open = S.unlock || n <= WS_.highest + 1, tier = WLEVELS[n - 1].tier;
    const b = document.createElement("button");
    b.className = "lvl flow" + (n === next && !st ? " next" : st ? " done" : "") + (open ? "" : " locked") + (tier === "H" ? " hard" : tier === "SH" ? " super" : "");
    b.innerHTML = open ? `${n}<span class="ls">${st ? "★".repeat(st) : ""}</span>` : `${n}`;
    b.disabled = !open; b.onclick = () => startFlow(n);
    b.setAttribute("aria-label", `Flow Fill level ${n}${st ? `, ${st} stars` : ""}${open ? "" : ", locked"}`);
    grid.appendChild(b);
  }
}

wc_.addEventListener("pointerdown", e => {
  if (!wlevel) return;
  const r = wc_.getBoundingClientRect(), g = wgeom();
  const x = Math.floor((e.clientX - r.left - g.ox) / g.cs), y = Math.floor((e.clientY - r.top - g.oy) / g.cs);
  if (x < 0 || y < 0 || x >= wlevel.w || y >= wlevel.h) return;
  wtap(y * wlevel.w + x);
});
window.addEventListener("resize", () => { if (!$("wapp").hidden) wlayout(); });
$("w-menu").onclick = () => { audio(); showFlowMap(); };
$("w-hint").onclick = whintNow; $("w-restart").onclick = () => startFlow(wnum);
$("w-back").onclick = showHub;
$("card-flow").onclick = () => { audio(); if (WS_.highest === 0) startFlow(1); else showFlowMap(); };
