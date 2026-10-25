# Line 5 after keyboard exits, Remove/section-list/Add buttons; line 6 in the new-tag box; Esc focus.
from b1d_h import *
launch(); w().set_focus(); time.sleep(1); goto()
def at(x, y):
    r = w().rectangle(); return (r.left + x, r.top + y)
def addbtn(): return w().child_window(auto_id="NewPaidTagAdd")
CLICKS = {"blank": (850, 230), "heading Paid work": (310, 350), "label paid-work text": (500, 385), "label readout": (555, 230)}
PRE = {
 "salary Enter": lambda: (typein(SAL, "3001"), send_keys("{ENTER}")),
 "salary Tab": lambda: (typein(SAL, "3002"), send_keys("{TAB}")),
 "salary Shift+Tab": lambda: (typein(SAL, "3003"), send_keys("+{TAB}")),
 "tag Enter": lambda: (typein(TAG, "1601"), send_keys("{ENTER}")),
 "tag Tab": lambda: (typein(TAG, "1602"), send_keys("{TAB}")),
 "add-line Esc": lambda: (typein(NAME, "pp"), send_keys("{ESC}")),
 "add-line Tab x2": lambda: (typein(NAME, "pp"), typein(NEW, "9"), send_keys("{TAB}{TAB}")),
 "refused button Add": lambda: (typein(NAME, "pp"), typein(NEW, ""), addbtn().click_input()),
}
bad = []
for p, fn in PRE.items():
    fn(); time.sleep(.8)
    ok, f = nine(f"{p} (before any click)")
    if f[1] == SAL or not ok: bad.append((p, "-", f))
    for c, xy in CLICKS.items():
        fn(); time.sleep(.8)
        mouse.click(coords=at(*xy)); time.sleep(1)
        ok, f = nine(f"{p} -> click {c}")
        if f[1] == SAL or not ok: bad.append((p, c, f))
    send_keys("{ESC}"); mouse.click(coords=at(850, 230)); time.sleep(.5)
log("== section list: General then Income")
w().child_window(title="General", control_type="ListItem").click_input(); time.sleep(1.2)
w().child_window(title="Income", control_type="ListItem").click_input(); time.sleep(1.2)
ok, f = nine("after section list -> Income"); 
if f[1] == SAL or not ok: bad.append(("section list", "Income", f))
log("== nav away and back via sidebar")
w().child_window(title="Today", control_type="ListItem").click_input(); time.sleep(1.5); goto()
ok, f = nine("after sidebar Today -> Settings -> Income")
if f[1] == SAL or not ok: bad.append(("sidebar", "back", f))
log("== L6 in new-tag box: add 'big' with 500 123 by mouse")
typein(NAME, "big"); typein(NEW, "500 123"); log("  new box shows", repr(val(NEW)))
addbtn().click_input(); time.sleep(1.2); log("  cfg", cfg(), "hourly", hourly(), "focus", foc())
ok, f = nine("after Add big")
log("== Remove button on 'big'")
rem = [b for b in w().descendants(control_type="Button") if b.element_info.name == "Remove"]
log("  remove buttons", len(rem)); rem[-1].click_input(); time.sleep(1.5)
log("  cfg", cfg(), "focus", foc())
ok, f = nine("after Remove click")
if f[1] == SAL or not ok: bad.append(("Remove", "-", f))
log("== Esc in add line, then letters on the section list")
typein(NAME, "ee"); send_keys("{ESC}"); time.sleep(.8); log("  focus", foc())
ok, f = nine("after Esc in add line")
send_keys("s"); time.sleep(1); log("  after typing 's': focus", foc(), "income panel visible?", bool(texts("Paid work")))
log("BAD", len(bad))
for b in bad: log("  BAD", b)
