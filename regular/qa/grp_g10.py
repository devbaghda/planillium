from grp_h import *
import grp_h as G
G.attach_pid(); w().set_focus()
base = set(e.element_info.name for e in w().descendants(control_type="Text"))
def newt(): return [e.element_info.name for e in w().descendants(control_type="Text") if e.element_info.name and e.element_info.name not in base]
[b for b in w().descendants(control_type="Button") if b.element_info.name == "Ask Claude"][0].click_input(); time.sleep(4)
log("compact reply ->", newt()[:6]); log("reply box:", repr(G.val("Claude's reply"))[:200]); shot("grp_g10_compact.png")
