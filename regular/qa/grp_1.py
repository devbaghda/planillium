from grp_h import *
import grp_h as G
try:
    launch(); H.kill(); G.seed(); G.relaunch()
    win = w(); win.set_focus(); win.maximize(); time.sleep(1.5)
    # skip Welcome back dialog if any
    for b in win.descendants(control_type="Button"):
        if b.element_info.name in ("Skip", "Not now", "Close", "Later"): 
            try: b.click_input(); time.sleep(1); break
            except Exception: pass
    G.dump(S + r"\today.txt")
    for item, f in (("ReportsItem", "reports"), ("DiaryItem", "diary")):
        try: w().child_window(auto_id=item).click_input(); time.sleep(3); G.dump(S + "\\" + f + ".txt"); shot("grp_" + f + ".png")
        except Exception as ex: log(item, ex)
finally:
    pass
log("pid", open(H.PIDF).read())
