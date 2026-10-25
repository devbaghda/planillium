import sys
from b1b_t import *
def heading():
    for e in w.descendants(control_type="Text"):
        if e.element_info.name=="Income" and e.rectangle().width()>300 and e.rectangle().top<700: return e
w.child_window(auto_id="SettingsItem").click_input(); time.sleep(1.5)
w.child_window(title="Income",control_type="ListItem").click_input(); time.sleep(2)
log("start",cfg(), [val(t) for t in (SAL,TAG,NEW)])
for t_,g,b in ((SAL,lambda:cfg()[0],2925),(TAG,lambda:cfg()[1]["Studioshoo"],1600)):
    run_box(t_,g,b,t_)
# L5 new tag
log("== L5")
n0=len(cfg()[1])
typein(NEW,"777"); log("typing: tags",len(cfg()[1]),"box",repr(val(NEW)))
send_keys("{TAB}"); time.sleep(.8); log("after Tab tags",cfg()[1])
typein(NEW,"778"); send_keys("{ENTER}"); time.sleep(.8); log("after Enter tags",cfg()[1],"box",repr(val(NEW)))
nm=edit("New payable tag"); nm.click_input(); send_keys("Zed"); time.sleep(.3)
[b for b in w.descendants(control_type="Button") if b.element_info.name=="Add"][0].click_input(); time.sleep(1.2)
log("after Add",cfg()[1])
# L6
log("== L6")
before=(cfg(),[val(t) for t in (SAL,TAG,NEW)])
edit(SAL).click_input(); time.sleep(.3)
heading().click_input(); time.sleep(.5)
send_keys("9"); time.sleep(.8)
after=(cfg(),[val(t) for t in (SAL,TAG,NEW)])
log("before",before); log("after ",after,"same" if before==after else "CHANGED")
# also from the tag box and new box
edit(TAG).click_input(); heading().click_input(); send_keys("9"); time.sleep(.6)
log("tagbox heading:",cfg(),[val(t) for t in (SAL,TAG,NEW)])
edit(NEW).click_input(); heading().click_input(); send_keys("9"); time.sleep(.6)
log("newbox heading:",cfg(),[val(t) for t in (SAL,TAG,NEW)])
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
