from b1d_h import *
w().set_focus(); time.sleep(1); goto()
log("cfg",cfg(),"focus",foc())
for e in w().descendants():
    try:
        ct=e.element_info.control_type; n=e.element_info.name; r=e.rectangle()
        if ct in ("Edit","Button","Text","CheckBox","ListItem","Hyperlink","Custom") and n is not None:
            log(ct,"|",repr(n)[:110],"|",e.element_info.automation_id,"|",r)
    except Exception as ex: pass
shot("b1d_start.png")
