from b1d_lib import *
import sys
SAL = "Previous salary"; TAG = "Studioshoo"
def names(): return [p["tag"] for p in cfg().get("paid_tags", [])]
def ntags(): return len(cfg().get("paid_tags", []))
def top_scroll(w):
    r = w.rectangle()
    for _ in range(6):
        mouse.scroll(coords=(r.left + 500, r.top + 400), wheel_dist=5); time.sleep(.2)
def btn(w, name, idx=-1):
    bs = [b for b in w.descendants(control_type="Button") if b.element_info.name == name and b.rectangle().width() > 0]
    return bs[idx] if bs else None
def in_win(w, el):
    r = el.rectangle(); W = w.rectangle()
    return r.width() > 0 and r.left >= W.left and r.right <= W.right and r.top >= W.top + 100 and r.bottom <= W.bottom
def reveal(w, el):
    W = w.rectangle()
    for _ in range(6):
        r = el.rectangle()
        if r.bottom > W.bottom - 40: mouse.scroll(coords=(W.left + 600, W.top + 400), wheel_dist=-2)
        elif r.top < W.top + 130: mouse.scroll(coords=(W.left + 600, W.top + 400), wheel_dist=2)
        else: break
        time.sleep(.25)
def newmsgs(w, before):
    return sorted(set((n[:70], a) for n, a, _ in texts(w)) - set((n[:70], a) for n, a in before))
def refusal_check(w, tagN):
    base = ntags()
    reveal(w, fe(w, "NewPaidTagName"))
    before = [(n, a) for n, a, _ in texts(w)]
    cc(fe(w, "NewPaidTagName"), "asd")
    cc(fe(w, "NewPaidTagRate"), "")
    reveal(w, btn(w, "Add")); btn(w, "Add").click_input(); time.sleep(1.0)
    m1 = newmsgs(w, before); c1 = ntags(); in1 = "asd" in names()
    reveal(w, fe(w, "NewPaidTagName")); fe(w, "NewPaidTagName").click_input(); safe_keys("{ENTER}"); time.sleep(1.0)
    m1b = newmsgs(w, before); c1b = ntags()
    hint = [t for t in w.descendants(control_type="Text") if t.element_info.automation_id == "NewPaidTagHint" or ("number" in (t.element_info.name or "").lower() and "amount" in (t.element_info.name or "").lower())]
    vis = [in_win(w, h) for h in hint]
    shot(w, "b1d_refuse_empty_%dtags.png" % tagN)
    cfg_has = "asd" in json.dumps(cfg())
    rec("1@%dtags" % tagN, c1 == base and c1b == base and not cfg_has and bool(m1) and bool(m1b) and (not vis or all(vis)),
        "Add-click: added=%s msgs=%r | Enter-in-name: tags=%d msgs=%r | cfg has asd=%s | hint elems=%d inWindow=%r | foc=%r" % (in1, m1, c1b, m1b, cfg_has, len(hint), vis, focused_edits(w)))
    cc(fe(w, "NewPaidTagRate"), "abc"); time.sleep(.3)
    t_abc = val(fe(w, "NewPaidTagRate"))
    before2 = [(n, a) for n, a, _ in texts(w)]
    reveal(w, btn(w, "Add")); btn(w, "Add").click_input(); time.sleep(1.0)
    m2 = newmsgs(w, before2); c2 = ntags()
    fe(w, "NewPaidTagName").click_input(); safe_keys("{ENTER}"); time.sleep(1.0); c2b = ntags()
    cc(fe(w, "NewPaidTagRate"), "abc"); safe_keys("{ENTER}"); time.sleep(1.0); c2c = ntags()
    msgs_now = [n for n, a, _ in texts(w) if "number" in n.lower() or "amount" in n.lower() or "name" in n.lower()]
    shot(w, "b1d_refuse_abc_%dtags.png" % tagN)
    rec("2@%dtags" % tagN, c2 == base and c2b == base and c2c == base and "asd" not in names() and bool(msgs_now),
        "amount box after typing abc=%r; Add-click tags=%d newmsgs=%r; Enter-name tags=%d; Enter-in-amount tags=%d; visible msgs=%r" % (t_abc, c2, m2, c2b, c2c, [m[:60] for m in msgs_now]))
    safe_keys("{ESC}"); time.sleep(.6)

def add_tag(w, name, amt):
    top_scroll(w); cc(fe(w, "NewPaidTagName"), name); cc(fe(w, "NewPaidTagRate"), amt)
    b = btn(w, "Add"); reveal(w, b); btn(w, "Add").click_input(); time.sleep(1.0)

p, w = start()
try:
    print("start cfg", salary(), tagrate(), val(fe(w, SAL)), val(fe(w, TAG)), "fg", fg_ok(), flush=True)
    refusal_check(w, 1)
    # 3
    cc(fe(w, "NewPaidTagName"), "zed"); cc(fe(w, "NewPaidTagRate"), "500"); safe_keys("{TAB}"); time.sleep(1.0)
    tn, tr = val(fe(w, "NewPaidTagName")), val(fe(w, "NewPaidTagRate")); f3 = focused_any(w)
    rec("3a-tab-keeps", tn == "zed" and nums(tr) == "500" and "zed" not in names(), "after Tab name=%r amount=%r tags=%r focus=%r" % (tn, tr, names(), f3))
    reveal(w, btn(w, "Add")); btn(w, "Add").click_input(); time.sleep(1.2)
    rec("3b-add-works", "zed" in names() and [q["monthly_net_eur"] for q in cfg()["paid_tags"] if q["tag"] == "zed"] == [500], "tags=%r rates=%r name box=%r amount box=%r focus=%r" % (names(), tagrate(), val(fe(w, "NewPaidTagName")), val(fe(w, "NewPaidTagRate")), focused_any(w)))
    # 8
    top_scroll(w)
    cc(fe(w, "NewPaidTagName"), "ok1"); cc(fe(w, "NewPaidTagRate"), "400"); b = btn(w, "Add"); reveal(w, b); btn(w, "Add").click_input(); time.sleep(1.2)
    f8 = focused_any(w); bad = [x for x in f8 if x[0] == "Edit" or x[1] == "Add"]
    rec("8", "ok1" in names() and not bad, "tags=%r focus after mouse Add=%r" % (names(), f8))
    # 4
    top_scroll(w)
    cc(fe(w, "NewPaidTagName"), "esc1"); cc(fe(w, "NewPaidTagRate"), "123"); safe_keys("{ESC}"); time.sleep(.8)
    a = (val(fe(w, "NewPaidTagName")), val(fe(w, "NewPaidTagRate")), names(), focused_any(w))
    top_scroll(w)
    cc(fe(w, "NewPaidTagName"), "out1"); cc(fe(w, "NewPaidTagRate"), "321"); bx, by = blank(w); sclick(bx, by); time.sleep(.8)
    b_ = (val(fe(w, "NewPaidTagName")), val(fe(w, "NewPaidTagRate")), names(), focused_any(w))
    rec("4", a[0] == "" and nums(a[1]) == "" and b_[0] == "" and nums(b_[1]) == "" and "esc1" not in names() and "out1" not in names(),
        "Esc: %r | blank click: %r" % (a, b_))
    # 6
    for key, label in ((SAL, "salary"), (TAG, "tag"), ("NewPaidTagRate", "newamt")):
        top_scroll(w); cc(fe(w, key), "500 123"); time.sleep(.6)
        ro = readouts(w); ok = not any("—" in r or "--" in r or "?/h" in r for r in ro)
        rec("6-" + label, ok, "box=%r readouts=%r" % (val(fe(w, key)), ro))
        shot(w, "b1d_500123_%s.png" % label)
        if key == "NewPaidTagRate": safe_keys("{ESC}")
        else: safe_keys("{ENTER}")
        time.sleep(.8)
    print("after 500123: cfg", salary(), tagrate(), flush=True)
    top_scroll(w); cc(fe(w, SAL), "2925"); safe_keys("{ENTER}"); time.sleep(.8); cc(fe(w, TAG), "1600"); safe_keys("{ENTER}"); time.sleep(.8)
    # 7
    top_scroll(w); cc(fe(w, SAL), "3111"); safe_keys("{ENTER}"); time.sleep(1.0)
    s_a = [n for n, a in msgs(w, ("Saved",))]
    cc(fe(w, "NewPaidTagName"), "asd"); cc(fe(w, "NewPaidTagRate"), ""); reveal(w, btn(w, "Add")); btn(w, "Add").click_input(); time.sleep(.8)
    s_b = [n for n, a in msgs(w, ("Saved",))]
    rec("7", bool(s_a) and not s_b, "after save: %r ; after refused add: %r ; all msgs=%r" % (s_a, s_b, msgs(w)))
    safe_keys("{ESC}"); time.sleep(.5)
    # 9
    for key, label, getc, exp in ((SAL, "salary", salary, 3111), (TAG, "tag", lambda: tagrate()[0], 1600)):
        for how in ("tab", "enter", "blank", "otherbox"):
            for txt in ("", "abc", "   "):
                top_scroll(w); cc(fe(w, key), txt); time.sleep(.3)
                if how == "tab": safe_keys("{TAB}")
                elif how == "enter": safe_keys("{ENTER}")
                elif how == "blank": bx, by = blank(w); sclick(bx, by)
                else: fe(w, TAG if key == SAL else SAL).click_input()
                time.sleep(1.0)
                bx_ = val(fe(w, key)); c = getc(); ms = [m[:50] for m in msgs(w, ("negative", "more than", "number"))]
                agree = (nums(bx_) == str(exp)) and c == exp
                rec("9-%s-%s-%r" % (label, how, txt), (agree or (bx_ != "" and ms)) and c == exp,
                    "box=%r stored=%r (exp %r) msgs=%r focus=%r" % (bx_, c, exp, ms, focused_edits(w)))
    top_scroll(w); cc(fe(w, TAG), "0"); safe_keys("{TAB}"); time.sleep(1.0)
    rec("9-tag-zero", tagrate()[0] == 1600 and bool(msgs(w, ("more than",))), "box=%r stored=%r msgs=%r" % (val(fe(w, TAG)), tagrate(), msgs(w, ("more than",))))
    cc(fe(w, TAG), "1600"); safe_keys("{ENTER}"); time.sleep(.8)
    # 5
    def targets(w):
        sal = fe(w, SAL).rectangle()
        head = [t for t in w.descendants(control_type="Text") if t.element_info.automation_id == "IncomeHeading"][0].rectangle()
        lab = [t for t in w.descendants(control_type="Text") if (t.element_info.name or "").startswith("Diary time carrying")][0].rectangle()
        emp = [b for b in w.descendants(control_type="Button") if b.element_info.automation_id == "EmployedToggle"][0].rectangle()
        return {"blank": (sal.right + 300, sal.top + 20), "heading": (head.left + 20, (head.top + head.bottom)//2),
                "label": (lab.left + 40, (lab.top + lab.bottom)//2), "button": ((emp.left+emp.right)//2, (emp.top+emp.bottom)//2)}
    top_scroll(w)
    for origin in ("salary", "tag", "addline", "none"):
        for tname in ("blank", "heading", "label", "button", "button"):
            top_scroll(w); time.sleep(.3)
            if origin == "salary": cc(fe(w, SAL), "3222")
            elif origin == "tag": cc(fe(w, TAG), "1650")
            elif origin == "addline": cc(fe(w, "NewPaidTagName"), "q")
            tg = targets(w)
            x, y = tg[tname]; sclick(x, y); time.sleep(1.0)
            fe_ = focused_edits(w); typ("9"); time.sleep(.8)
            sv, tv = nums(val(fe(w, SAL))), nums(val(fe(w, TAG)))
            ok = (not fe_) and sv == str(salary()) and tv == str(tagrate()[0]) and nums(val(fe(w, "NewPaidTagName"))) in ("", "q") and "9" not in val(fe(w, "NewPaidTagName")) and nums(val(fe(w, "NewPaidTagRate"))) == ""
            rec("5-%s->%s" % (origin, tname), ok, "focusedEdits=%r salaryBox=%r cfgSalary=%r tagBox=%r cfgTags=%r newName=%r newAmt=%r allfocus=%r" % (fe_, sv, salary(), tv, tagrate()[:2], val(fe(w, "NewPaidTagName")), val(fe(w, "NewPaidTagRate")), focused_any(w)))
        top_scroll(w); cc(fe(w, SAL), "3111"); safe_keys("{ENTER}"); time.sleep(.5); cc(fe(w, TAG), "1600"); safe_keys("{ENTER}"); time.sleep(.5)
    print("income cfg:", cfg().get("income"), flush=True)
    # N / N+2 tags
    top_scroll(w)
    while ntags() < 3: add_tag(w, "t%d" % ntags(), "100")
    refusal_check(w, ntags())
    while ntags() < 5: add_tag(w, "t%d" % ntags(), "100")
    refusal_check(w, ntags())
    cc(fe(w, "NewPaidTagRate"), "-5"); time.sleep(1.0)
    hint = [t for t in w.descendants(control_type="Text") if "negative" in (t.element_info.name or "") and t.rectangle().width() > 0]
    shot(w, "b1d_minus_5tags.png")
    rec("add-line-minus@5tags", bool(hint) and all(in_win(w, h) for h in hint), "hints=%d rects=%r window=%r" % (len(hint), [str(h.rectangle()) for h in hint], str(w.rectangle())))
    cc(fe(w, "NewPaidTagRate"), "0"); time.sleep(1.0)
    hint = [t for t in w.descendants(control_type="Text") if "more than 0" in (t.element_info.name or "") and t.rectangle().width() > 0]
    shot(w, "b1d_zero_5tags.png")
    rec("add-line-zero@5tags", bool(hint) and all(in_win(w, h) for h in hint), "hints=%d rects=%r" % (len(hint), [str(h.rectangle()) for h in hint]))
    safe_keys("{ESC}"); time.sleep(.5)
    # standing extras
    for key, label in (("NewPaidTagName", "newname"), ("NewPaidTagRate", "newamt"), (SAL, "salary"), (TAG, "tag")):
        top_scroll(w) if key in (SAL, TAG) else reveal(w, fe(w, key))
        base_c = (salary(), tagrate()[:2], ntags())
        cc(fe(w, key), "                    "); safe_keys("{ENTER}"); time.sleep(.8)
        r1 = (val(fe(w, key)), salary(), tagrate()[:2], ntags())
        cc(fe(w, key), "9" * 40); time.sleep(.6); v = val(fe(w, key)); safe_keys("{ENTER}"); time.sleep(1.0)
        r2 = (v[:50], salary(), tagrate()[:2], ntags(), focused_edits(w))
        R.append(("standing-" + label, "INFO", "base=%r spaces=%r long=%r" % (base_c, r1, r2)))
        print("   standing", label, R[-1][2], flush=True)
        top_scroll(w) if key in (SAL, TAG) else reveal(w, fe(w, key))
        if key == SAL: cc(fe(w, SAL), "3111"); safe_keys("{ENTER}")
        if key == TAG: cc(fe(w, TAG), "1600"); safe_keys("{ENTER}")
        if key in ("NewPaidTagRate", "NewPaidTagName"): safe_keys("{ESC}")
        time.sleep(.6)
    print("final cfg", json.dumps(cfg()), flush=True)
except Abort as ex:
    print("ABORT (safety guard):", ex, flush=True)
finally:
    kill(p)
print("SUMMARY")
for r in R: print(r[0], r[1])
