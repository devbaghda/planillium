from grp_h import *
import grp_h as G
from PIL import Image
G.attach_pid(); w().set_focus()
G.nav("Today")
def dot(tag):
    shot("grp_g6_" + tag + ".png"); im = Image.open(SHOTS + "\grp_g6_" + tag + ".png").convert("RGB")
    r = byname("Today", "ListItem").rectangle(); wr = w().rectangle()
    box = (r.left - wr.left + 180, r.top - wr.top, r.right - wr.left, r.bottom - wr.top)
    px = [im.getpixel((x, y)) for x in range(box[0], box[2]) for y in range(box[1], box[3])]
    blue = sum(1 for p in px if p[2] > 180 and p[0] < 150)
    return blue
log("before complete: banner", texts("overdue task"), "dot px", dot("a"))
byname("Overdue QA task", "CheckBox").click_input(); time.sleep(2.5)
log("after complete: banner", texts("overdue task"), "dot px", dot("b"))
G.nav("Plans"); log("on Plans: dot px", dot("c")); G.nav("Today")
c = [e for e in w().descendants(control_type="CheckBox") if e.element_info.name == "Overdue QA task"]
log("checkbox still there?", len(c))
if c: c[0].click_input(); time.sleep(2.5); log("after un-complete: banner", texts("overdue task"), "dot px", dot("d"))
