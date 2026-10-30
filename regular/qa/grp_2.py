from grp_h import *
import grp_h as G, ctypes
h = 984382
ctypes.windll.user32.ShowWindow(h, 3); time.sleep(1.5)
H._w = Application(backend="uia").connect(handle=h).window(handle=h)
win = w(); win.set_focus(); time.sleep(1)
G.dump(S + r"\today.txt")
for item, f in (("ReportsItem", "reports"), ("DiaryItem", "diary")):
    try: w().child_window(auto_id=item).click_input(); time.sleep(3); G.dump(S + "\\" + f + ".txt"); shot("grp_" + f + ".png")
    except Exception as ex: log(item, ex)
