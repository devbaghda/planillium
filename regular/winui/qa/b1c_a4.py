from h import *
from pywinauto import mouse
w.set_focus()
def m(): return [x[2] for x in msgs()]
for t in (SAL,TAG,NEW):
    log("=====",t,"cfg",cfg())
    c0=cfg()
    e=edit(t); e.click_input(); send_keys("^a{BACKSPACE}"); send_keys("-500"); time.sleep(.7)
    log("L7 box",repr(val(t)),"msgs",m(),"cfg same",cfg()==c0)
    if t==SAL: w.capture_as_image().save("neg_sal.png")
    send_keys("123"); time.sleep(.6)
    log("L8 box",repr(val(t)),"msgs",m(),"cfg same",cfg()==c0)
    send_keys("{HOME}{DELETE}"); time.sleep(.7)
    log("L9 box",repr(val(t)),"msgs",m())
    # re-add minus, then Tab
    send_keys("{HOME}-"); time.sleep(.6)
    log("   re-minus",repr(val(t)),m())
    send_keys("{TAB}"); time.sleep(1)
    log("L10 Tab: box",repr(val(t)),"msgs",m(),"cfg same",cfg()==c0,"focus",foc())
    # click elsewhere (blank)
    edit(t).click_input(); send_keys("{END}"); 
    r=edit(t).rectangle(); mouse.click(coords=(r.right+250,r.top+10)); time.sleep(1)
    log("L10 click-away: box",repr(val(t)),"msgs",m(),"cfg same",cfg()==c0,"focus",foc())
    # Enter with negative
    edit(t).click_input(); send_keys("{ENTER}"); time.sleep(1)
    log("   Enter w/ neg: box",repr(val(t)),"msgs",m(),"cfg same",cfg()==c0,"focus",foc())
    # restore valid: remove minus
    edit(t).click_input(); send_keys("{HOME}{DELETE}"); time.sleep(.5); send_keys("{ENTER}"); time.sleep(1)
    log("   restored box",repr(val(t)),"msgs",m(),"cfg",cfg())
