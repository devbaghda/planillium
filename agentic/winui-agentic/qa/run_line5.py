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
    ed = lambda sub: [x for x in w.descendants(control_type="Edit") if x.element_info.name.startswith(sub)][0]
    before = cfg()
    ed("New payable tag").click_input(); send_keys("QaTag")
    r = ed("Monthly net"); r.click_input(); send_keys("^a{BACKSPACE}700"); time.sleep(1)
    print("5a typed 700 no leave: cfg unchanged:", cfg() == before, "box", repr(r.get_value()))
    send_keys("{ENTER}"); time.sleep(1)
    print("5b Enter in rate box: cfg unchanged (no add):", cfg() == before, "box", repr(r.get_value()), "tags", tagrate())
    send_keys("{TAB}"); time.sleep(.6)
    print("5c after Tab: cfg unchanged:", cfg() == before, "box", repr(ed("Monthly net").get_value()))
    [b for b in w.descendants(control_type="Button") if b.element_info.name == "Add"][0].click_input(); time.sleep(1.2)
    print("5d after Add: paid_tags", cfg().get("paid_tags"))
    # app close while focused
    sal = [x for x in w.descendants(control_type="Edit") if "salary" in x.element_info.name][0]
    sal.click_input(); send_keys("^a{BACKSPACE}4444"); time.sleep(.5)
    w.child_window(auto_id="Close", control_type="Button").click_input(); time.sleep(2)
    print("close while focused: cfg salary", salary(), "alive", p.poll() is None)
finally: kill(p)
