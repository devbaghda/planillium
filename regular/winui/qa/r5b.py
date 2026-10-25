from l6r4 import focused, tx
from b1b_t import *
def line(): return val("New payable tag"), val(NEW)
def fill():
    w.set_focus(); time.sleep(.3)
    nm=edit("New payable tag"); nm.click_input(); send_keys("^a"); send_keys("qq"); send_keys("{TAB}"); send_keys("^a"); send_keys("55"); time.sleep(.4)
R=w.rectangle(); log("win",R)
log("== B1 new-tag line (window-relative coords)")
fill(); log("filled",line(),"focus",focused())
edit("New payable tag").click_input(); time.sleep(.5); log("click inside line (name):",line())
edit(NEW).click_input(); time.sleep(.5); log("click inside line (amount):",line())
r=tx("Income").rectangle(); mouse.click(coords=(r.left+15,(r.top+r.bottom)//2)); time.sleep(.6); log("click heading:",line(),"focus",focused())
fill(); mouse.click(coords=(R.left+700,R.top+400)); time.sleep(.6); log("click caption row inside page:",line(),"focus",focused())
fill(); mouse.click(coords=(R.left+1040,R.top+268)); time.sleep(.6); log("click right section-list area (Income card in section list):",line(),"focus",focused())
w.child_window(title="Income",control_type="ListItem").click_input(); time.sleep(1.5)
fill(); send_keys("{ESC}"); time.sleep(.6); log("Esc:",line(),"focus",focused())
log("cfg",cfg())
