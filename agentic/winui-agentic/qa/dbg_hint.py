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
    e = [x for x in w.descendants(control_type="Edit") if "salary" in x.element_info.name][0]
    e.click_input(); send_keys("5"); send_keys("-"); time.sleep(1)
    for x in w.descendants():
        try:
            n = x.element_info.name or ""
            if "egative" in n or "mounts" in n:
                print("FOUND", x.element_info.control_type, repr(n), x.rectangle(), x.is_visible(), x.element_info.visible, x.element_info.class_name)
        except Exception as ex: print("err", ex)
    print("all texts near:")
    for x in w.descendants(control_type="Text"):
        r = x.rectangle()
        print(repr(x.element_info.name)[:60], r)
        if r.top > 450: break
    w.capture_as_image().save(r"D:\CLAUDE\Planillium\agentic\winui-agentic\qa\hint_shot.png")
finally: kill(p)
