from grp_h import *
import grp_h as G
G.attach(); w().set_focus()
def inv(name):
    e = byname(name, "Hyperlink"); log("  rect", e.rectangle()); e.invoke(); time.sleep(3)
inv("Show 50 more"); log("after +50:", G.showing())
inv("Show 1000 more"); log("after +1000:", G.showing())
l = [e.element_info.name for e in w().descendants(control_type="Hyperlink") if (e.element_info.name or "").startswith("Show")]; log("links", l)
inv([x for x in l if x.startswith("Show all")][0]); log("after show all:", G.showing())
G.wheel_down(60); shot("grp_g2_showall_bottom.png")
