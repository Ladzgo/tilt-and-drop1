"""Builds web/dist/index.html: app.html + sim.js + levels 1-300 + daily puzzles inlined (one file)."""
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(ROOT, "tools", "levelgen"))
from tiltdrop.pack import read_pack  # noqa: E402

def slim(d):
    out = {k: d[k] for k in ("w", "h", "grid", "gates", "moves", "par", "tier")}
    sol = d.get("meta", {}).get("solution")
    if sol:
        out["solution"] = sol
    return out

with open(os.path.join(ROOT, "shared", "levels", "curated.json")) as f:
    levels = [slim(d) for d in json.load(f)["levels"]]
with open(os.path.join(ROOT, "shared", "levels", "daily.tdp.gz"), "rb") as f:
    daily = [slim(lv.to_json()) for lv in read_pack(f.read())]
html = open(os.path.join(HERE, "app.html")).read()
sim = open(os.path.join(HERE, "sim.js")).read()
prism_js = open(os.path.join(HERE, "prism.js")).read()
prism_ui = open(os.path.join(HERE, "prism-ui.js")).read()
with open(os.path.join(ROOT, "shared", "prism", "levels.json")) as f:
    plevels = [{k: d[k] for k in ("w", "h", "grid", "moves", "par", "tier")} for d in json.load(f)["levels"]]
prism_ui = prism_ui.replace("/*PRISM_LEVELS*/[]", json.dumps(plevels, separators=(",", ":")))
roll_js = open(os.path.join(HERE, "roll.js")).read()
roll_ui = open(os.path.join(HERE, "roll-ui.js")).read()
with open(os.path.join(ROOT, "shared", "roll", "levels.json")) as f:
    rlevels = [{**{k: d[k] for k in ("w", "h", "grid", "cube", "moves", "par", "tier")}, "solution": d.get("meta", {}).get("solution", "")} for d in json.load(f)["levels"]]
roll_ui = roll_ui.replace("/*ROLL_LEVELS*/[]", json.dumps(rlevels, separators=(",", ":")))
slide_js = open(os.path.join(HERE, "slide.js")).read()
slide_ui = open(os.path.join(HERE, "slide-ui.js")).read()
with open(os.path.join(ROOT, "shared", "slide", "levels.json")) as f:
    slevels = [{**{k: d[k] for k in ("w", "h", "grid", "capacity", "tier")}, "solution": d.get("meta", {}).get("solution", [])} for d in json.load(f)["levels"]]
slide_ui = slide_ui.replace("/*SLIDE_LEVELS*/[]", json.dumps(slevels, separators=(",", ":")))
html = html.replace("/*SLIDE*/", slide_js).replace("/*SLIDEUI*/", slide_ui)
html = html.replace("/*PRISM*/", prism_js).replace("/*PRISMUI*/", prism_ui).replace("/*ROLL*/", roll_js).replace("/*ROLLUI*/", roll_ui)
html = html.replace("/*SIM*/", sim).replace("/*LEVELS*/[]", json.dumps(levels, separators=(",", ":"))).replace("/*DAILY*/[]", json.dumps(daily, separators=(",", ":")))
os.makedirs(os.path.join(HERE, "dist"), exist_ok=True)
out = os.path.join(HERE, "dist", "index.html")
open(out, "w").write(html)
print(f"{out}: {len(html) // 1024} KB, {len(levels)} tilt levels, {len(daily)} daily, {len(plevels)} prism levels, {len(rlevels)} roll levels, {len(slevels)} slide levels")
