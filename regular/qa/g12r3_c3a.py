from g12r3_h import *
page("AI")
cb=find(aid="AiCapBox"); click(cb); print("focus",focused(),"val",val(cb))
time.sleep(0.8)
from pywinauto import Desktop
for top in Desktop(backend="uia").windows(process=PID):
    print("TOP", top.element_info.control_type, top.element_info.name, top.element_info.rectangle)
    for e in top.descendants():
        i=e.element_info
        if i.control_type in ("Button","RepeatButton") or "Popup" in (i.name or ""): print("  ",i.control_type,i.name,i.automation_id,i.rectangle)
shot("c3_focus.png")
