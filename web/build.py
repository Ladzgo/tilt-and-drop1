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
gears_js = open(os.path.join(HERE, "gears.js")).read()
gears_ui = open(os.path.join(HERE, "gears-ui.js")).read()
with open(os.path.join(ROOT, "shared", "gears", "levels.json")) as f:
    glevels = [{**{k: d[k] for k in ("w", "h", "grid", "inventory", "moves", "par", "tier")}, "solution": d.get("meta", {}).get("solution", [])} for d in json.load(f)["levels"]]
gears_ui = gears_ui.replace("/*GEAR_LEVELS*/[]", json.dumps(glevels, separators=(",", ":")))
html = html.replace("/*GEARS*/", gears_js).replace("/*GEARSUI*/", gears_ui)
domino_js = open(os.path.join(HERE, "domino.js")).read()
domino_ui = open(os.path.join(HERE, "domino-ui.js")).read()
with open(os.path.join(ROOT, "shared", "domino", "levels.json")) as f:
    dlevels = [{**{k: d[k] for k in ("w", "h", "grid", "attempts", "par", "tier")}, "solution": d.get("meta", {}).get("solution", [])} for d in json.load(f)["levels"]]
domino_ui = domino_ui.replace("/*DOMINO_LEVELS*/[]", json.dumps(dlevels, separators=(",", ":")))
html = html.replace("/*DOMINO*/", domino_js).replace("/*DOMINOUI*/", domino_ui)
fold_js = open(os.path.join(HERE, "fold.js")).read()
fold_ui = open(os.path.join(HERE, "fold-ui.js")).read()
with open(os.path.join(ROOT, "shared", "fold", "levels.json")) as f:
    flevels = [{**{k: d[k] for k in ("w", "h", "front", "back", "target", "moves", "par", "tier")}, "solution": d.get("meta", {}).get("solution", [])} for d in json.load(f)["levels"]]
fold_ui = fold_ui.replace("/*FOLD_LEVELS*/[]", json.dumps(flevels, separators=(",", ":")))
html = html.replace("/*FOLD*/", fold_js).replace("/*FOLDUI*/", fold_ui)
flow_js = open(os.path.join(HERE, "flow.js")).read()
flow_ui = open(os.path.join(HERE, "flow-ui.js")).read()
with open(os.path.join(ROOT, "shared", "flow", "levels.json")) as f:
    wlevels = [{**{k: d[k] for k in ("w", "h", "grid", "sources", "cups", "moves", "par", "tier")}, "meta": {"solution": d.get("meta", {}).get("solution", [])}} for d in json.load(f)["levels"]]
flow_ui = flow_ui.replace("/*FLOW_LEVELS*/[]", json.dumps(wlevels, separators=(",", ":")))
html = html.replace("/*FLOW*/", flow_js).replace("/*FLOWUI*/", flow_ui)
html = html.replace("/*PRISM*/", prism_js).replace("/*PRISMUI*/", prism_ui).replace("/*ROLL*/", roll_js).replace("/*ROLLUI*/", roll_ui)
html = html.replace("/*SIM*/", sim).replace("/*LEVELS*/[]", json.dumps(levels, separators=(",", ":"))).replace("/*DAILY*/[]", json.dumps(daily, separators=(",", ":")))
os.makedirs(os.path.join(HERE, "dist"), exist_ok=True)
out = os.path.join(HERE, "dist", "index.html")
open(out, "w").write(html)
print(f"{out}: {len(html) // 1024} KB, {len(levels)} tilt levels, {len(daily)} daily, {len(plevels)} prism levels, {len(rlevels)} roll levels, {len(slevels)} slide levels, {len(glevels)} gear levels, {len(dlevels)} domino levels, {len(flevels)} fold levels, {len(wlevels)} flow levels")
