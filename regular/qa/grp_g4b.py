from grp_h import *
import grp_h as G, re
G.attach_pid(); w().set_focus()
base = set(e.element_info.name for e in w().descendants(control_type="Text"))
def newt(): return [e.element_info.name for e in w().descendants(control_type="Text") if e.element_info.name and e.element_info.name not in base]
def btn(n): return [b for b in w().descendants(control_type="Button") if b.element_info.name == n]
t0 = time.time(); G.click("Accept and ask Claude", "Button", wait=0.3)
for i in range(3):
    time.sleep(1.5); log(f"t+{time.time()-t0:.1f}s new texts={newt()} buttons={[b.element_info.name for b in w().descendants(control_type='Button') if b.element_info.name in ('Ask Claude','Cancel','Stop','Cancel request','Preview')]}")
    if i == 1: shot("grp_g4_waiting.png")
time.sleep(4.5); log(f"after reply t+{time.time()-t0:.1f}s new={newt()} reply={G.val(chr(67)+'laude' + chr(39) + 's reply')[:80]!r}"); shot("grp_g4_badreply.png")
# trial 2: long wait, cancel
open(G.S + r"\stub_delay.txt", "w").write("60")
ask = btn("Ask Claude"); log("Ask button present again:", bool(ask))
if ask:
    t0 = time.time(); ask[0].click_input(); time.sleep(3); log(f"waiting t+{time.time()-t0:.1f}s new={newt()}")
    cands = [b for b in w().descendants(control_type="Button") if b.element_info.name in ("Cancel request", "Stop", "Stop waiting", "Cancel")]
    log("cancel candidates", [(b.element_info.name, b.element_info.automation_id) for b in cands])
    c = [b for b in cands if b.element_info.automation_id != "CloseButton"] or cands
    tc = time.time(); c[0].click_input()
    for k in range(6):
        time.sleep(0.4); nt = newt(); a = btn("Ask Claude")
        log(f"  cancel+{time.time()-tc:.1f}s new={nt} ask_enabled={a[0].is_enabled() if a else None}")
    shot("grp_g4_cancelled.png")
log("dialog still open:", bool(btn("Preview")))
