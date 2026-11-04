# G12 QA: L15 spin button click (launch 1), then L8 paid-work cents Week/Month/Year with seeded diary rows (launch 2).
import os, sys, time, sqlite3, datetime
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from b1f_lib import *
import b1f_lib as L

def texts():
    out = []
    for t in w().descendants(control_type="Text"):
        n = t.element_info.name or ""
        try: r = t.rectangle()
        except Exception: continue
        if r.width() > 0 and n: out.append((r.top, r.left, n))
    return sorted(out)
try:
    make_root(with_fixture=True); launch(); time.sleep(2); dismiss_dialogs(); refresh(); bring_front()
    if os.environ.get("SPIN", "1") == "1":
        section("Hours and reminders"); refresh()
        g = reveal(lambda: fe("Grace before alert")); r = g.rectangle(); v0 = val(g)
        eclick(g); time.sleep(1.2)
        xs = r.right - 14; yu = r.top + (r.bottom - r.top) // 3; yd = r.bottom - (r.bottom - r.top) // 4
        move(xs, yu); time.sleep(.8)
        log("L15 before", v0, "at up point", at(xs, yu), "at down point", at(xs, yd))
        shot(os.path.join(HERE, "g12_spin_hover.png"))
        click(xs, yu); time.sleep(1.2); v1 = val(fe("Grace before alert"))
        log("after click up-half", v1, "cfg", {k: v for k, v in cfg().items() if "grace" in k.lower()})
        click(xs, yd); time.sleep(1.2); v2 = val(fe("Grace before alert"))
        log("after click down-half", v2, "focus", focused()[:3])
    kill(); time.sleep(2)
    db = os.path.join(ROOT, "data", "progress.db"); con = sqlite3.connect(db)
    today = datetime.date.today(); mon = today - datetime.timedelta(days=today.weekday())
    rows = []
    for i, mins in enumerate((37, 37, 41)):
        d = (mon + datetime.timedelta(days=i)).isoformat()
        rows.append((d, "10:00", "10:%02d" % mins if mins < 60 else "11:00", mins, "paid", "Studio work - Visual Studio Code", None, "Studioshoo"))
    con.executemany("INSERT INTO time_diary(date,start_time,end_time,duration_min,category,window,description,tag) VALUES (?,?,?,?,?,?,?,?)", rows)
    con.commit(); log("seeded", rows); con.close()
    launch(); time.sleep(2); dismiss_dialogs(); refresh(); bring_front()
    nav = [li for li in w().descendants(control_type="ListItem") if li.element_info.name == "Reports"]
    eclick(nav[0]); time.sleep(3); refresh()
    for label in ("Week", "Month", "Year"):
        bring_front(); time.sleep(.3)
        sel = [r for r in w().descendants(control_type="RadioButton") if r.element_info.name == label]
        if not sel: log(label, "no radio"); continue
        eclick(sel[0]); time.sleep(2.5)
        ts = texts(); shot(os.path.join(HERE, "g12_l8_%s.png" % label))
        log("---", label)
        for t in ts:
            if any(ch.isdigit() for ch in t[2]) or "paid" in t[2].lower() or "Total" in t[2]: log("   ", t[0], t[1], t[2][:80])
except Abort as ex: log("ABORT", ex)
except Exception as ex:
    import traceback; log("ERROR", traceback.format_exc()[-800:])
finally: kill()
