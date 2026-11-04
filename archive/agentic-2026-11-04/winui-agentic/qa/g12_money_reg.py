# G12 QA: MoneyBox regression after the ceiling change (salary): minus, empty, text, spaces; qa-extra B1h Esc after a new save.
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from b1f_lib import *
import b1f_lib as L
def F(): f = focused(); return "%s|%s" % (f[0], f[2])
def msgs():
    return [t.element_info.name[:90] for t in L.WIN[0].descendants(control_type="Text") if any(k in (t.element_info.name or "") for k in ("Not saved", "too large", "kept", "must", "back in the box"))]
def blank():
    P = byid("SectionScroll").rectangle(); x = int(P.left + (P.right - P.left) * .92); y = int(P.top + (P.bottom - P.top) * .85)
    a = at(x, y); assert a[0] in ("SectionScroll", "") and a[2] == "pane", a
    bring_front(); click(x, y); time.sleep(1)
try:
    make_root(with_fixture=True); launch(); time.sleep(2); dismiss_dialogs(); refresh(); bring_front()
    section("Income"); refresh()
    for txt, how in (("-5", "{TAB}"), ("", "{ENTER}"), ("abc", "{TAB}"), ("   ", "{ENTER}"), ("0", "{TAB}")):
        bring_front(); eclick(salary_box()); time.sleep(.7); keys("^a{DEL}" + txt if txt else "^a{DEL}"); time.sleep(.6); keys(how); time.sleep(1.2)
        log("salary %r %s -> box %r stored %r focus %s msgs %r" % (txt, how, val(salary_box()), salary(), F(), msgs())); blank()
    eclick(salary_box()); time.sleep(.7); keys("^a3333"); keys("{ENTER}"); time.sleep(1.2)
    eclick(salary_box()); time.sleep(.7); keys("^a4444"); time.sleep(.6); keys("{ESC}"); time.sleep(1)
    log("B1h: saved 3333 then 4444 Esc -> box %r stored %r focus %s" % (val(salary_box()), salary(), F()))
except Abort as ex: log("ABORT", ex)
except Exception as ex:
    import traceback; log("ERROR", traceback.format_exc()[-800:])
finally: kill()
