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
html = html.replace("/*SIM*/", sim).replace("/*LEVELS*/[]", json.dumps(levels, separators=(",", ":"))).replace("/*DAILY*/[]", json.dumps(daily, separators=(",", ":")))
os.makedirs(os.path.join(HERE, "dist"), exist_ok=True)
out = os.path.join(HERE, "dist", "index.html")
open(out, "w").write(html)
print(f"{out}: {len(html) // 1024} KB, {len(levels)} levels, {len(daily)} daily")
