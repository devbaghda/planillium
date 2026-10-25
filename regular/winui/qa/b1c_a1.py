from h import *
goto()
log("start",snap(),"focus",foc())
log([ (e.element_info.name) for e in edits()])
# L1
typein(SAL,"3000"); log("typed3000 cfg",cfg()[0])
send_keys("{ENTER}"); time.sleep(1)
log("L1 after Enter cfg",cfg()[0],"focus",foc())
b=snap(); send_keys("9"); time.sleep(.6); log("L1 type 9 same?",b==snap(),snap(),"focus",foc())
# L2
typein(TAG,"2000"); send_keys("{ENTER}"); time.sleep(1)
log("L2 cfg",cfg()[1],"focus",foc())
b=snap(); send_keys("9"); time.sleep(.6); log("L2 type 9 same?",b==snap(),"focus",foc())
# L3
typein(SAL,"3100"); send_keys("{TAB}"); time.sleep(1)
log("L3 cfg",cfg()[0],"focus after Tab",foc())
send_keys("{TAB}"); time.sleep(.5); log("   second Tab focus",foc())
# L4 blank space
typein(SAL,"3200")
r=w.rectangle(); mouse_x=r.right-60; 
from pywinauto import mouse
# find blank spot: right of the salary box
se=edit(SAL).rectangle(); mouse.click(coords=(se.right+250,se.top+10)); time.sleep(1)
log("L4 blank click cfg",cfg()[0],"focus",foc())
# click another box
typein(SAL,"3300"); edit(TAG).click_input(); time.sleep(1)
log("L4 other box cfg",cfg()[0],"focus",foc())
