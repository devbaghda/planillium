# G13 QA (agentic): API key stored only by Save / Enter in the key box. Real input, own PID only, scratch root.
import os, sys, time, json, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from b1f_lib import *
import b1f_lib as L
from PIL import Image, ImageChops

OUT = os.path.join(HERE, "g13_shots"); os.makedirs(OUT, exist_ok=True)
def F(): f = focused(); return "%s|%s|%s" % (f[0], f[1][:30], f[2])
def aicfg():
    c = cfg(); return {k: v for k, v in c.items() if "ai" in k.lower() or "model" in k.lower()}
def texts(sub=("key", "Key", "saved", "Saved", "limit", "Not ", "Paste", "paste")):
    out = []
    for t in L.WIN[0].descendants(control_type="Text"):
        n = t.element_info.name or ""
        if any(k in n for k in sub) and t.rectangle().width() > 0: out.append(n[:120])
    return out
def btn_state():
    r = {}
    for b in L.WIN[0].descendants(control_type="Button"):
        if b.element_info.automation_id in ("AiTestBtn", "AiRemoveBtn", "AiSaveBtn"):
            r[b.element_info.automation_id] = b.is_enabled()
    return r
def find_blank():
    P = byid("SectionScroll").rectangle()
    for fx in (0.92, 0.85, 0.75):
        for fy in (0.85, 0.7, 0.5, 0.3):
            x = int(P.left + (P.right - P.left) * fx); y = int(P.top + (P.bottom - P.top) * fy)
            a = at(x, y)
            if a[2] in ("pane", "group", "scroll viewer", "custom", "") and a[0] in ("", "SectionScroll") and not a[1]:
                return (x, y, a)
    return None
def blank():
    b = find_blank()
    if b is None: raise Abort("no blank point")
    bring_front(); click(b[0], b[1]); time.sleep(1)
    return b
def step(name):
    if not bring_front(): raise Abort("not foreground at " + name)
    time.sleep(.3); log("== " + name)
key = lambda: byid("AiKeyBox") or fe("Anthropic API key")
mod = lambda: fe("AiModelBox") or fe("Model")
cap = lambda: fe("AiCapBox") or fe("Monthly AI limit")
BASE = [None]
def keycrop(tag):
    p = os.path.join(OUT, tag + ".png"); shot(p)
    im = Image.open(p).convert("L"); r = key().rectangle(); wr = L.WIN[0].rectangle()
    c = im.crop((r.left - wr.left + 8, r.top - wr.top + 26, r.right - wr.left - 40, r.bottom - wr.top - 4))
    return c
def keyfilled(tag):
    c = keycrop(tag)
    if BASE[0] is None: return None
    d = ImageChops.difference(c, BASE[0]); n = sum(1 for v in d.getdata() if v > 60)
    return n
def state(tag):
    log("  focus", F(), "| keybox diff-px vs empty:", keyfilled(tag), "| buttons", btn_state())
    log("  cfg", aicfg()); log("  texts", texts())

try:
    make_root(with_fixture=False)
    launch(); time.sleep(2); dismiss_dialogs(); refresh(); bring_front()
    step("open AI"); log("AI section", section("AI")); refresh()
    log("edits", [(e.element_info.automation_id, e.element_info.name) for e in edits()])
    BASE[0] = keycrop("base_empty"); state("s0")

    step("L3a fresh: key + Save (no blank click)"); eclick(key()); time.sleep(.8); keys("sk-ant-l3a-fresh"); time.sleep(.5)
    eclick(byid("AiSaveBtn")); time.sleep(1.2); state("l3a_fresh")
except Abort as ex:
    log("ABORT", ex)
except Exception as ex:
    import traceback; log("ERROR", traceback.format_exc()[-800:])
finally:
    kill()
    open(os.path.join(HERE, "g13_l3a.out"), "w", encoding="utf-8").write("\n".join(LOG))
