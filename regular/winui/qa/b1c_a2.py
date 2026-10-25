from h import *
log("start",snap(),foc())
def tags(): return cfg()[1]
# L5a Enter in name box
typein(NEW,"500"); typein(NAME,"zed"); send_keys("{ENTER}"); time.sleep(1.2)
log("L5a name-Enter tags",tags(),"focus",foc(),"fields",[val(t) for t in (NAME,NEW)] if False else "")
log("   name/amt boxes now:",repr(edit(NAME).get_value()),repr(edit(NEW).get_value()))
# L5b Enter in amount box
typein(NAME,"yan"); typein(NEW,"700"); send_keys("{ENTER}"); time.sleep(1.2)
log("L5b amount-Enter tags",tags(),"focus",foc())
log("   boxes:",repr(edit(NAME).get_value()),repr(edit(NEW).get_value()))
# Add button parity: compare entry structure
c=json.load(open(CFG,encoding="utf-8")); log("paid_tags raw",c["paid_tags"])
typein(NAME,"btn"); typein(NEW,"900")
[b for b in w.descendants(control_type="Button") if b.element_info.name=="Add"][0].click_input(); time.sleep(1.2)
c=json.load(open(CFG,encoding="utf-8")); log("after Add btn",c["paid_tags"],"focus",foc())
# L6 invalid
n0=len(tags())
typein(NAME,"bad"); typein(NEW,"-500"); send_keys("{ENTER}"); time.sleep(1)
log("L6 minus+Enter tags same",len(tags())==n0,"focus",foc(),"msgs",msgs(),"box",repr(val(NEW)))
typein(NEW,""); 
e=edit(NEW); e.click_input(); send_keys("^a{BACKSPACE}"); time.sleep(.4)
send_keys("{ENTER}"); time.sleep(1)
log("L6 empty+Enter tags same",len(tags())==n0,"focus",foc(),"msgs",msgs(),"name",repr(val(NAME)))
# name empty
e=edit(NAME); e.click_input(); send_keys("^a{BACKSPACE}"); typein(NEW,"500"); edit(NAME).click_input(); send_keys("{ENTER}"); time.sleep(1)
log("L6 noname+Enter tags same",len(tags())==n0,"focus",foc(),"msgs",msgs())
w.capture_as_image().save("l6.png")
