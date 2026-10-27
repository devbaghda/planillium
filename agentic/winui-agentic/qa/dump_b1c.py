from common import *
make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception as ex: print("no welcome", ex)
    time.sleep(1); w=win(p)
    w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1.5); w=win(p)
    for e in w.descendants():
        try:
            ct=e.element_info.control_type; n=e.element_info.name; r=e.rectangle()
            if ct in ("Edit","Button","Text","CheckBox","Group","ListItem") and r.width()>0: print(ct,"|",repr(n)[:70],"|",e.element_info.automation_id,"|",r)
        except Exception: pass
finally: kill(p)
