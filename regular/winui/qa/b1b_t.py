import os,sys,time,json
sys.path.insert(0,r"D:\CLAUDE\Planillium\regular\winui\qa")
os.environ["QPID"]=open("pid.txt").read()
from lib import *
from pywinauto.keyboard import send_keys
from pywinauto import mouse
CFG=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\cf7cbaac-b682-4ea0-9f95-3643c4f351b6\scratchpad\root\config.json"
def cfg():
    c=json.load(open(CFG,encoding="utf-8")); return c["income"]["monthly_net_eur"], {t["tag"]:t["monthly_net_eur"] for t in c.get("paid_tags",[])}
w=win()
def edit(title):
    for e in w.descendants(control_type="Edit"):
        if e.element_info.name==title: return e
    raise Exception("no edit "+title)
def val(title): return edit(title).get_value()
def hints(): return [n for d,ct,n,a in alltexts(16) if "negative" in (n or "")]
def typein(title,s,ctrl_a=True):
    e=edit(title); e.click_input()
    if ctrl_a: send_keys("^a")
    send_keys(s,with_spaces=True); time.sleep(0.6)
SAL="Previous salary — monthly net (€)"; TAG="Studioshoo — monthly net (€)"; NEW="Monthly net (€)"
def log(*a): print(*a, flush=True)
def sec(name):
    w.child_window(title=name if False else "Income",control_type="ListItem").click_input()
def run_box(title,getter,expect_before,label):
    log("==",label)
    typein(title,"3000")
    log("L1 typing, no leave: cfg=",getter(),"box=",val(title)," (expect cfg",expect_before,")")
    send_keys("{TAB}"); time.sleep(1)
    log("L2 after Tab: cfg=",getter(),"box=",repr(val(title)))
    typein(title,"3100"); send_keys("{ENTER}"); time.sleep(1)
    log("L3 after Enter: cfg=",getter(),"box=",repr(val(title)))
if __name__=="__main__":
    w.child_window(auto_id="SettingsItem").click_input(); time.sleep(1.2)
    w.child_window(title="Income",control_type="ListItem").click_input(); time.sleep(1.2)
    log("start cfg",cfg())
    run_box(SAL,lambda:cfg()[0],2925,"salary")
    run_box(TAG,lambda:cfg()[1]["Studioshoo"],1600,"tag")
