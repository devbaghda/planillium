# Line 5: real clicks on blank / headings / labels / a button; focus must never land in the salary box; "9" changes nothing.
from b1d_h import *
launch(); w().set_focus(); time.sleep(1); goto()
log("fixture", snap())
def at(x, y):
    r = w().rectangle(); return (r.left + x, r.top + y)
TGT = {   # window-relative coordinates (from b1d_start.png)
 "blank right of salary": (850, 230),
 "blank right of toggle": (700, 305),
 "blank below add line": (700, 575),
 "heading Settings": (330, 75),
 "heading Income": (310, 142),
 "heading Paid work": (310, 350),
 "label Employment status": (335, 271),
 "label paid-work text": (500, 385),
 "label salary readout": (555, 230),
 "label tag readout": (555, 466),
 "label bottom text": (500, 605),
 "label salary box header": (380, 196),
 "label new-tag header": (330, 508),
 "status strip": (500, 740),
 "sidebar BALANCE label": (70, 538),
 "button Employment toggle (x2)": (297, 304),
}
SRC = {
 "nothing": None,
 "salary box": lambda: typein(SAL, "", clear=False),
 "salary box edited": lambda: typein(SAL, "3000"),
 "tag box": lambda: typein(TAG, "", clear=False),
 "tag box edited": lambda: typein(TAG, "1700"),
 "add-line name": lambda: typein(NAME, "qq"),
 "add-line amount": lambda: typein(NEW, "55"),
}
bad = []; n = 0
for sname, pre in SRC.items():
    for tname, (x, y) in TGT.items():
        if pre: pre()
        mouse.click(coords=at(x, y)); time.sleep(1.0)
        if "x2" in tname:
            f0 = foc(); mouse.click(coords=at(x, y)); time.sleep(1.0)
        f = foc()
        same, _ = nine(f"{sname} -> {tname}")
        n += 1
        if f[1] == SAL or not same: bad.append((sname, tname, f, same))
log("cfg end", cfg(), "window", w().rectangle())
log("CASES", n, "FAILURES", len(bad))
for b in bad: log("  BAD", b)
