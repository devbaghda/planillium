from common import *
R = []   # (line, result, evidence)
def rec(line, ok, ev):
    R.append((line, "PASS" if ok else "FAIL", ev)); print(("%s: %s -- %s" % (line, "PASS" if ok else "FAIL", ev)).encode("ascii","replace").decode(), flush=True)

def edits(w): return [e for e in w.descendants(control_type="Edit")]
def find_edit(w, sub):
    for e in edits(w):
        if sub in (e.element_info.name or ""): return e
    return None
def texts(w): return [t for t in w.descendants(control_type="Text")]
def hint_visible(w):
    for t in texts(w):
        try:
            if "negative" in (t.element_info.name or "") and t.rectangle().width() > 0: return True
        except Exception: pass
    return False
def val(e):
    try: return e.get_value()
    except Exception: return e.window_text()
def nums(s): return s.replace(" ","").replace(" ","").replace(" ","").replace(" ","")
def click(e): e.click_input()
def typ(s): send_keys(s, with_spaces=True, pause=0.05)
def clear_and_type(e, s):
    click(e); time.sleep(.2); send_keys("^a"); send_keys("{BACKSPACE}"); typ(s)
def focus_heading(w, name):
    cands = [t for t in texts(w) if (t.element_info.name == name) and t.rectangle().width() > 0]
    cands.sort(key=lambda t: -t.rectangle().left)
    return cands[0]
def go_section(w, name):
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == name: li.click_input(); break
    time.sleep(1)
SAL = "Previous salary"; TAG = "Studioshoo"

make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception as ex: print("no welcome", ex)
    w = win(p); go_section(w, "Income")
    sal = find_edit(w, SAL); tag = find_edit(w, TAG)
    print("edits", [e.element_info.name for e in edits(w)])
    print("initial salary", salary(), "tag", tagrate(), "box:", repr(val(sal)), repr(val(tag)))
    s0 = salary()

    clear_and_type(sal, "3000"); time.sleep(1.0)
    rec("1", salary() == s0 and nums(val(sal)) == "3000", "typed 3000, box=%r config=%r" % (val(sal), salary()))
    send_keys("{TAB}"); time.sleep(1.0)
    rec("2", salary() == 3000 and val(find_edit(w, SAL)) == "3 000" or val(find_edit(w, SAL)) in ("3 000", "3 000", "3 000"), "after Tab config=%r box=%r" % (salary(), val(find_edit(w, SAL))))
    clear_and_type(find_edit(w, SAL), "3100"); send_keys("{ENTER}"); time.sleep(1.0)
    rec("3", salary() == 3100, "after Enter config=%r box=%r" % (salary(), val(find_edit(w, SAL))))
    m1 = os.path.getmtime(CFG); send_keys("{TAB}"); time.sleep(1.0)
    rec("X-enter-tab-once", os.path.getmtime(CFG) == m1, "config mtime unchanged by Tab after Enter")

    t0 = tagrate()[0]
    clear_and_type(find_edit(w, TAG), "1700"); time.sleep(1.0)
    a = tagrate()[0] == t0
    send_keys("{TAB}"); time.sleep(1.0)
    b = tagrate()[0] == 1700
    clear_and_type(find_edit(w, TAG), "1800"); send_keys("{ENTER}"); time.sleep(1.0)
    c = tagrate()[0] == 1800
    rec("4", a and b and c, "typed:unchanged=%s Tab:1700=%s Enter:1800=%s cfg=%r" % (a, b, c, tagrate()))

    # line 5: new-tag amount box
    ne = [e for e in edits(w) if "Studioshoo" not in (e.element_info.name or "") and "salary" not in (e.element_info.name or "").lower()]
    print("other edits", [e.element_info.name for e in ne])

    # 6 heading click
    c_before = cfg()
    click(find_edit(w, SAL)); time.sleep(.3)
    click(focus_heading(w, "Income")); time.sleep(1.0); typ("9"); time.sleep(1.0)
    rec("6a", cfg() == c_before and nums(val(find_edit(w, SAL))) == "3100", "click salary, click Income heading, type 9: box=%r cfg unchanged=%s" % (val(find_edit(w, SAL)), cfg() == c_before))
    clear_and_type(find_edit(w, SAL), "3200")
    click(focus_heading(w, "Income")); time.sleep(1.0); typ("9"); time.sleep(1.0)
    rec("6c", salary() == 3200 and "9" not in val(find_edit(w, SAL)), "typed 3200, heading click, 9: config=%r box=%r" % (salary(), val(find_edit(w, SAL))))
    click(find_edit(w, TAG)); click(focus_heading(w, "Paid work")); time.sleep(1.0); typ("9"); time.sleep(1.0)
    rec("6d", tagrate()[0] == 1800 and nums(val(find_edit(w, TAG))) == "1800", "tag box click, Paid work heading, 9: cfg=%r box=%r" % (tagrate(), val(find_edit(w, TAG))))

    # 7 minus
    click(find_edit(w, SAL)); send_keys("^a"); send_keys("{BACKSPACE}"); typ("5"); typ("-"); time.sleep(.6)
    hv = hint_visible(w); bx = val(find_edit(w, SAL))
    typ("0"); time.sleep(.6); hv2 = hint_visible(w)
    typ("-"); time.sleep(.5); hv3 = hint_visible(w); send_keys("{TAB}"); time.sleep(.6); hv4 = hint_visible(w)
    rec("7-salary", hv and "-" not in bx and not hv2 and hv3 and not hv4, "hint after minus=%s box=%r; after digit hint=%s; minus again=%s; after leave=%s" % (hv, bx, hv2, hv3, hv4))
    click(find_edit(w, TAG)); send_keys("^a"); send_keys("{BACKSPACE}"); typ("-"); time.sleep(.6)
    hv = hint_visible(w); bx = val(find_edit(w, TAG)); typ("5"); time.sleep(.5); hv2 = hint_visible(w)
    rec("7-tag", hv and "-" not in bx and not hv2, "tag hint=%s box=%r digit->hint=%s" % (hv, bx, hv2))
    send_keys("{TAB}"); time.sleep(.5)
    print("cfg after minus tests", salary(), tagrate())

    # 8
    s_b, t_b = salary(), tagrate()
    click(find_edit(w, SAL)); send_keys("^a"); send_keys("{BACKSPACE}"); send_keys("{TAB}"); time.sleep(1.0)
    ok1 = salary() == s_b and p.poll() is None
    click(find_edit(w, SAL)); send_keys("^a"); typ("abc"); send_keys("{TAB}"); time.sleep(1.0)
    ok2 = salary() == s_b and p.poll() is None
    click(find_edit(w, TAG)); send_keys("^a"); send_keys("{BACKSPACE}"); send_keys("{TAB}"); time.sleep(1.0)
    ok3 = tagrate() == t_b and p.poll() is None
    rec("8", ok1 and ok2 and ok3, "empty salary kept=%s, letters kept=%s, empty tag kept=%s, alive=%s; boxes now %r / %r" % (ok1, ok2, ok3, p.poll() is None, val(find_edit(w, SAL)), val(find_edit(w, TAG))))

    # 9
    clear_and_type(find_edit(w, SAL), "14000"); send_keys("{TAB}"); time.sleep(1)
    spin = [b for b in w.descendants(control_type="Button") if "Increase" in (b.element_info.name or "") or "Decrease" in (b.element_info.name or "")]
    rec("9", nums(val(find_edit(w, SAL))) == "14000" and val(find_edit(w, SAL)) != "14000" and not spin, "box=%r spinner buttons=%d" % (val(find_edit(w, SAL)), len(spin)))
    print("salary cfg", salary())
    # extra: section switch commits
    clear_and_type(find_edit(w, SAL), "3333")
    go_section(w, "General"); time.sleep(1.2)
    rec("X-section-switch", salary() == 3333, "typed 3333, switched section: config=%r" % salary())
    go_section(w, "Income")
    clear_and_type(find_edit(w, TAG), "1999")
    rm = [b for b in w.descendants(control_type="Button") if "emove" in (b.element_info.name or "")]
    print("remove buttons", [b.element_info.name for b in rm])
    if rm: rm[0].click_input(); time.sleep(1.2)
    print("after remove cfg paid_tags:", cfg().get("paid_tags"), "alive", p.poll() is None)
    dump(w, depth=16)
finally:
    kill(p)
print("SUMMARY"); [print(r) for r in R]
