from h import *
def texts():
    r=edit(NEW).rectangle()
    out=[]
    for e in w.descendants(control_type="Text"):
        try:
            rr=e.rectangle()
            if abs(rr.top-r.top)<120 and e.is_visible(): out.append((e.element_info.name,rr.top))
        except: pass
    return out
e=edit(NEW); e.click_input(); send_keys("^a{BACKSPACE}"); edit(NAME).click_input(); send_keys("^a"); send_keys("emp"); 
edit(NEW).click_input(); send_keys("^a{BACKSPACE}{ENTER}"); time.sleep(1)
log("empty amt Enter texts near:",texts(),"focus",foc(), "tags",cfg()[1].keys())
# Add button with empty
edit(NAME).click_input(); send_keys("^a"); send_keys("emp2")
[b for b in w.descendants(control_type="Button") if b.element_info.name=="Add"][0].click_input(); time.sleep(1)
log("Add btn empty texts:",texts(),"tags",list(cfg()[1].keys()))
