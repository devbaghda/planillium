from l6r4 import focused, tx
from b1b_t import *
# L7
log("== L7")
for t_ in (SAL,TAG,NEW):
    c0=cfg()
    edit(t_).click_input(); send_keys("^a"); send_keys("{BACKSPACE}"); send_keys("-"); time.sleep(.6)
    log(t_,"after '-': box",repr(val(t_)),"hints",hints())
    send_keys("5"); time.sleep(.5)
    log("  after '5': box",repr(val(t_)),"hints",hints())
    send_keys("-"); time.sleep(.5); log("  after '-' again: box",repr(val(t_)),"hints",hints())
    send_keys("{TAB}"); time.sleep(.8); log("  after Tab: hints",hints(),"box",repr(val(t_)),"cfg changed" if cfg()!=c0 else "cfg same")
    # restore
# L8
log("== L8")
for t_,g in ((SAL,lambda:cfg()[0]),(TAG,lambda:cfg()[1]["Studioshoo"])):
    typein(t_,"4000"); send_keys("{TAB}"); time.sleep(.8); base=g(); log(t_,"committed",base)
    edit(t_).click_input(); send_keys("^a{BACKSPACE}"); time.sleep(.4); send_keys("{TAB}"); time.sleep(.8)
    log("  empty+Tab cfg",g(),"box",repr(val(t_)))
    edit(t_).click_input(); send_keys("^a"); send_keys("abc"); time.sleep(.4); send_keys("{TAB}"); time.sleep(.8)
    log("  abc+Tab cfg",g(),"box",repr(val(t_)))
    edit(t_).click_input(); send_keys("^a"); send_keys("."); time.sleep(.4); send_keys("{TAB}"); time.sleep(.8)
    log("  dot+Tab cfg",g(),"box",repr(val(t_)))
    edit(t_).click_input(); send_keys("^a{BACKSPACE}{ENTER}"); time.sleep(.8)
    log("  empty+Enter cfg",g(),"box",repr(val(t_)))
log("alive", w.exists())
# L9
log("== L9 buttons near boxes:",[ (b.element_info.name) for b in w.descendants(control_type="Button") if 'Spin' in b.element_info.name or 'Increase' in b.element_info.name or 'Decrease' in b.element_info.name])
w.capture_as_image().save("income.png")
log("== B1 new-tag line")
def line(): return val("New payable tag"), val(NEW)
def fill():
    nm=edit("New payable tag"); nm.click_input(); send_keys("^a"); send_keys("qq"); send_keys("{TAB}"); send_keys("^a"); send_keys("55"); time.sleep(.4)
def reset():
    edit(SAL).click_input()
fill(); log("filled",line(),"focus",focused())
# click inside line (name box) must not clear
edit("New payable tag").click_input(); time.sleep(.5); log("click inside line:",line())
r=tx("Income").rectangle(); mouse.click(coords=(r.left+15,(r.top+r.bottom)//2)); time.sleep(.6); log("click heading:",line(),"focus",focused())
fill(); mouse.click(coords=(1000,600)); time.sleep(.6); log("click caption/blank (1000,600):",line(),"focus",focused())
fill(); mouse.click(coords=(1250,330)); time.sleep(.6); log("click summary area (1250,330):",line(),"focus",focused())
fill(); send_keys("{ESC}"); time.sleep(.6); log("Esc:",line(),"focus",focused())
log("tags cfg",cfg())
