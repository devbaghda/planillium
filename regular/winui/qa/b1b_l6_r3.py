from b1b_t import *
def focused():
    from pywinauto.uia_defines import IUIA
    el=IUIA().iuia.GetFocusedElement(); return el.CurrentName, el.CurrentClassName
def tx(name):
    for e in w.descendants(control_type="Text"):
        r=e.rectangle()
        if e.element_info.name.startswith(name) and r.left==355 and r.top<500: return e
def setup():
    typein(SAL,"5000"); send_keys("{TAB}"); time.sleep(.5)
    typein(TAG,"2000"); send_keys("{TAB}"); time.sleep(.5)
for hn in ("Income","Paid work","Diary time carrying"):
  for box in (SAL,TAG,NEW):
    setup()
    edit(box).click_input(); time.sleep(.4); f0=focused()
    t=tx(hn); r=t.rectangle(); mouse.click(coords=(r.left+15,(r.top+r.bottom)//2)); time.sleep(.5); f1=focused()
    send_keys("9"); time.sleep(.6)
    log(hn,"| start",f0,"| click at",(r.left+15,(r.top+r.bottom)//2),"-> focus",f1,"| boxes",[val(t_) for t_ in (SAL,TAG,NEW)],"cfg",cfg())
