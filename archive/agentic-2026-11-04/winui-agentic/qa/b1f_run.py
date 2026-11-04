# B1f focus-jump harness. Real mouse (SendInput, cursor moved, button held), ~1 s between clicks, UIA focus check
# after every blank click, then a typed "9" and a check that no box / stored value changed.
# usage: py b1f_run.py <cases> <holds> <reps> [extra_tags]
#   cases: comma list of  c1 | c2newamt | c2salary | c2tag | c2toggle | c2list | c3 | c3act | c4 | c4scroll
#   holds: comma list of seconds the button is held, e.g. 0,0.05,0.12,0.25
import random
from b1f_lib import *

cases = sys.argv[1].split(",")
holds = [float(h) for h in sys.argv[2].split(",")]
reps = int(sys.argv[3]) if len(sys.argv) > 3 else 1
extra_tags = int(sys.argv[4]) if len(sys.argv) > 4 else 0
random.seed(7)

def rect(e): return e.rectangle()
def mid(r): return ((r.left + r.right) // 2, (r.top + r.bottom) // 2)

def setup_fixture():
    """Fixture made through the app: salary 2925 + Enter, Studioshoo 1600 + Enter; optional extra tags by mouse Add."""
    section("Income"); refresh()
    # The app shows built-in defaults equal to the fixture and does not save an unchanged value, so each box is
    # set to another amount first, then to the fixture amount.
    time.sleep(1.5)
    for name, v in (("Previous salary", "3000"), ("Previous salary", "2925"), ("Studioshoo", "1700"), ("Studioshoo", "1600")):
        for attempt in range(4):
            b = fe(name); eclick(b); time.sleep(.5)
            if not fe(name).has_keyboard_focus(): log("  setup: %s did not take focus, focus=%r" % (name, focused()[:3])); continue
            keys("^a{BACKSPACE}" + v + "{ENTER}"); time.sleep(1.0)
            got = salary() if name.startswith("Prev") else dict((t.lower(), r) for t, r in tags()).get("studioshoo")
            if got == int(v): break
            log("  setup: %s=%r after typing %s; retry" % (name, got, v))
    for i in range(extra_tags):
        eclick(reveal(lambda: fe("NewPaidTagName"))); time.sleep(.3); keys("x%d" % i)
        eclick(reveal(lambda: fe("NewPaidTagRate"))); time.sleep(.3); keys("100")
        add = reveal(lambda: ([b for b in w().descendants(control_type="Button") if b.element_info.name == "Add"] or [None])[-1])
        eclick(add); time.sleep(1.0)
    log("fixture config:", salary(), tags())
    assert salary() == 2925 and ("studioshoo", 1600) in [(t.lower(), v) for t, v in tags()]

def to_top():
    P = rect(byid("SectionScroll"))
    for _ in range(3): wheel(P.left + 300, P.top + 100, +3); time.sleep(.25)
    time.sleep(.4)

def blank_points():
    P = rect(byid("SectionScroll"))
    sal = rect(salary_box()); tog = rect(byid("EmployedToggle")); stu = rect(fe("Studioshoo")); nt = rect(fe("NewPaidTagName"))
    pw = rect(byid("PaidWorkHeading"))
    cand = [("top-right", P.right - 25, sal.top + 15), ("top-mid", sal.right + 150, sal.top + 10),
            ("right-of-toggle", P.right - 60, (tog.top + tog.bottom) // 2), ("middle", P.left + int(P.width() * .62), (stu.top + stu.bottom) // 2),
            ("right-of-addline", P.right - 40, (nt.top + nt.bottom) // 2), ("left-gap-tag/addline", P.left + 12, (stu.bottom + nt.top) // 2),
            ("left-gap-salary/toggle", P.left + 8, (sal.bottom + tog.top) // 2), ("mid-toggle/paidwork", P.left + 260, (tog.bottom + pw.top) // 2),
            ("bottom-left", P.left + 15, P.bottom - 20), ("bottom-mid", P.left + P.width() // 2, P.bottom - 50),
            ("bottom-right", P.right - 25, P.bottom - 10), ("right-mid", P.right - 15, (P.top + P.bottom) // 2)]
    if os.environ.get("QA_EDGE"):
        # the ScrollViewer's own 4 px right padding strip (outside SectionContent) and the very edges
        cand = [("edge-r1-top", P.right - 1, sal.top + 15), ("edge-r2-mid", P.right - 2, (P.top + P.bottom) // 2),
                ("edge-r3-bottom", P.right - 3, P.bottom - 15), ("edge-r2-toggle", P.right - 2, (tog.top + tog.bottom) // 2),
                ("edge-bottom-1", P.left + 300, P.bottom - 1), ("edge-left-1", P.left + 1, P.bottom - 30)]
    pts = []
    for nm, x, y in cand:
        if P.left < x < P.right and P.top < y < P.bottom and at(x, y)[0] == "SectionScroll": pts.append((nm, x, y))
    return pts

def snapshot():
    vals = {}
    for e in edits():
        if e.rectangle().width() > 0 or True: vals[e.element_info.automation_id or e.element_info.name] = val(e)
    return vals, salary(), tags()

def recover():
    """After a failure: put the fixture amounts back by real input (Enter parks focus), so the next trial starts clean."""
    try:
        refresh(); to_top()
        for name, v in (("Previous salary", "2925"), ("Studioshoo", "1600")):
            b = fe(name)
            if nums(val(b)) != v or salary() != 2925:
                eclick(b, 0.05); time.sleep(.3); keys("^a{BACKSPACE}" + v + "{ENTER}"); time.sleep(.7)
        log("  recovered:", salary(), tags(), "focus", focused()[:3])
    except Exception as ex: log("recover failed", ex)

def src_element(kind):
    if kind == "name": return fe("NewPaidTagName")
    if kind == "newamt": return fe("NewPaidTagRate")
    if kind == "salary": return salary_box()
    if kind == "tag": return fe("Studioshoo")
    if kind == "toggle": return byid("EmployedToggle")
    if kind == "list": return [li for li in w().descendants(control_type="ListItem") if li.element_info.name == "Income"][0]

def switch_away():
    u.SetForegroundWindow(u.GetShellWindow()); time.sleep(.6)
    return not fg_ok()

def root_pid_at(x, y):
    h = u.WindowFromPoint(wintypes.POINT(int(x), int(y))); h = u.GetAncestor(h, 2); pid = wintypes.DWORD()
    u.GetWindowThreadProcessId(h, ctypes.byref(pid)); return pid.value

STATS = {}
FOCUS_AFTER = {}
def trial(case, kind, pt, hold):
    nm, x, y = pt
    to_top() if case in ("c4scroll",) else None
    before = snapshot()
    src = reveal(lambda: src_element(kind)) if kind != "list" else src_element(kind)
    eclick(src, hold)
    if kind == "toggle":
        time.sleep(.6); eclick(src_element("toggle"), hold)   # toggle back, focus stays on the toggle
    time.sleep(.35)
    srcok = True
    if kind in ("name", "newamt", "salary", "tag"):
        srcok = src_element(kind).has_keyboard_focus()
    time.sleep(.65)  # ~1 s between the two clicks
    note = ""
    if case == "c3":
        away = switch_away(); back = bring_front(); time.sleep(.5); note = "away=%s back=%s" % (away, back)
        if not back: raise Abort("could not bring app back")
    if case == "c3act":
        away = switch_away(); note = "away=%s" % away
        if root_pid_at(x, y) != PID[0]: raise Abort("blank point not on our window")
        move(x, y); u.mouse_event(0x2, 0, 0, 0, 0); time.sleep(hold); u.mouse_event(0x4, 0, 0, 0, 0)
    elif case in ("c4", "c4scroll"):
        wheel(x, y, -1); time.sleep(.6)
        if case == "c4scroll":
            pts = blank_points(); nm, x, y = random.choice(pts)
            note = "after scroll at %s" % nm
        click(x, y, hold)
    else:
        if at(x, y)[0] != "SectionScroll":
            fresh = blank_points(); old = nm; nm, x, y = random.choice(fresh); note = "point %s not blank after reveal; used %s" % (old, nm)
        click(x, y, hold)
    time.sleep(.5)
    if case == "c3act":
        for _ in range(10):
            if fg_ok(): break
            time.sleep(.2)
    f = focused()
    jumped = is_salary_focus()
    edit_focus = [e.element_info.automation_id or e.element_info.name for e in edits() if e.has_keyboard_focus()]
    keys("9"); time.sleep(.45)
    after = snapshot()
    changed = before != after
    ok = (not jumped) and not edit_focus and not changed
    key = (case, kind)
    s = STATS.setdefault(key, [0, 0, 0]); s[0] += 1; s[1] += (not ok); s[2] += (not srcok)
    FOCUS_AFTER.setdefault(key, {}).setdefault(f[1] or f[0] or f[2], 0)
    FOCUS_AFTER[key][f[1] or f[0] or f[2]] += 1
    log("%s %-6s %-6s hold=%.2f pt=%-24s srcfocus=%s focus=%r jumped=%s editfocus=%s changed=%s %s %s" % (
        "OK  " if ok else "FAIL", case, kind, hold, nm, srcok, f[:3], jumped, edit_focus, changed,
        "" if not changed else (before, after), note))
    if not ok:
        recover()
        # put boxes back if the "9" landed somewhere
        if after[1] != 2925 or after != before: log("  state drift; before=%r after=%r" % (before, after))
    if case in ("c4scroll",): to_top()

CASE_SRC = {"c1": ["name"], "c2newamt": ["newamt"], "c2salary": ["salary"], "c2tag": ["tag"], "c2toggle": ["toggle"],
            "c2list": ["list"], "c3": ["name", "tag", "newamt"], "c3act": ["name", "tag", "newamt"],
            "c4": ["name", "tag", "newamt"], "c4scroll": ["name", "salary", "tag"]}
make_root(with_fixture=False)
p = launch()
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front()
    setup_fixture(); refresh(); to_top()
    pts = blank_points()
    log("window", w().rectangle(), "pane", rect(byid("SectionScroll")), "blank points", pts)
    for case in cases:
        for kind in CASE_SRC[case]:
            for r in range(reps):
                order = [(pt, h) for pt in pts for h in holds]
                random.shuffle(order)
                if case in ("c3", "c3act", "c4", "c4scroll"): order = order[: max(8, len(order) // 2)]
                for pt, h in order:
                    try: trial(case, kind, pt, h)
                    except Abort as ex: log("ABORT trial", case, kind, pt, ex); bring_front(); time.sleep(.5)
    log("==== SUMMARY (case, source): trials, failures, source-focus-misses")
    for k, v in STATS.items(): log(k, v, "focus after blank click:", FOCUS_AFTER.get(k))
    log("final config:", salary(), tags())
    shot(os.path.join(HERE, "b1f_end_%s.png" % os.environ.get("QA_TAG", "run")))
finally:
    kill()
