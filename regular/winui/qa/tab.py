from l6r4 import focused, tx
from b1b_t import *
import l6r4
def seq(keys,n):
    out=[]
    for _ in range(n):
        send_keys(keys); time.sleep(.35); out.append(focused()[0:3:2] if False else (focused()[0],focused()[2]))
    return out
def park():
    edit(SAL).click_input(); time.sleep(.3)
    r=tx("Income").rectangle(); mouse.click(coords=(r.left+15,(r.top+r.bottom)//2)); time.sleep(.5)
    return focused()
# baseline: no park ever happened this launch? (park happened earlier in launch; IsTabStop reset on LostFocus)
edit(SAL).click_input(); time.sleep(.3)
log("A baseline salary: Shift+Tab x2 ->",seq("+{TAB}",2))
edit(SAL).click_input(); time.sleep(.3)
log("A baseline salary: Tab x4 ->",seq("{TAB}",4))
log("B park:",park())
log("B Tab x4 from park ->",seq("{TAB}",4))
log("B2 park:",park())
log("B2 Shift+Tab x2 from park ->",seq("+{TAB}",2))
# after park left, shift-tab from salary equals baseline?
log("C park:",park()); send_keys("{TAB}"); time.sleep(.3); log("C Tab ->",focused()); 
log("C then Shift+Tab x2 ->",seq("+{TAB}",2))
log("C then Tab x2 ->",seq("{TAB}",2))
# park again immediately ok (repeatable)
log("D second park:",park()); log("D third park:",park())
