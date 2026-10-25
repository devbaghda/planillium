import os,sys,time,json
from pywinauto import Application
from pywinauto.keyboard import send_keys
from pywinauto.uia_defines import IUIA
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\cf7cbaac-b682-4ea0-9f95-3643c4f351b6\scratchpad"
CFG=S+r"\root3\config.json"
PID=int(open(S+r"\b1c\pid.txt").read())
w=Application(backend="uia").connect(process=PID).top_window()
def cfg():
    c=json.load(open(CFG,encoding="utf-8")); return c["income"]["monthly_net_eur"], {t["tag"]:t["monthly_net_eur"] for t in c.get("paid_tags",[])}
def edits(): return [e for e in w.descendants(control_type="Edit")]
def edit(title):
    for e in w.descendants(control_type="Edit"):
        if e.element_info.name==title: return e
    raise Exception("no edit "+title)
def val(t): return edit(t).get_value()
def foc():
    try:
        f=IUIA().get_focused_element()
        return (str(f.CurrentControlType), f.CurrentName, f.CurrentAutomationId)
    except Exception as ex: return ("err",str(ex),"")
def msgs():
    out=[]
    for e in w.descendants():
        try:
            n=e.element_info.name
            if n and "negative" in n and e.is_visible(): out.append((e.element_info.control_type,n,e.rectangle().top))
        except Exception: pass
    return out
SAL="Previous salary — monthly net (€)"; TAG="Studioshoo — monthly net (€)"; NEW="Monthly net (€)"; NAME="New payable tag"
def typein(t,s):
    e=edit(t); e.click_input(); send_keys("^a"); send_keys(s,with_spaces=True); time.sleep(.5)
def log(*a): print(*a,flush=True)
def snap(): return cfg(),[val(t) for t in (SAL,TAG,NEW)]
def goto():
    w.child_window(auto_id="SettingsItem").click_input(); time.sleep(1.5)
    w.child_window(title="Income",control_type="ListItem").click_input(); time.sleep(1.5)
