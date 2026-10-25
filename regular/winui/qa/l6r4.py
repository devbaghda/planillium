from b1b_t import *
from pywinauto.uia_defines import IUIA
def focused():
    el=IUIA().iuia.GetFocusedElement(); return (el.CurrentName, el.CurrentClassName, el.CurrentAutomationId)
def tx(prefix):
    best=None
    for e in w.descendants(control_type="Text"):
        r=e.rectangle()
        if e.element_info.name.startswith(prefix) and r.width()>300 and r.top<700: best=e
    return best
BOXES=(SAL,TAG,"Zed — monthly net (€)",NEW)
def setup():
    for b in BOXES[:3]:
        e=edit(b); e.click_input(); send_keys("^a"); send_keys("5000"); send_keys("{TAB}"); time.sleep(.5)
if __name__=="__main__":
    for hn in ("Income","Paid work","Diary time carrying"):
      for box in BOXES:
        setup(); edit(box).click_input(); time.sleep(.4); f0=focused()
        t=tx(hn); r=t.rectangle(); pt=(r.left+15,(r.top+r.bottom)//2); mouse.click(coords=pt); time.sleep(.5); f1=focused()
        before=(cfg(),[val(b) for b in BOXES]); send_keys("9"); time.sleep(.6)
        after=(cfg(),[val(b) for b in BOXES]); log(hn,r,f0,"->",f1,"same" if before==after else "CHANGED")
