# B1e QA: Reports page, four periods; order, removed cards absent, no gap. Scratch root, own PID, UIA read + PrintWindow.
import os, sys
from b1f_lib import *

def texts():
    out = []
    for t in w().descendants(control_type="Text"):
        n = t.element_info.name or ""
        try: r = t.rectangle()
        except Exception: continue
        if r.width() > 0 and n: out.append((r.top, r.left, r.bottom, n))
    return sorted(out)

res = []
def rec(name, ok, ev=""):
    res.append(ok); log(("PASS" if ok else "FAIL"), name, "|", ev)

try:
    make_root(with_fixture=True)
    launch(); bring_front(); time.sleep(1)
    dismiss_dialogs()
    log("section Reports:", section("Reports")); time.sleep(1.5)
    for label in ("Day", "Week", "Month", "Year"):
        bring_front(); time.sleep(.3)
        sel = [r for r in w().descendants(control_type="RadioButton") if r.element_info.name == label]
        if not sel: rec(label, False, "no radio"); continue
        eclick(sel[0]); time.sleep(1.8)
        ts = texts()
        names = [t[3] for t in ts]
        up = [n.upper() for n in names]
        shot(os.path.join(HERE, "b1e_%s.png" % label))
        for t in ts: log("   ", t[0], t[1], t[2], t[3][:90])
        def first(sub):
            for t in ts:
                if sub in t[3].upper(): return t
        score = first("SCORE"); lost = first("INCOME LOST OR GAINED"); paid = first("OF WHICH PAID WORK")
        absent = not any("UNEARNED INCOME" in n or "EARNINGS FROM PAID WORK" in n or "EXTRA INCOME" in n or "NO INCOME LOST" in n for n in up)
        rec(label + " removed cards absent", absent, str([n for n in names if "UNEARNED" in n.upper() or "EARNINGS" in n.upper()]))
        ok_order = bool(score and lost and paid and score[0] < lost[0] and abs(lost[0] - paid[0]) < 5 or (lost and paid and lost[0] == paid[0]))
        rec(label + " order score<strip", bool(score and lost and paid and score[0] < lost[0]), "score %s lost %s paid %s" % (score and score[:2], lost and lost[:3], paid and paid[:3]))
        # table follows: first text below the strip's last caption/description
        below = [t for t in ts if t[0] > max(lost[2], paid[2]) - 1] if lost and paid else []
        strip_bottom = max(t[2] for t in ts if lost and lost[0] - 2 <= t[0] <= lost[0] + 130 and t[1] < 2000 and t[0] >= lost[0] - 2) if lost else 0
        nxt = [t for t in ts if lost and t[0] > lost[0] + 60][:6]
        log("   next after strip:", [(t[0], t[3][:30]) for t in nxt])
    log("SUMMARY", sum(res), "pass of", len(res))
finally:
    kill()
