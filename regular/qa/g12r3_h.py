import g13_h
g13_h.S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\g12r3"
g13_h.PID=int(open(g13_h.S+r"\pid.txt").read())
from g13_h import *
import json
from pywinauto.uia_defines import IUIA
CFG=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\mroot-g12r3\config.json"
def cfg():
    try: return json.load(open(CFG,encoding="utf-8"))
    except Exception as x: return {"err":str(x)}
def under(pt):
    import comtypes.gen.UIAutomationClient as C; e=IUIA().iuia.ElementFromPoint(C.tagPOINT(*pt)); return f"{e.CurrentControlType}|{e.CurrentName}|{e.CurrentAutomationId}"
def summ(): return [t for t in texts() if t.startswith("No key") or t.startswith("Key saved") or t.startswith("A key is saved")]
def page(name):
    find(name="Settings", ct="ListItem").iface_selection_item.Select(); time.sleep(1.2)
    find(name=name, ct="ListItem").iface_selection_item.Select(); time.sleep(1.2)
