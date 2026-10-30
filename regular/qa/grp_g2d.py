from grp_h import *
import grp_h as G
G.attach(); w().set_focus()
G.click("Show 50 more", "Hyperlink", wait=2.5); log("after +50:", G.showing())
G.click("Show 500 more", "Hyperlink", wait=3); log("after +500:", G.showing())
G.toggle("app", "Other"); log("Other after paging:", G.showing())
links = [e.element_info.name for e in w().descendants(control_type="Hyperlink") if "Show" in (e.element_info.name or "")]; log("links now", links)
G.toggle("app", "Other")
G.click([l for l in [e.element_info.name for e in w().descendants(control_type="Hyperlink")] if l and l.startswith("Show all")][0], "Hyperlink", wait=6)
log("after show all:", G.showing(), G.tcount())
G.nav("Today"); G.nav("Reports"); time.sleep(2)
log("after return: showing", G.showing(), "90d ticked?", byname("Apply filters across the last 90 days instead of just this day", "CheckBox").get_toggle_state())
G.toggle("tag", "-"); log("after return tag-:", G.showing()); G.toggle("tag", "-"); log("cleared", G.showing())
