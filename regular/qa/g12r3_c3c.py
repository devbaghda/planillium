from g12r3_h import *
from pywinauto import mouse
page("Hours and reminders")
G=lambda: find(name="Grace before alert", ct="Edit", starts=True)
Rp=lambda: find(name="Repeat every", ct="Edit", starts=True)
def glyph(e): r=e.rectangle(); return (r.right-14, (r.top+r.bottom)//2)
def popup():
    out=[]
    for e in w().descendants():
        i=e.element_info
        if i.control_type in ("Button","RepeatButton","Window") and i.rectangle.width()>0 and i.automation_id not in ("Minimize","Maximize","Close","IncomeFocusPark"): out.append((i.control_type,i.name,i.automation_id,str(i.rectangle)))
    return out
def ckey(): c=cfg(); return {k:c.get(k) for k in ("grace_min","repeat_min","alert_grace_min","alert_repeat_min") if k in c} or [k for k in c][:40]
print("cfg0", ckey())
# A: focus in Grace, hover glyph, look
click(G()); gx=glyph(G()); guard(); mouse.move(coords=gx); time.sleep(0.8); print("popup on hover:", popup()); shot("c3_hover.png")
