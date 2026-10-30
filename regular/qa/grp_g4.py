from grp_h import *
import grp_h as G
G.attach_pid(); w().set_focus()
try: G.click("Cancel", "Button", wait=1.5)
except Exception: pass
G.nav("Settings"); G.click("AI", "ListItem"); G.typein("API key", "sk-qa-fake"); byname("Save", "Button").click_input(); time.sleep(1.5)
G.nav("Plans"); G.click("Replace remaining tasks: QA plan", "Button", wait=2.5)
btns = [(e.element_info.name, e.element_info.automation_id) for e in w().descendants(control_type="Button")]; log("dialog buttons", btns[:12])
G.typein("What should change?", "make it shorter")
ask = [e for e in w().descendants(control_type="Button") if "Ask" in (e.element_info.name or "")][0]
t0 = time.time(); ask.click_input()
def dlg_texts():
    return [e.element_info.name for e in w().descendants(control_type="Text") if e.element_info.name and any(k in e.element_info.name for k in ("Waiting", "waiting", "s ", "sec", "minute", "Claude", "Cancel", "Nothing", "reply", "couldn"))][:8]
for i in range(4):
    time.sleep(1.5); log(f"t+{time.time()-t0:.1f}s", dlg_texts(), [b.element_info.name for b in w().descendants(control_type="Button") if b.element_info.name in ("Cancel", "Stop", "Cancel request", "Stop waiting")])
    if i == 1: shot("grp_g4_waiting.png")
time.sleep(3); log(f"after reply t+{time.time()-t0:.1f}s", dlg_texts()); shot("grp_g4_badreply.png")
log("reply box:", repr(G.val("Claude's reply"))[:120])
