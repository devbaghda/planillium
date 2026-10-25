from l6r4 import focused, tx
from b1b_t import *
def line(): return val("New payable tag"), val(NEW)
def go(label,focus_in,target):
    nm=edit("New payable tag"); nm.click_input(); send_keys("^a"); send_keys("qq"); send_keys("{TAB}"); send_keys("^a"); send_keys("55"); time.sleep(.3)
    (edit("New payable tag") if focus_in=="name" else edit(NEW)).click_input(); time.sleep(.3)
    s0=val(SAL)
    r=tx(target).rectangle(); mouse.click(coords=(r.left+15,(r.top+r.bottom)//2)); time.sleep(.6)
    f=focused(); send_keys("9"); time.sleep(.5)
    log(label,"| line",line(),"| focus",f,"| salary",repr(s0),"->",repr(val(SAL)),"cfg",cfg()[0])
for fi in ("name","amount"):
  for tg in ("Income","Paid work"):
    go(f"focus in {fi}, click {tg}",fi,tg)
