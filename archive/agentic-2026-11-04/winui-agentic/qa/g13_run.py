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

    step("L4 key typed, Esc"); eclick(key()); time.sleep(.8); keys("sk-ant-esc-111"); time.sleep(.6)
    log("  typed diff-px", keyfilled("l4_typed")); keys("{ESC}"); time.sleep(1); state("l4_after")

    step("L1 key, blank, Model Enter"); eclick(key()); time.sleep(.8); keys("sk-ant-l1-222"); time.sleep(.6); blank()
    log("  after blank diff-px", keyfilled("l1_blank"), "focus", F())
    eclick(mod()); time.sleep(.8); keys("^aqa-model-l1"); time.sleep(.5); keys("{ENTER}"); time.sleep(1.2); state("l1_after")

    step("L2 key, blank, cap Enter"); eclick(key()); time.sleep(.8); keys("sk-ant-l2-333"); time.sleep(.6); blank()
    log("  after blank diff-px", keyfilled("l2_blank"))
    eclick(cap()); time.sleep(.8); keys("^a7"); time.sleep(.5); keys("{ENTER}"); time.sleep(1.2); state("l2_after")

    step("L3b key, blank, Save"); eclick(key()); time.sleep(.8); keys("sk-ant-l3b-444"); time.sleep(.6); blank()
    eclick(byid("AiSaveBtn")); time.sleep(1.2); state("l3b_after")

    step("L5 key, Tab, model, Enter"); eclick(key()); time.sleep(.8); keys("sk-ant-l5-555"); time.sleep(.6); keys("{TAB}"); time.sleep(.8)
    log("  after Tab focus", F(), "diff-px", keyfilled("l5_tab")); keys("^aqa-model-l5"); time.sleep(.5); keys("{ENTER}"); time.sleep(1.2); state("l5_after_model_enter")
    step("L5b then click cap box, 9, Enter"); eclick(cap()); time.sleep(.8); keys("^a9"); time.sleep(.5); keys("{ENTER}"); time.sleep(1.2); state("l5b_after_cap_enter")
    step("L5c then blank click (key left long ago)"); blank(); state("l5c_after_blank")

    step("SIB key box: Ctrl+A, new key, click Model box (other box), blank"); eclick(key()); time.sleep(.8); keys("^asibkey-666"); time.sleep(.5)
    eclick(mod()); time.sleep(.8); log("  focus", F()); keys("{ESC}"); time.sleep(.8); state("sib_after")

    step("L3c Enter in key box stores"); eclick(key()); time.sleep(.8); keys("^ask-ant-l3c-777"); time.sleep(.5); keys("{ENTER}"); time.sleep(1.2); state("l3c_after")
    step("Remove key via cfg? no - L3a key + Save stores"); eclick(key()); time.sleep(.8); keys("^ask-ant-l3a-888"); time.sleep(.5)
    eclick(byid("AiSaveBtn")); time.sleep(1.2); state("l3a_after")
except Abort as ex:
    log("ABORT", ex)
except Exception as ex:
    import traceback; log("ERROR", traceback.format_exc()[-800:])
finally:
    kill()
    open(os.path.join(HERE, "g13_run.out"), "w", encoding="utf-8").write("\n".join(LOG))
