import sys
sys.argv = ["x"]
src = open(__file__.replace("dbg_b1c.py", "run_b1c.py"), encoding="utf-8").read()
head = src[:src.index("make_root(); p = launch()")]
exec(head)
make_root(); p = launch()
def st(w, tag):
    print(tag, "| boxes:", [(e.element_info.name[:12] if e.element_info.name else e.element_info.automation_id, val(e)) for e in edits(w)], "| hints", hints(w), "| focus", focused(w), "| status", fe_text(w, "SaveStatus"), flush=True)
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception as ex: print("no welcome", ex)
    time.sleep(1); w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1.5); w = win(p)
    st(w, "start")
    def add_line(name, rate, how):
        n = fe(w, "NewPaidTagName"); r = fe(w, "NewPaidTagRate")
        cc(n, name); r.click_input(); send_keys("^a"); send_keys("{BACKSPACE}"); typ(rate)
        if how == "name": n.click_input(); send_keys("{ENTER}")
        elif how == "rate": send_keys("{ENTER}")
        time.sleep(1.3)
    add_line("zed", "500", "name"); st(w, "after add1")
    add_line("zed2", "500", "rate"); st(w, "after add2")
    add_line("zed3", "500", "button") if False else None
    cc(fe(w, "NewPaidTagName"), "zed3"); r=fe(w, "NewPaidTagRate"); r.click_input(); send_keys("^a"); send_keys("{BACKSPACE}"); typ("500")
    [b for b in w.descendants(control_type="Button") if b.element_info.name == "Add"][0].click_input(); time.sleep(1.3); st(w, "after add3")
    print("rects", [(e.element_info.name[:8], e.rectangle().top, e.rectangle().bottom) for e in edits(w)], "win", w.rectangle())
    shot(w, "dbg_after_adds.png")
    def R_(tag): print("  rects", tag, [(e.element_info.name[:8], e.rectangle().top) for e in edits(w)], flush=True)
    R_("start")
    key = SAL
    cc(fe(w, SAL), "-500"); send_keys("{TAB}"); time.sleep(1); st(w, "sal -500 tab"); R_("a")
    fe(w, SAL).click_input(); send_keys("{HOME}"); send_keys("{DELETE}"); send_keys("{ENTER}"); time.sleep(1); st(w, "sal fixed"); R_("b")
    cc(fe(w, SAL), "-8"); time.sleep(.5); bx, by = blank(w); mouse.click(coords=(bx, by)); time.sleep(1.0); st(w, "sal -8 blank"); R_("c")
    cc(fe(w, SAL), "1234"); send_keys("{ENTER}"); time.sleep(1.0); st(w, "sal 1234"); R_("d")
    cc(fe(w, TAG), "-500"); time.sleep(.8); st(w, "tag typed -500"); R_("e"); shot(w, "dbg_tag2.png")
finally:
    kill(p)
