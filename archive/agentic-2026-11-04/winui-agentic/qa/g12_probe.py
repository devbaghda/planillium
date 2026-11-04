# G12 QA probes: AI "Key saved" summary, salary natural Shift+Tab, spin button, cap valid Enter.
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from b1f_lib import *
import b1f_lib as L

def F(): f = focused(); return "%s|%s|%s" % (f[0], f[1][:30], f[2])
def texts(keys_):
    return [t.element_info.name[:90] for t in L.WIN[0].descendants(control_type="Text") if any(k in (t.element_info.name or "") for k in keys_)]
def ai_state():
    tb = fe("AiKeyBox"); test = [b for b in w().descendants(control_type="Button") if b.element_info.name == "Test connection"]
    return {"sum": texts(["Key saved", "No key"]), "test_enabled": test[0].is_enabled() if test else None, "keylen": len(val(tb) or "")}
def blank():
    P = byid("SectionScroll").rectangle(); x = int(P.left + (P.right - P.left) * .92); y = int(P.top + (P.bottom - P.top) * .85)
    a = at(x, y); assert a[0] in ("SectionScroll", "") and a[2] == "pane", a
    bring_front(); click(x, y); time.sleep(1)
try:
    make_root(with_fixture=True); launch(); time.sleep(2); dismiss_dialogs(); refresh(); bring_front()
    section("AI"); refresh()
    log("P1 start", ai_state(), "cfg ai", cfg().get("ai"))
    m = fe("AiModelBox"); eclick(m); time.sleep(.8); keys("^aqa-m1"); time.sleep(.6); keys("{ENTER}"); time.sleep(1.2)
    log("P1 after model Enter (no key typed)", ai_state(), "focus", F(), "cfg ai", cfg().get("ai")); blank()
    k = fe("AiKeyBox"); eclick(k); time.sleep(.8); keys("sk-ant-abandoned"); time.sleep(.6); blank()
    log("P1 key typed + blank", ai_state(), "focus", F())
    eclick(fe("AiModelBox")); time.sleep(.8); keys("^aqa-m2"); time.sleep(.6); blank()
    log("P1 model blank after abandoned key", ai_state(), "focus", F(), "cfg ai", cfg().get("ai"))
    c = fe("AiCapBox"); eclick(c); time.sleep(.8); keys("^a9"); time.sleep(.6); keys("{ENTER}"); time.sleep(1.2)
    log("P4 cap valid Enter focus", F(), "cap", val(fe("AiCapBox")), "cfg ai", cfg().get("ai")); blank()
    section("Income"); refresh()
    eclick(salary_box()); time.sleep(.8); keys("+{TAB}"); time.sleep(1)
    log("P2 salary click, Shift+Tab (no Esc)", F()); blank()
    eclick(salary_box()); time.sleep(.8); keys("{ESC}"); time.sleep(.8); keys("{TAB}"); time.sleep(1)
    log("P2b salary click, Esc, Tab", F()); blank()
    section("Hours and reminders"); refresh()
    g = reveal(lambda: fe("Grace before alert")); v0 = val(g); r = g.rectangle()
    eclick(g); time.sleep(1.2); refresh()
    allb = [(b.element_info.automation_id, b.element_info.name, str(b.rectangle())) for b in w().descendants(control_type="Button")
            if b.rectangle().top >= r.top - 80 and b.rectangle().bottom <= r.bottom + 80]
    log("P3 buttons near Grace", allb)
    for dx in (8, 20, 34, 50):
        log("P3 at right of box +%d" % dx, at(r.right + dx, (r.top + r.bottom) // 2), at(r.right - dx, (r.top + r.bottom) // 2 + 6))
    shot(os.path.join(HERE, "g12_spin.png"))
except Abort as ex: log("ABORT", ex)
except Exception as ex:
    import traceback; log("ERROR", traceback.format_exc()[-800:])
finally: kill()
