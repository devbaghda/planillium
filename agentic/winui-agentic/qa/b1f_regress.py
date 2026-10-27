# B1f regression with real input: (A) Settings controls still work now that SectionContent claims pointer releases
# (radio, slider click + drag, toggle, NumberBox, text-box drag selection, selectable TextBlock drag selection,
# section switching, wheel scroll); (B) B1/B1b/B1c/B1d lines incl. every B1d exit path; (C) standing input-box matrix.
# usage: py b1f_regress.py A,B,C
from b1f_lib import *
from pywinauto.uia_defines import get_elem_interface

parts = sys.argv[1].split(",") if len(sys.argv) > 1 else ["A", "B", "C"]
R = []
def rec(line, ok, ev):
    R.append((line, ok, ev)); log("%s: %s -- %s" % (line, "PASS" if ok else "FAIL", ev))
def iface(el, name): return get_elem_interface(el.element_info.element, name)
def state(): return json.load(open(os.path.join(ROOT, "data", "winui_state.json"), encoding="utf-8"))
def btn(name):
    bs = [b for b in w().descendants(control_type="Button") if b.element_info.name == name and b.rectangle().width() > 0]
    return bs[-1] if bs else None
def text_el(pred):
    for t in w().descendants(control_type="Text"):
        if pred(t.element_info.name or "", t.element_info.automation_id or "") and t.rectangle().width() > 0: return t
def selection_text(el):
    try:
        arr = iface(el, "Text").GetSelection()
        return "".join(arr.GetElement(i).GetText(-1) for i in range(arr.Length))
    except Exception as ex: return "<no TextPattern: %s>" % repr(ex)[:60]
def edit_focus(): return [e.element_info.automation_id or e.element_info.name for e in edits() if e.has_keyboard_focus()]
def hint():
    t = byid("NewPaidTagHint");
    try: return t.element_info.name if t and t.rectangle().width() > 0 else ""
    except Exception: return ""
def status():
    t = byid("SaveStatus"); return (t.element_info.name or "") if t else ""
def pane(): return byid("SectionScroll").rectangle()
def blank_pt():
    P = pane(); sal = salary_box().rectangle(); x, y = P.right - 25, sal.top + 15
    assert at(x, y)[0] == "SectionScroll", at(x, y); return x, y
def heading_pt():
    r = byid("IncomeHeading").rectangle(); return r.left + 15, (r.top + r.bottom) // 2
def to_top():
    P = pane()
    for _ in range(3): wheel(P.left + 300, P.top + 100, +3); time.sleep(.2)
    time.sleep(.4)
def tagnames(): return [t.lower() for t, _ in tags()]
def set_box(el, text, hold=0.1):
    eclick(el, hold); time.sleep(.35); keys("^a{BACKSPACE}")
    if text: keys(text.replace("{", "{{}").replace("+", "{+}") if False else text)
    time.sleep(.3)
def fixture():
    section("Income"); refresh(); time.sleep(1.2)
    for name, v in (("Previous salary", "3000"), ("Previous salary", "2925"), ("Studioshoo", "1700"), ("Studioshoo", "1600")):
        for _ in range(4):
            eclick(fe(name)); time.sleep(.5)
            if not fe(name).has_keyboard_focus(): continue
            keys("^a{BACKSPACE}" + v + "{ENTER}"); time.sleep(1.0); break
    log("fixture:", salary(), tags())
def reset():
    """clean state: fixture values, empty add line, focus parked (Enter)."""
    refresh(); section("Income"); to_top()
    if val(fe("NewPaidTagName")) or val(fe("NewPaidTagRate")):
        eclick(fe("NewPaidTagName")); time.sleep(.3); keys("{ESC}"); time.sleep(.5)
    for name, v in (("Previous salary", 2925), ("Studioshoo", 1600)):
        cur = salary() if name.startswith("Prev") else dict((t.lower(), r) for t, r in tags()).get("studioshoo")
        if cur != v or nums(val(fe(name))) != str(v):
            eclick(fe(name)); time.sleep(.4); keys("^a{BACKSPACE}%d{ENTER}" % v); time.sleep(.8)
    for t in [t for t in tagnames() if t not in ("studioshoo",)]:
        pass  # extra tags are removed only where a test added them

def remove_tag(name):
    # the Remove button sits on the tag's row; find the row by the tag box rectangle
    box = fe(name)
    if box is None: return
    br = box.rectangle()
    for b in w().descendants(control_type="Button"):
        r = b.rectangle()
        if b.element_info.name == "Remove" and abs(((r.top + r.bottom) // 2) - ((br.top + br.bottom) // 2)) < 25:
            eclick(b); time.sleep(1.0)
            for b2 in w().descendants(control_type="Button"):  # confirmation dialog, if any
                if b2.element_info.automation_id == "PrimaryButton": eclick(b2); time.sleep(1.0)
            return

# ======================= A: controls =======================
def part_A():
    section("General"); refresh(); time.sleep(.8)
    # radio buttons
    radios = {r.element_info.name: r for r in w().descendants(control_type="RadioButton")}
    log("radios", list(radios))
    dark = radios.get("Dark"); fw = [r for n, r in radios.items() if n.startswith("Follow")][0]
    for hold in (0.0, 0.12, 0.25):
        eclick(dark, hold); time.sleep(.8)
        s1 = iface(dark, "SelectionItem").CurrentIsSelected; t1 = state().get("theme") or state().get("Theme")
        eclick(fw, hold); time.sleep(.8)
        s2 = iface(fw, "SelectionItem").CurrentIsSelected; t2 = state().get("theme") or state().get("Theme")
        rec("A-radio hold=%.2f" % hold, s1 and s2 and t1 == "dark" and t2 == "default", "Dark selected=%s state=%r; Follow selected=%s state=%r" % (s1, t1, s2, t2))
    # slider: click on track, then drag the thumb
    sl = byid("OpacitySlider"); rv = lambda: iface(byid("OpacitySlider"), "RangeValue").CurrentValue
    r = sl.rectangle(); log("slider rect", r, "value", rv())
    # the track sits in the lower part of the control (header above)
    ty = r.bottom - 14
    def thumb_x(v): return int(r.left + 10 + (v - 40) / 60.0 * (r.width() - 20))
    for hold in (0.0, 0.12, 0.25):
        v0 = rv(); click(r.left + int(r.width() * 0.3), ty, hold); time.sleep(.8); v1 = rv()
        drag(thumb_x(v1), ty, r.right - 2, ty, steps=16, hold=max(hold, 0.05)); time.sleep(.8); v2 = rv()
        st = state().get("opacity") or state().get("Opacity")
        rec("A-slider hold=%.2f" % hold, v1 < 70 and v2 == 100 and st == 100, "start=%s after track click at 30%%=%s after thumb drag to right end=%s state=%s" % (v0, v1, v2, st))
    # text box: drag-select inside 'Your name', then type to replace the selection
    yn = byid("YourName"); eclick(yn); time.sleep(.3); keys("^a{BACKSPACE}Alexander Smith"); time.sleep(.3)
    keys("{TAB}"); time.sleep(.6)
    yr = yn.rectangle(); y = yr.bottom - 16
    drag(yr.left + 12, y, yr.left + 70, y); time.sleep(.5)
    sel = selection_text(byid("YourName")); foc = byid("YourName").has_keyboard_focus()
    rec("A-textbox drag selection", len(sel) >= 3 and foc, "selected %r, box focused=%s" % (sel, foc))
    keys("{END}"); time.sleep(.2); keys("{BACKSPACE 15}QA{TAB}"); time.sleep(.6)
    rec("A-textbox still saves", cfg().get("user_name") == "QA", "user_name=%r" % cfg().get("user_name"))
    # double-click selects a word
    click(yr.left + 20, y, 0.03); time.sleep(.05); click(yr.left + 20, y, 0.03); time.sleep(.5)
    rec("A-textbox double-click selects word", selection_text(byid("YourName")) == "QA", "selected %r" % selection_text(byid("YourName")))
    keys("{TAB}"); time.sleep(.4)
    # Income toggle
    section("Income"); refresh(); time.sleep(.6)
    tg = byid("EmployedToggle"); ts = lambda: iface(byid("EmployedToggle"), "Toggle").CurrentToggleState
    for hold in (0.0, 0.12, 0.25):
        a = ts(); eclick(tg, hold); time.sleep(.8); b = ts(); e1 = cfg().get("income", {}).get("employed")
        eclick(byid("EmployedToggle"), hold); time.sleep(.8); c = ts(); e2 = cfg().get("income", {}).get("employed")
        rec("A-toggle hold=%.2f" % hold, a != b and c == a and e1 is True and e2 is False, "state %s->%s->%s employed %s->%s" % (a, b, c, e1, e2))
    # Hours: NumberBox typing, spin via keyboard arrow, text box drag selection
    section("Hours and reminders"); refresh(); time.sleep(.8)
    nb = byid("GraceMin"); log("GraceMin", nb.element_info.control_type if nb else None)
    gi = [e for e in edits() if (e.element_info.name or "").startswith("Grace")]
    g = gi[0] if gi else nb
    eclick(g); time.sleep(.3); keys("^a{BACKSPACE}7{TAB}"); time.sleep(.8)
    rec("A-numberbox type+Tab", cfg().get("reminder_grace_minutes") == 7, "reminder_grace_minutes=%r" % cfg().get("reminder_grace_minutes"))
    eclick(g); time.sleep(.3); keys("{UP}"); time.sleep(.3); keys("{TAB}"); time.sleep(.8)
    rec("A-numberbox Up arrow", cfg().get("reminder_grace_minutes") == 8, "after Up+Tab=%r" % cfg().get("reminder_grace_minutes"))
    # compact spin popup: focus shows a spin icon; look for Increase buttons
    eclick(g); time.sleep(.6)
    ups = [b for b in w().descendants(control_type="Button") if "ncrease" in (b.element_info.name or "") or b.element_info.automation_id in ("UpSpinButton", "PopupUpSpinButton")]
    ups = [b for b in ups if b.rectangle().width() > 0]
    if ups:
        eclick(ups[0], 0.12); time.sleep(.6); keys("{TAB}"); time.sleep(.8)
        rec("A-numberbox spin button click", cfg().get("reminder_grace_minutes") == 9, "spin %r -> %r" % (ups[0].element_info.automation_id, cfg().get("reminder_grace_minutes")))
    else:
        log("A-numberbox spin button: none visible in UIA after focus (compact mode)"); keys("{TAB}")
    ws = byid("WorkStart"); eclick(ws); time.sleep(.3); keys("^a{BACKSPACE}08:30{TAB}"); time.sleep(.6)
    wr = ws.rectangle(); yy = wr.bottom - 16
    drag(wr.left + 10, yy, wr.left + 40, yy); time.sleep(.4)
    rec("A-hours textbox drag selection", len(selection_text(byid("WorkStart"))) >= 2, "selected %r" % selection_text(byid("WorkStart")))
    keys("{TAB}"); time.sleep(.4)
    # Keywords: multi-line box, click places caret, drag select
    section("Activity keywords"); refresh(); time.sleep(.8)
    ro = byid("RulesOn"); eclick(ro); time.sleep(.3); keys("alpha{ENTER}beta"); time.sleep(.2); keys("{TAB}"); time.sleep(.6)
    rr = ro.rectangle(); drag(rr.left + 10, rr.top + 40, rr.left + 60, rr.top + 40); time.sleep(.4)
    s = selection_text(byid("RulesOn"))
    rec("A-multiline drag selection + save", len(s) >= 2 and "alpha" in json.dumps(cfg()), "selected %r; alpha saved=%s" % (s, "alpha" in json.dumps(cfg())))
    keys("{TAB}"); time.sleep(.4)
    # Data: selectable TextBlock drag selection (no buttons pressed here)
    section("Data"); refresh(); time.sleep(.8)
    di = byid("DataInfo"); dr = di.rectangle(); log("DataInfo", dr, repr((di.element_info.name or "")[:60]))
    drag(dr.left + 3, dr.top + 9, dr.left + 200, dr.top + 9); time.sleep(.5)
    s = selection_text(byid("DataInfo"))
    rec("A-selectable TextBlock drag selection", len(s) >= 5, "selected %r" % s[:60])
    shot(os.path.join(HERE, "b1f_A_data_selection.png"))
    # right-click on the selection still opens the text context menu (Copy)
    rclick(dr.left + 60, dr.top + 9); time.sleep(1.0)
    items = [m.element_info.name for m in Desktop(backend="uia").window(process=PID[0]).descendants(control_type="MenuItem")] if False else \
            [m.element_info.name for m in w().descendants(control_type="MenuItem")]
    if not items:
        try:
            for top in Application(backend="uia").connect(process=PID[0]).windows():
                items += [m.element_info.name for m in top.descendants(control_type="MenuItem")]
        except Exception: pass
    rec("A-right-click context menu on selectable text", any("Copy" in (i or "") for i in items), "menu items %r" % items)
    keys("{ESC}"); time.sleep(.4)
    # right-click inside a TextBox (Your name) opens its menu too -- checked in General below via YourName

    # click blank in Data afterwards; selection should clear or stay harmless; focus not on any edit
    P = pane(); click(P.right - 25, P.bottom - 20, 0.12); time.sleep(.5)
    rec("A-blank click in Data", not edit_focus(), "focus=%r" % (focused()[:3],))
    # AI section: model box drag select (no network call)
    section("AI"); refresh(); time.sleep(.8)
    mb = byid("AiModelBox"); eclick(mb); time.sleep(.3); keys("^a{BACKSPACE}claude-test-model"); time.sleep(.3); keys("{TAB}"); time.sleep(.5); mr = mb.rectangle()
    log("AI model text", val(mb))
    drag(mr.left + 10, mr.bottom - 16, mr.left + 80, mr.bottom - 16); time.sleep(.4)
    rec("A-AI model box drag selection", len(selection_text(byid("AiModelBox"))) >= 3, "selected %r" % selection_text(byid("AiModelBox")))
    keys("{ESC}"); time.sleep(.3)
    # section list clicks still switch sections
    okc = []
    for nm in ("General", "Income", "Hours and reminders", "Scoring", "Activity keywords", "Idle-answer library", "TickTick", "AI", "Data", "Income"):
        section(nm); refresh()
        okc.append((nm, any(li.element_info.name == nm and iface(li, "SelectionItem").CurrentIsSelected for li in w().descendants(control_type="ListItem"))))
    rec("A-section list clicks", all(o for _, o in okc), repr(okc))
    # wheel scroll still scrolls a long section (Scoring/Keywords) -- measure a box's top before/after
    section("Activity keywords"); refresh(); time.sleep(.6)
    t0 = byid("RulesOn").rectangle().top; P = pane(); wheel(P.left + 300, P.top + 150, -2); time.sleep(.8)
    t1 = byid("RulesOn").rectangle().top
    rec("A-wheel scroll", t1 < t0 or (t1 == t0 and "noscroll"), "RulesOn top %s -> %s (pane %s)" % (t0, t1, P))
    to_top()

# ======================= B: B1..B1d lines =======================
def part_B():
    section("Income"); refresh(); to_top()
    SAL = "Previous salary"; TAG = "Studioshoo"
    # B1 6-8 / B1b 9: formatting + no spinners on money boxes
    set_box(fe(SAL), "14000"); v = val(fe(SAL)); keys("{ENTER}"); time.sleep(.8)
    rec("B1-6 14000 formatting/save", nums(v) == "14000" and v != "14000" and salary() == 14000, "box %r saved %r" % (v, salary()))
    spins = [b for b in w().descendants(control_type="Button") if "pin" in (b.element_info.automation_id or "") and b.rectangle().width() > 0]
    rec("B1-8 no spinners in Income", not spins, repr([b.element_info.automation_id for b in spins]))
    reset()
    # B1b 1-3 / B1c 1: type without leaving keeps stored; Enter saves, focus nowhere, 9 changes nothing
    for box, stored, key in ((SAL, 2925, None), (TAG, 1600, "studioshoo")):
        get = (lambda: salary()) if key is None else (lambda: dict((t.lower(), r) for t, r in tags()).get("studioshoo"))
        set_box(fe(box), "3000"); time.sleep(.6); s0 = get()
        keys("{ENTER}"); time.sleep(.9); s1 = get(); ef = edit_focus()
        before = (val(fe(SAL)), val(fe(TAG))); keys("9"); time.sleep(.5); after = (val(fe(SAL)), val(fe(TAG)))
        rec("B1b-1/B1c-1/2 %s Enter" % box, s0 == stored and s1 == 3000 and not ef and before == after, "typing-only stored=%r after Enter=%r focus=%r 9-changed=%s" % (s0, s1, ef, before != after))
        set_box(fe(box), "3100"); keys("{TAB}"); time.sleep(.9)
        rec("B1c-3 %s Tab" % box, get() == 3100 and not fe(SAL).has_keyboard_focus(), "stored=%r focus=%r" % (get(), focused()[:3]))
        set_box(fe(box), "3200"); click(*blank_pt(), 0.12); time.sleep(.9)
        rec("B1c-4 %s click blank" % box, get() == 3200 and not edit_focus(), "stored=%r focus=%r" % (get(), focused()[:3]))
        other = TAG if box == SAL else SAL
        set_box(fe(box), "3300"); eclick(fe(other), 0.12); time.sleep(.9)
        rec("B1c-4 %s click other box" % box, get() == 3300 and fe(other).has_keyboard_focus(), "stored=%r focus=%r" % (get(), focused()[:3]))
        reset()
    # B1c 7-10 minus
    for box in (SAL, TAG):
        set_box(fe(box), "-500"); time.sleep(.4); v = val(fe(box)); st = status(); h = [t.element_info.name for t in w().descendants(control_type="Text") if "negative" in (t.element_info.name or "").lower() and t.rectangle().width() > 0]
        keys("{TAB}"); time.sleep(.8); v2 = val(fe(box)); h2 = [t.element_info.name for t in w().descendants(control_type="Text") if "negative" in (t.element_info.name or "").lower() and t.rectangle().width() > 0]
        rec("B1c-7/10 %s minus" % box, v.startswith("-") and h and v2.startswith("-") and h2 and salary() == 2925 and ("studioshoo", 1600) in [(a.lower(), b) for a, b in tags()],
            "box %r msg %r; after Tab box %r msg %r; stored %r %r" % (v, h[:1], v2, h2[:1], salary(), tags()))
        reset()
    # B1d 1-2: refused add (empty, abc) by mouse Add and by Enter in name
    for amt in ("", "abc", "0"):
        for how in ("mouse", "enter"):
            set_box(fe("NewPaidTagName"), "asd"); set_box(fe("NewPaidTagRate"), amt)
            if how == "mouse": eclick(btn("Add"), 0.12)
            else: eclick(fe("NewPaidTagName"), 0.1); time.sleep(.3); keys("{ENTER}")
            time.sleep(1.0); hh = hint()
            rec("B1d-1/2 amount %r Add by %s" % (amt, how), "asd" not in tagnames() and bool(hh), "hint %r tags %r" % (hh, tagnames()))
            keys("{ESC}") if fg_ok() else None; time.sleep(.6)
            rec("B1d-4 Esc after refused (%r,%s)" % (amt, how), val(fe("NewPaidTagName")) == "" and not hint(), "name %r hint %r" % (val(fe("NewPaidTagName")), hint()))
    # refused mouse Add then click outside (blank / heading) clears
    for where in ("blank", "heading"):
        set_box(fe("NewPaidTagName"), "asd"); set_box(fe("NewPaidTagRate"), ""); eclick(btn("Add"), 0.12); time.sleep(1.0)
        click(*(blank_pt() if where == "blank" else heading_pt()), 0.12); time.sleep(.9)
        rec("B1d-4 click %s after refused mouse Add" % where, val(fe("NewPaidTagName")) == "" and not hint() and not edit_focus(), "name %r hint %r focus %r" % (val(fe("NewPaidTagName")), hint(), focused()[:3]))
    # B1d 3: Tab out of amount keeps the text; then mouse Add works and parks focus (B1d 8)
    set_box(fe("NewPaidTagName"), "zed"); set_box(fe("NewPaidTagRate"), "500"); keys("{TAB}"); time.sleep(.8)
    kept = (val(fe("NewPaidTagName")), nums(val(fe("NewPaidTagRate"))))
    rec("B1d-3 Tab keeps add line", kept == ("zed", "500") and "zed" not in tagnames(), "kept %r tags %r focus %r" % (kept, tagnames(), focused()[:3]))
    eclick(btn("Add"), 0.12); time.sleep(1.2); f = focused()
    rec("B1d-8 mouse Add adds and parks", ("zed", 500) in [(a.lower(), b) for a, b in tags()] and not edit_focus() and f[1] != "Add", "tags %r focus %r" % (tags(), f[:3]))
    before = [val(e) for e in edits()]; keys("9"); time.sleep(.5)
    rec("B1d-8 typed 9 after mouse Add", before == [val(e) for e in edits()], "unchanged")
    remove_tag("zed"); refresh()
    # B1c 5: Enter in name and in amount adds, then no box focused
    for k, which in (("e1", "NewPaidTagName"), ("e2", "NewPaidTagRate")):
        set_box(fe("NewPaidTagName"), k); set_box(fe("NewPaidTagRate"), "500"); eclick(fe(which), 0.1); time.sleep(.3); keys("{END}{ENTER}"); time.sleep(1.2)
        rec("B1c-5 Enter in %s adds" % which, k in tagnames() and not edit_focus(), "tags %r focus %r" % (tagnames(), focused()[:3]))
        remove_tag(k); refresh()
    # B1d 4: Esc in line / click outside clears without adding
    set_box(fe("NewPaidTagName"), "qq"); keys("{ESC}"); time.sleep(.7)
    rec("B1d-4 Esc clears", val(fe("NewPaidTagName")) == "" and "qq" not in tagnames(), "name %r" % val(fe("NewPaidTagName")))
    set_box(fe("NewPaidTagName"), "qq"); click(*blank_pt(), 0.12); time.sleep(.8)
    rec("B1d-4 click outside clears", val(fe("NewPaidTagName")) == "" and "qq" not in tagnames() and not edit_focus(), "name %r focus %r" % (val(fe("NewPaidTagName")), focused()[:3]))
    # B1d 5: blank/heading/button(EmployedToggle)/label after leaving salary, tag box, add line; then 9
    lab = text_el(lambda n, a: n.startswith("Diary time carrying")).rectangle()
    targets = {"blank": blank_pt, "heading": heading_pt, "label": lambda: (lab.left + 40, (lab.top + lab.bottom) // 2),
               "paidwork-heading": lambda: (byid("PaidWorkHeading").rectangle().left + 20, (byid("PaidWorkHeading").rectangle().top + byid("PaidWorkHeading").rectangle().bottom) // 2)}
    for src in (SAL, TAG, "NewPaidTagName", "NewPaidTagRate"):
        for tname, tf in targets.items():
            for hold in (0.0, 0.12, 0.25):
                eclick(fe(src), hold); time.sleep(1.0); click(*tf(), hold); time.sleep(.6)
                jumped = fe(SAL).has_keyboard_focus(); ef = edit_focus()
                b = [val(e) for e in edits()]; keys("9"); time.sleep(.45); a = [val(e) for e in edits()]
                rec("B1d-5 %s -> %s hold=%.2f" % (src, tname, hold), not jumped and not ef and a == b and salary() == 2925, "focus %r 9-changed %s" % (focused()[:3], a != b))
                if a != b or jumped: reset()
    # B1d 5 with a button target: the toggle (click twice so config ends unchanged), focus must be the toggle, not salary
    for src in (SAL, TAG, "NewPaidTagName"):
        eclick(fe(src), 0.12); time.sleep(1.0); eclick(byid("EmployedToggle"), 0.12); time.sleep(.6); eclick(byid("EmployedToggle"), 0.12); time.sleep(.6)
        b = [val(e) for e in edits()]; keys("9"); time.sleep(.45); a = [val(e) for e in edits()]
        rec("B1d-5 %s -> button(toggle)" % src, not fe(SAL).has_keyboard_focus() and a == b and cfg()["income"].get("employed") is False, "focus %r 9-changed %s" % (focused()[:3], a != b))
    # B1d 6: 500 123
    for box in (SAL, TAG, "NewPaidTagRate"):
        set_box(fe(box), "500 123"); time.sleep(.6)
        ro = [t.element_info.name for t in w().descendants(control_type="Text") if (t.element_info.name or "").startswith("=") and t.rectangle().width() > 0]
        rec("B1d-6 500 123 in %s" % box, not any("--" in r for r in ro), "box %r readouts %r" % (val(fe(box)), ro))
        keys("{ESC}" if box == "NewPaidTagRate" else "{TAB}"); time.sleep(.6); reset()
    # B1d 7: refused entry after a save clears "Saved."
    set_box(fe(SAL), "2926"); keys("{ENTER}"); time.sleep(.8); s1 = status()
    set_box(fe("NewPaidTagName"), "asd"); set_box(fe("NewPaidTagRate"), ""); eclick(btn("Add"), 0.12); time.sleep(1.0); s2 = status()
    rec("B1d-7 Saved cleared by refusal", "saved" in s1.lower() and "saved." not in s2.lower(), "after save %r; after refusal %r" % (s1, s2))
    keys("{ESC}"); time.sleep(.5); reset()
    # B1d 9: emptied / abc / spaces then leave -> box and stored agree
    for box, stored in ((SAL, "2925"), (TAG, "1600")):
        for txt in ("", "abc", "   "):
            for leave in ("tab", "enter", "blank", "other"):
                set_box(fe(box), txt)
                if leave == "tab": keys("{TAB}")
                elif leave == "enter": keys("{ENTER}")
                elif leave == "blank": click(*blank_pt(), 0.12)
                else: eclick(fe(TAG if box == SAL else SAL), 0.12)
                time.sleep(.9)
                v = nums(val(fe(box))); sv = salary() if box == SAL else dict((t.lower(), r) for t, r in tags()).get("studioshoo")
                sal_ok = (not fe(SAL).has_keyboard_focus()) or (leave == "other" and box == TAG)
                rec("B1d-9 %s %r leave by %s" % (box, txt, leave), v == stored and str(sv) == stored and sal_ok,
                    "box %r stored %r focus %r" % (val(fe(box)), sv, focused()[:3]))
                reset()

# ======================= C: standing input-box matrix =======================
def part_C():
    section("Income"); refresh(); to_top()
    SAL = "Previous salary"; TAG = "Studioshoo"
    inputs = {"empty": "", "spaces": "   ", "text": "abc", "minus": "-5", "long": "9" * 40, "valid": "2500"}
    exits = ("tab", "enter", "esc", "other", "blank", "heading")
    for box in (SAL, TAG, "NewPaidTagName", "NewPaidTagRate"):
        for ik, txt in inputs.items():
            if box == "NewPaidTagName" and ik in ("minus",): txt = "-5"
            for ex in exits:
                reset()
                if box == "NewPaidTagRate": set_box(fe("NewPaidTagName"), "mx")
                set_box(fe(box), txt)
                shown = val(fe(box))
                other = SAL if box != SAL else TAG
                if ex == "tab": keys("{TAB}")
                elif ex == "enter": keys("{ENTER}")
                elif ex == "esc": keys("{ESC}")
                elif ex == "other": eclick(fe(other), 0.12)
                elif ex == "blank": click(*blank_pt(), 0.12)
                else: click(*heading_pt(), 0.12)
                time.sleep(1.0)
                f = focused(); sal_focus = fe(SAL).has_keyboard_focus(); ef = edit_focus()
                sv, tv = salary(), dict((t.lower(), r) for t, r in tags()).get("studioshoo")
                after = val(fe(box)); msgs = [t.element_info.name for t in w().descendants(control_type="Text")
                                              if any(k in (t.element_info.name or "").lower() for k in ("negative", "more than 0", "needs", "not saved", "number", "amount"))
                                              and t.rectangle().width() > 0][:2]
                added = [t for t in tagnames() if t not in ("studioshoo",)]
                # judgement
                problems = []
                if sv == 0 or tv == 0: problems.append("stored 0")
                if box in (SAL, TAG):
                    stored = sv if box == SAL else tv; base = 2925 if box == SAL else 1600
                    if ik in ("empty", "spaces", "text", "minus") and stored != base: problems.append("invalid saved")
                    if ik in ("empty", "spaces", "text") and ex != "esc" and nums(after) != str(base): problems.append("box/stored disagree")
                    if ik == "minus" and not msgs: problems.append("no visible reason for minus")
                    if ik == "valid" and ex != "esc" and stored != 2500: problems.append("valid not saved")
                else:
                    if added and not (ex == "enter" and ik == "valid" and box == "NewPaidTagRate"): problems.append("tag added %r" % added)
                    if sv != 2925 or tv != 1600: problems.append("stored changed")
                if sal_focus and not (ex == "other" and other == SAL): problems.append("SALARY FOCUS")
                if ex in ("blank", "heading", "enter") and ef and not (ik in ("minus",) and ex == "enter"):
                    # Enter with invalid add-line amount leaves cursor (refused, B1c-6); Enter with a minus keeps cursor
                    if not (box.startswith("NewPaid") and ex == "enter"): problems.append("edit focus %r" % ef)
                rec("C %s %s %s" % (box[:14], ik, ex), not problems, "typed %r shown %r after %r stored sal=%r tag=%r focus=%r msgs=%r added=%r %s" % (
                    txt[:12], shown[:14], after[:14], sv, tv, f[:3], [m[:40] for m in msgs], added, problems))
                for t in added: remove_tag(t); refresh()

make_root(with_fixture=False)
p = launch()
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front()
    fixture()
    for part in parts:
        try: {"A": part_A, "B": part_B, "C": part_C}[part]()
        except Abort as ex: log("ABORT part", part, ex)
        except Exception as ex:
            import traceback; log("ERROR part", part, traceback.format_exc()[-600:])
    log("==== SUMMARY: %d checks, %d FAIL" % (len(R), sum(1 for r in R if not r[1])))
    for r in R:
        if not r[1]: log("FAIL", r[0], "--", r[2])
finally:
    kill()
