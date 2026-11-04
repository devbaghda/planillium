from b1d_lib import *
SAL = "Previous salary"; TAG = "Studioshoo"
def names(): return [p["tag"] for p in cfg().get("paid_tags", [])]
def top_scroll(w):
    r = w.rectangle()
    for _ in range(6):
        mouse.scroll(coords=(r.left + 500, r.top + 400), wheel_dist=5); time.sleep(.2)
def btn(w, name):
    bs = [b for b in w.descendants(control_type="Button") if b.element_info.name == name and b.rectangle().width() > 0]
    return bs[-1] if bs else None
def allmsgs(w): return [(n[:60], a) for n, a, _ in texts(w) if any(k in n.lower() for k in ("number", "negative", "more than", "saved", "name"))
                        and not n.startswith("Diary") and not n.startswith("No name set")]
p, w = start()
try:
    # A: Enter in an emptied box, clean state (no earlier add-line message)
    for key, label, getc, exp in ((SAL, "salary", salary, 2925), (TAG, "tag", lambda: tagrate()[0], 1600)):
        for txt in ("", "abc", "   "):
            top_scroll(w); cc(fe(w, key), txt); safe_keys("{ENTER}"); time.sleep(1.0)
            b = val(fe(w, key)); f = focused_any(w); m = allmsgs(w)
            shot(w, "b1d2_enter_%s_%s.png" % (label, "empty" if not txt.strip() and txt == "" else ("abc" if txt == "abc" else "spaces")))
            typ("7"); time.sleep(.6); b2 = val(fe(w, key))
            rec("A-enter-%s-%r" % (label, txt), nums(b) == str(exp) and getc() == exp and not [x for x in f if x[0] == "Edit"],
                "box after Enter=%r stored=%r focus=%r msgs=%r | then typed 7 -> box=%r stored=%r" % (b, getc(), f, m, b2, getc()))
            # now leave by Tab/click to see final state
            safe_keys("{TAB}"); time.sleep(.8)
            print("    after later Tab: box=%r stored=%r focus=%r" % (val(fe(w, key)), getc(), focused_any(w)), flush=True)
            top_scroll(w); cc(fe(w, key), str(exp)); safe_keys("{ENTER}"); time.sleep(.6)
    # B: refused Add then Esc / outside click
    top_scroll(w)
    cc(fe(w, "NewPaidTagName"), "asd"); cc(fe(w, "NewPaidTagRate"), ""); btn(w, "Add").click_input(); time.sleep(1.0)
    f = focused_any(w); n0 = val(fe(w, "NewPaidTagName"))
    safe_keys("{ESC}"); time.sleep(.8)
    n1 = val(fe(w, "NewPaidTagName")); m1 = allmsgs(w)
    bx, by = blank(w); sclick(bx, by); time.sleep(.8)
    n2 = val(fe(w, "NewPaidTagName")); m2 = allmsgs(w)
    rec("B-refused-add-then-esc/click", n1 == "" or n2 == "", "focus after refused Add click=%r name before=%r after Esc=%r msgs=%r | after blank click name=%r msgs=%r" % (f, n0, n1, m1, n2, m2))
    # B2: refused via Enter in name box (cursor stays) then Esc
    cc(fe(w, "NewPaidTagName"), "asd"); cc(fe(w, "NewPaidTagRate"), "abc"); fe(w, "NewPaidTagName").click_input(); safe_keys("{ENTER}"); time.sleep(.8)
    safe_keys("{ESC}"); time.sleep(.8)
    rec("B2-refused-enter-then-esc", val(fe(w, "NewPaidTagName")) == "" and not allmsgs(w), "name=%r amt=%r msgs=%r focus=%r" % (val(fe(w, "NewPaidTagName")), val(fe(w, "NewPaidTagRate")), allmsgs(w), focused_any(w)))
    # name missing, amount valid
    cc(fe(w, "NewPaidTagName"), ""); cc(fe(w, "NewPaidTagRate"), "100"); btn(w, "Add").click_input(); time.sleep(1.0)
    rec("B3-no-name", len(names()) == 1, "tags=%r msgs=%r" % (names(), allmsgs(w)))
    safe_keys("{ESC}"); time.sleep(.5); sclick(*blank(w)); time.sleep(.5)
    # C: line 5 with clean add line
    def targets(w):
        sal = fe(w, SAL).rectangle()
        head = [t for t in w.descendants(control_type="Text") if t.element_info.automation_id == "IncomeHeading"][0].rectangle()
        lab = [t for t in w.descendants(control_type="Text") if (t.element_info.name or "").startswith("Diary time carrying")][0].rectangle()
        emp = [b for b in w.descendants(control_type="Button") if b.element_info.automation_id == "EmployedToggle"][0].rectangle()
        return {"blank": (sal.right + 300, sal.top + 20), "heading": (head.left + 20, (head.top + head.bottom)//2),
                "label": (lab.left + 40, (lab.top + lab.bottom)//2), "button": ((emp.left+emp.right)//2, (emp.top+emp.bottom)//2),
                "title": (w.rectangle().left + 300, w.rectangle().top + 90)}
    top_scroll(w); cc(fe(w, SAL), "2925"); safe_keys("{ENTER}"); time.sleep(.5)
    for origin in ("salary", "tag", "addline", "none"):
        for tname in ("blank", "heading", "label", "button", "title"):
            top_scroll(w); time.sleep(.3)
            if origin == "salary": cc(fe(w, SAL), "3222")
            elif origin == "tag": cc(fe(w, TAG), "1650")
            elif origin == "addline": cc(fe(w, "NewPaidTagName"), "q")
            x, y = targets(w)[tname]; sclick(x, y); time.sleep(1.0)
            fe_ = focused_edits(w); typ("9"); time.sleep(.8)
            sv, tv = nums(val(fe(w, SAL))), nums(val(fe(w, TAG))); nn = val(fe(w, "NewPaidTagName")); na = val(fe(w, "NewPaidTagRate"))
            ok = (not fe_) and sv == str(salary()) and tv == str(tagrate()[0]) and nn == "" and nums(na) == ""
            rec("C5-%s->%s" % (origin, tname), ok, "focusedEdits=%r salaryBox=%r cfg=%r tagBox=%r cfg=%r newName=%r newAmt=%r" % (fe_, sv, salary(), tv, tagrate(), nn, na))
        top_scroll(w); cc(fe(w, SAL), "2925"); safe_keys("{ENTER}"); time.sleep(.4); cc(fe(w, TAG), "1600"); safe_keys("{ENTER}"); time.sleep(.4)
    print("income:", cfg()["income"], flush=True)
    # D: B1-B1c regression: Enter saves + focus nowhere; minus persists; thin spaces; no spinners
    for key, label, getc in ((SAL, "salary", salary), (TAG, "tag", lambda: tagrate()[0])):
        top_scroll(w); cc(fe(w, key), "1234567"); safe_keys("{ENTER}"); time.sleep(1.0)
        rec("D-enter-" + label, getc() == 1234567 and not focused_edits(w) and val(fe(w, key)) == "1 234 567", "stored=%r box=%r focusedEdits=%r" % (getc(), val(fe(w, key)), focused_edits(w)))
        cc(fe(w, key), "-500"); time.sleep(.6); t1 = val(fe(w, key)); h1 = allmsgs(w)
        safe_keys("{TAB}"); time.sleep(.8)
        t2 = val(fe(w, key)); h2 = allmsgs(w)
        shot(w, "b1d2_minus_%s.png" % label)
        rec("D-minus-" + label, t1 == "-500" and t2 == "-500" and any("negative" in x[0] for x in h2) and getc() == 1234567, "typed=%r after Tab=%r msgs=%r stored=%r" % (t1, t2, h2, getc()))
        cc(fe(w, key), "2000"); safe_keys("{ENTER}"); time.sleep(.8)
    sp = [e for e in w.descendants() if e.element_info.control_type in ("Spinner",) or "Spin" in (e.element_info.name or "") or "Increase" in (e.element_info.name or "")]
    rec("D-no-spinners", not sp, "spinner-like elements=%d" % len(sp))
    # E: Tab path through add line: name -> Tab -> amount; Tab on to Add button; Space on Add with empty amount
    top_scroll(w)
    cc(fe(w, "NewPaidTagName"), "tabby"); safe_keys("{TAB}"); typ("250"); safe_keys("{TAB}"); time.sleep(.6)
    f = focused_any(w)
    safe_keys("{ENTER}"); time.sleep(1.0)
    rec("E-tab-to-Add-then-Enter", "tabby" in names(), "focus on Tab#2=%r tags=%r focus now=%r" % (f, names(), focused_any(w)))
    print("final", json.dumps(cfg()), flush=True)
except Abort as ex:
    print("ABORT (safety guard):", ex, flush=True)
finally:
    kill(p)
print("SUMMARY")
for r in R: print(r[0], r[1])
