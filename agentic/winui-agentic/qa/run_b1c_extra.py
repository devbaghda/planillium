src = open(__file__.replace("run_b1c_extra.py", "run_b1c.py"), encoding="utf-8").read()
exec(src[:src.index("make_root(); p = launch()")])
make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception as ex: print("no welcome", ex)
    time.sleep(1); w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1.5); w = win(p)
    base = salary()
    cc(fe(w, SAL), "-"); time.sleep(.6)
    rec("X1-lone-minus", val(fe(w, SAL)) == "-" and len(hints(w)) == 1 and salary() == base, "box=%r hints=%d cfg=%r" % (val(fe(w, SAL)), len(hints(w)), salary()))
    send_keys("{ENTER}"); time.sleep(1)
    rec("X2-enter-invalid-keeps-focus", any(SAL in x for x in focused(w)) and salary() == base and len(hints(w)) == 1, "focus=%r cfg=%r hints=%d" % (focused(w), salary(), len(hints(w))))
    typ("0"); time.sleep(.5)
    rec("X3-minus-zero", val(fe(w, SAL)) == "-0" and len(hints(w)) == 1, "box=%r hints=%d cfg=%r" % (val(fe(w, SAL)), len(hints(w)), salary()))
    send_keys("^a"); send_keys("{BACKSPACE}"); time.sleep(.5)
    rec("X4-empty-no-message", len(hints(w)) == 0, "box=%r hints=%d" % (val(fe(w, SAL)), len(hints(w))))
    typ("5"); typ("-"); typ("0"); time.sleep(.6)
    rec("X5-mid-minus", val(fe(w, SAL)) == "-50" and len(hints(w)) == 1, "typed 5-0 -> box=%r hints=%d" % (val(fe(w, SAL)), len(hints(w))))
    send_keys("^a"); send_keys("{BACKSPACE}"); typ("−5"); time.sleep(.6)
    rec("X6-unicode-minus", val(fe(w, SAL)) == "-5" and len(hints(w)) == 1, "typed U+2212 5 -> box=%r hints=%d" % (val(fe(w, SAL)), len(hints(w))))
    print("readout:", [t.element_info.name for t in w.descendants(control_type="Text") if "/h" in (t.element_info.name or "")][:3])
    # invalid box + section switch: nothing saved, no crash
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "General": li.click_input(); break
    time.sleep(1.2)
    rec("X7-section-switch-invalid", salary() == base and p.poll() is None, "cfg=%r alive=%s" % (salary(), p.poll() is None))
    print("log tail:")
    import glob
    for f in glob.glob(ROOT + "/data/*.log"):
        print(f); print("".join(open(f, encoding="utf-8", errors="replace").readlines()[-12:]))
finally:
    kill(p)
print("SUMMARY"); [print(r) for r in R]
