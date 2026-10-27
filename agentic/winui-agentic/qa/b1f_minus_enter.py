# Re-check of part C "Studioshoo minus enter": type -5 in the tag box with real keys, press Enter; record the box
# value before/after Enter, visible reason, stored value and focus. Repeated; also for the salary box as control.
from b1f_lib import *
make_root(with_fixture=True)
p = launch()
def msgs():
    return [t.element_info.name[:45] for t in w().descendants(control_type="Text")
            if any(k in (t.element_info.name or "").lower() for k in ("negative", "not saved")) and t.rectangle().width() > 0]
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front(); section("Income"); refresh(); time.sleep(1)
    for box in ("Studioshoo", "Previous salary"):
        for i in range(8):
            el = fe(box); eclick(el, 0.12); time.sleep(.4)
            keys("^a{BACKSPACE}"); time.sleep(.2); keys("-5"); time.sleep(.4)
            before = val(fe(box)); keys("{ENTER}"); time.sleep(1.0)
            after = val(fe(box)); f = focused()[:3]
            log("%s #%d typed -5: before-enter %r after-enter %r msgs %r stored sal=%r tags=%r focus %r" % (
                box, i, before, after, msgs(), salary(), tags(), f))
            eclick(fe(box), 0.12); time.sleep(.3); keys("{ESC}"); time.sleep(.3)
            keys("^a{BACKSPACE}" + ("1600" if box == "Studioshoo" else "2925") + "{ENTER}"); time.sleep(.8)
finally:
    kill()
