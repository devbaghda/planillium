from grp_h import *
import grp_h as G, ctypes
G.attach(); w().set_focus()
ctypes.windll.user32.ShowWindow(G.HW, 9); time.sleep(1)
ctypes.windll.user32.MoveWindow(G.HW, 100, 50, 900, 900, True); time.sleep(2)
t = byname("Mon 26.10", "Text"); r = t.rectangle()
mouse.move(coords=(r.left + 300, r.top)); time.sleep(.5)
for i in range(6): mouse.scroll(coords=(r.left + 300, r.top), wheel_dist=-3) if False else None
# horizontal: shift+wheel
send_keys("{VK_SHIFT down}"); mouse.scroll(coords=(r.left + 300, r.top), wheel_dist=-10); send_keys("{VK_SHIFT up}"); time.sleep(1)
e = byname("Of which paid work", "Text"); log("after shift-wheel col rect", e.rectangle())
shot("grp_g1_narrow_scrolled.png")
ctypes.windll.user32.ShowWindow(G.HW, 3); time.sleep(1)
