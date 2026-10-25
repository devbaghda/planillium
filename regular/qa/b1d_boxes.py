# Lines 6, 9, 10 + app-qa-checks §5 standing input-box checks on the salary and tag boxes.
from b1d_h import *
launch(); w().set_focus(); time.sleep(1); goto()
def status():
    try: return w().child_window(auto_id="SaveStatus").window_text()
    except Exception: return "ERR"
def at(x, y):
    r = w().rectangle(); return (r.left + x, r.top + y)
def st(tag, box):
    log(f"  [{tag}] box={val(box)!r} cfg={cfg()} hourly={hourly()} red={red()} status={status()!r} focus={foc()[1:]}")
EXITS = {
 "Tab": lambda: send_keys("{TAB}"),
 "Enter": lambda: send_keys("{ENTER}"),
 "Esc": lambda: send_keys("{ESC}"),
 "click other field": None,   # filled per box
 "click blank": lambda: mouse.click(coords=at(850, 230)),
 "click heading": lambda: mouse.click(coords=at(310, 350)),
}
INPUTS = ["", "   ", "abc", "-", "-5", "500 123", "99999999999999999999999", "0", "4321"]
for box, other in ((SAL, TAG), (TAG, SAL)):
    log(f"===== box {box}")
    for inp in INPUTS:
        for ename, ex in EXITS.items():
            typein(box, inp); before_box = val(box)
            if ex is None: edit(other).click_input()
            else: ex()
            time.sleep(1.0)
            st(f"{inp!r} typed->{before_box!r}, exit {ename}", box)
            # always leave the other box/focus clean
            if ename == "click other field": send_keys("{TAB}"); time.sleep(.3)
            # restore a valid stored value for the next case if a minus was left
            if "-" in val(box):
                typein(box, "2925" if box == SAL else "1600"); send_keys("{ENTER}"); time.sleep(.6)
log("== L6 sign path: -500 123 then delete the minus")
typein(SAL, "-500 123"); st("typed -500 123", SAL)
send_keys("{HOME}{DELETE}"); time.sleep(.6); st("minus deleted", SAL)
send_keys("{ENTER}"); time.sleep(.8); st("Enter", SAL)
shot("b1d_l6.png")
log("== L10 spinners: children of money boxes")
for e in w().descendants(control_type="Edit"):
    n = e.element_info.name
    kids = [(c.element_info.control_type, c.element_info.name) for c in e.descendants()]
    log("  ", n, "children:", kids)
log("final", cfg())
