from b1b_t import *
import comtypes.client
def focused():
    try:
        from pywinauto.uia_defines import IUIA
        el=IUIA().iuia.GetFocusedElement()
        return el.CurrentName, el.CurrentControlType, el.CurrentClassName
    except Exception as ex: return repr(ex)
def tx(name):
    for e in w.descendants(control_type="Text"):
        r=e.rectangle()
        if e.element_info.name==name and 400<r.left<1100: return e
def setup():
    typein(SAL,"5000"); send_keys("{TAB}"); time.sleep(.5)
    typein(TAG,"2000"); send_keys("{TAB}"); time.sleep(.5)
for hn in ("Income","Paid work"):
  for box in (SAL,TAG):
    setup()
    log("fixture",cfg(),[val(t) for t in (SAL,TAG,NEW)])
    edit(box).click_input(); time.sleep(.4); log(" focus in box:",focused())
    tx(hn).click_input(); time.sleep(.5); log(" focus after click on",hn,":",focused())
    send_keys("9"); time.sleep(.6)
    log(" after typing 9: boxes",[val(t) for t in (SAL,TAG,NEW)],"cfg",cfg())
