from grp_h import *
import grp_h as G
G.attach(); w().set_focus()
G.click("Apply filters across the last 90 days instead of just this day", "CheckBox", wait=3)
log("after 90d: buttons", [b.element_info.name for b in w().descendants(control_type="Button") if "Mark" in (b.element_info.name or "")][:3])
log("texts showing:", texts("howing"), texts("rows"))
before = set((e.element_info.control_type, e.element_info.name) for e in w().descendants())
for n in ("Filter diary by category", "Filter diary by app", "Filter diary by page", "Filter diary by tag"):
    G.click(n, "Button", wait=1.5)
    new = [(e.element_info.control_type, e.element_info.name) for e in w().descendants()]
    log(n, "->", [x for x in new if x not in before][:40])
    shot("grp_g2_" + n.split()[-1] + ".png")
    send_keys("{ESC}"); time.sleep(.8)
c = byname("Search field", "ComboBox"); c.click_input(); time.sleep(1.2)
log("search fields", [i.element_info.name for i in w().descendants(control_type="ListItem")])
send_keys("{ESC}"); time.sleep(.6)
