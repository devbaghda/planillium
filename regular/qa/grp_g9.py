from grp_h import *
import grp_h as G, re
G.attach(); w().set_focus()
CAP = "Monthly AI cap in euro"
def capcfg():
    t = open(H.CFG, encoding="utf-8").read(); m = re.search(r'"cap_eur"\s*:\s*([^,}\s]+)', t); return m.group(1) if m else None
def st():
    out = {}
    for e in w().descendants(control_type="Text"):
        a = e.element_info.automation_id
        if a in ("AiResult", "AiSpendText", "SaveStatus") or (e.element_info.name and any(k in e.element_info.name for k in ("cap", "Cap")) and a not in ("AiSpendText",)):
            out[a or e.element_info.name[:30]] = e.element_info.name
    return out
log("initial box", repr(G.val(CAP)), "cfg", capcfg())
for label, s in (("empty",""),("spaces","   "),("text","abc"),("minus","-5"),("long","9"*30),("zero","0")):
    G.typein(CAP, s); byname("Save", "Button").click_input(); time.sleep(1.2)
    log(f"{label}+Save: box={G.val(CAP)!r} cfg={capcfg()} st={st()} focus={G.foc()[1:]}")
for key in ("{TAB}", "{ENTER}", "{ESC}"):
    G.typein(CAP, "-5"); send_keys(key); time.sleep(1.2)
    log(f"-5 {key}: box={G.val(CAP)!r} cfg={capcfg()} st={st()} focus={G.foc()[1:]}")
G.typein(CAP, "-5"); r = w().rectangle(); mouse.click(coords=(r.left+1300, r.top+800)); time.sleep(1.2)
log(f"-5 blankclick: box={G.val(CAP)!r} cfg={capcfg()} st={st()} focus={G.foc()[1:]}")
G.typein(CAP, "3"); byname("Save", "Button").click_input(); time.sleep(1.2)
log(f"3+Save: box={G.val(CAP)!r} cfg={capcfg()} st={st()}")
shot("grp_g9_cap.png")
