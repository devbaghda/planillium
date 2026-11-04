from common import *
make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception: pass
    w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1)
    n = [x for x in w.descendants(control_type="Edit") if x.element_info.name == "New payable tag"][0]
    n.click_input(); send_keys("a-b"); time.sleep(.5); print("name box:", n.get_value())
    e = [x for x in w.descendants(control_type="Edit") if "salary" in x.element_info.name][0]
    e.click_input(); send_keys("^a{BACKSPACE}7"); send_keys("{VK_SUBTRACT}"); time.sleep(.8)
    print("after VK_SUBTRACT box", e.get_value())
    send_keys("-"); time.sleep(.8)
    print("after OEM_MINUS box", e.get_value())
    for x in w.descendants():
        try:
            nm = x.element_info.name or ""
            if "egative" in nm: print("FOUND", x.element_info.control_type, repr(nm), x.rectangle())
        except Exception: pass
    w.capture_as_image().save(r"D:\CLAUDE\Planillium\agentic\winui-agentic\qa\hint_shot2.png")
finally: kill(p)
