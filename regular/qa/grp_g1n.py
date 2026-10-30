from grp_h import *
import grp_h as G, ctypes
G.attach(); w().set_focus()
G.click("Week", "RadioButton", wait=2)
ctypes.windll.user32.ShowWindow(G.HW, 9); time.sleep(1)
ctypes.windll.user32.MoveWindow(G.HW, 100, 50, 760, 900, True); time.sleep(2)
e = byname("Of which paid work", "Text"); r = e.rectangle(); wr = w().rectangle()
log("narrow: col rect", r, "win", wr, "inside", r.right <= wr.right and r.width() > 0)
sb = [x.element_info.automation_id or x.element_info.name for x in w().descendants(control_type="ScrollBar")]
log("scrollbars", sb)
shot("grp_g1_narrow.png")
ctypes.windll.user32.ShowWindow(G.HW, 3); time.sleep(1)
