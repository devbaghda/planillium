# B1g QA: acceptance 1-7 in the Previous-salary box and the Studioshoo tag box, every exit, plus short Add-line / B1d
# Enter regression. Real clicks and keys (b1f_lib SendInput / send_keys), scratch root, own PID only.
# usage: QA_EXE=<worktree exe> QA_SCRATCH=<scratch> py b1g_run.py
from b1f_lib import *
exec(open(os.path.join(HERE, "b1f_regress.py"), encoding="utf-8").read().split("# ======================= A: controls")[0]
     .split("from b1f_lib import *", 1)[1].replace('parts = sys.argv[1].split(",") if len(sys.argv) > 1 else ["A", "B", "C"]', ""))

_set_box = set_box
def set_box(el, text, hold=0.1):
    if not fg_ok():
        log("  (foreground lost: %r; bringing app back)" % (focused()[:4],)); bring_front(); time.sleep(.4)
    _set_box(el, text, hold)

def visible_texts(*keys_):
    out = []
    for t in w().descendants(control_type="Text"):
        n = t.element_info.name or ""
        if any(k in n.lower() for k in keys_) and t.rectangle().width() > 0: out.append(n[:70])
    return out

def stored(name): return salary() if name.startswith("Prev") else dict((t.lower(), r) for t, r in tags()).get("studioshoo")

def run_box(name, S):
    def box(): return fe(name)
    def shown(): return nums(val(box()))
    def ok_reverted(): return shown() == str(S) and not edit_focus() and stored(name) == S
    def ev(): return "box %r focus %r stored %r status %r" % (val(box()), focused()[:3], stored(name), status())
    tag = "SAL" if name.startswith("Prev") else "TAG"
    # L1-L3: Esc
    for line, text in (("L1 2500", "2500"), ("L2 empty", ""), ("L3 abc", "abc"), ("L3 spaces", "   ")):
        set_box(box(), text); keys("{ESC}"); time.sleep(.8)
        rec("%s %s +Esc" % (tag, line), ok_reverted(), ev())
    # L4 -5 Enter (stays, message), then L5 Esc
    set_box(box(), "-5"); keys("{ENTER}"); time.sleep(.8)
    st = status(); msg = visible_texts("negative")
    rec("%s L4 -5 +Enter" % tag, "negative" in st.lower() and box().has_keyboard_focus() and stored(name) == S,
        "msgs %r %s" % (msg, ev()))
    keys("{ESC}"); time.sleep(.8)
    rec("%s L5 -5 +Esc (after Enter)" % tag, ok_reverted(), "msgs %r %s" % (visible_texts("negative"), ev()))
    # L5 direct: -5 then Esc without Enter
    set_box(box(), "-5"); keys("{ESC}"); time.sleep(.8)
    rec("%s L5 -5 +Esc (direct)" % tag, ok_reverted(), "msgs %r %s" % (visible_texts("negative"), ev()))
    # L6 12 digits, every exit
    for exitname in ("Enter", "Esc", "Tab", "click blank", "click other box"):
        set_box(box(), "999999999999"); time.sleep(.4)
        typed = val(box()); msg = visible_texts("too large", "large")
        if exitname == "Enter": keys("{ENTER}")
        elif exitname == "Esc": keys("{ESC}")
        elif exitname == "Tab": keys("{TAB}")
        elif exitname == "click blank": click(*blank_pt())
        else: eclick(fe("Studioshoo" if tag == "SAL" else "Previous salary"))
        time.sleep(.9)
        after_msg = visible_texts("too large", "large")
        okk = bool(msg) and shown() == str(S) and stored(name) == S
        if exitname in ("Enter", "Esc", "click blank"): okk = okk and not edit_focus()
        rec("%s L6 12 digits +%s" % (tag, exitname), okk,
            "typed %r msg-while-typed %r msg-after %r %s" % (typed, msg, after_msg, ev()))
        if exitname in ("Tab", "click other box"):
            click(*blank_pt()); time.sleep(.6)
    # standing: empty/letters/minus by Tab and click blank; empty by Enter (B1d)
    for text, exitname in (("", "Tab"), ("abc", "click blank"), ("", "Enter"), ("abc", "Enter")):
        set_box(box(), text)
        if exitname == "Tab": keys("{TAB}")
        elif exitname == "Enter": keys("{ENTER}")
        else: click(*blank_pt())
        time.sleep(.9)
        okk = shown() == str(S) and stored(name) == S
        if exitname != "Tab": okk = okk and not edit_focus()
        rec("%s standing %r +%s" % (tag, text, exitname), okk, ev())
        if exitname == "Tab": click(*blank_pt()); time.sleep(.6)
    for exitname in ("Tab", "click blank"):
        set_box(box(), "-5")
        keys("{TAB}") if exitname == "Tab" else click(*blank_pt())
        time.sleep(.9)
        rec("%s standing -5 +%s (old rule: text kept with message, not saved)" % (tag, exitname),
            stored(name) == S and "negative" in status().lower(), "msgs %r %s" % (visible_texts("negative"), ev()))
        eclick(box()); time.sleep(.4); keys("{ESC}"); time.sleep(.6)
    # L7 valid + Enter saves and parks; restore
    N = S + 100
    set_box(box(), str(N)); keys("{ENTER}"); time.sleep(1.0)
    rec("%s L7 valid %d +Enter" % (tag, N), shown() == str(N) and stored(name) == N and not edit_focus(), ev())
    set_box(box(), str(S)); keys("{ENTER}"); time.sleep(1.0)
    rec("%s restore %d" % (tag, S), stored(name) == S, ev())

make_root(with_fixture=True)
p = launch()
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front(); section("Income"); refresh(); time.sleep(1)
    log("start", salary(), tags())
    run_box("Previous salary", 2925)
    run_box("Studioshoo", 1600)
    # L7b Esc in new-tag add line clears and leaves (both fields)
    for fld in ("NewPaidTagName", "NewPaidTagRate"):
        set_box(fe("NewPaidTagName"), "qq"); set_box(fe("NewPaidTagRate"), "50")
        eclick(fe(fld)); time.sleep(.3); keys("{ESC}"); time.sleep(.8)
        rec("L7 add line Esc in %s" % fld, val(fe("NewPaidTagName")) == "" and val(fe("NewPaidTagRate")) == "" and not edit_focus()
            and "qq" not in tagnames(), "name %r rate %r focus %r tags %r" % (val(fe("NewPaidTagName")), val(fe("NewPaidTagRate")), focused()[:3], tags()))
    # Add line regression: refused mouse Add then Esc clears; valid Enter adds and parks
    set_box(fe("NewPaidTagName"), "asd"); set_box(fe("NewPaidTagRate"), ""); eclick(btn("Add"), 0.12); time.sleep(1.0)
    h = hint(); keys("{ESC}"); time.sleep(.8)
    rec("REG refused mouse Add then Esc", bool(h) and val(fe("NewPaidTagName")) == "" and not hint() and "asd" not in tagnames(),
        "hint-before %r name %r hint %r" % (h, val(fe("NewPaidTagName")), hint()))
    set_box(fe("NewPaidTagName"), "zed"); set_box(fe("NewPaidTagRate"), "500"); keys("{ENTER}"); time.sleep(1.2)
    rec("REG Enter in add line adds and parks", ("zed", 500) in [(a.lower(), b) for a, b in tags()] and not edit_focus(),
        "tags %r focus %r" % (tags(), focused()[:3]))
    shot(os.path.join(HERE, "b1g_end.png"))
finally:
    kill()
    n = sum(1 for r in R if r[1]); log("TOTAL %d/%d pass" % (n, len(R)))
