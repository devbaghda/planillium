from common import *
import subprocess
R = []
def typ(s): send_keys(s, with_spaces=True, pause=0.05)
def rec(n, ok, ev):
    R.append((n, ok)); print(("%s: %s -- %s" % (n, "PASS" if ok else "FAIL", ev)).encode("ascii", "replace").decode(), flush=True)
def hint(w):
    for x in w.descendants():
        try:
            if "negative" in (x.element_info.name or "") and x.rectangle().width() > 0: return True
        except Exception: pass
    return False
def clip(s): subprocess.run(["powershell", "-command", "Set-Clipboard -Value '%s'" % s])
def shot(w, name):
    try: w.capture_as_image().save(os.path.join(os.path.dirname(__file__), name))
    except Exception as ex: print("shot fail", ex)
make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception: pass
    w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1)
    t0 = time.time()
    while time.time() - t0 < 120:
        if [b for b in w.descendants(control_type="Button") if b.element_info.name == "Later" and b.rectangle().width() > 0]:
            [b for b in w.descendants(control_type="Button") if b.element_info.name == "Later"][0].click_input(); print("  (harness) morning dialog dismissed after %.0fs" % (time.time()-t0)); break
        time.sleep(2)
    def E(sub): return [x for x in w.descendants(control_type="Edit") if x.element_info.name.startswith(sub)][0]
    # sanity: harness delivers minus
    E("New payable tag").click_input(); send_keys("a-b"); rec("sanity", E("New payable tag").get_value() == "a-b", "plain box got a-b")
    send_keys("^a{BACKSPACE}")
    for label, sub in [x for x in (("salary", "Previous salary"), ("tag", "Studioshoo"), ("newtag", "Monthly net")) if x[0] in os.environ.get("LOOPS","salary,tag,newtag")]:
        b = E(sub); b.click_input(); send_keys("^a{BACKSPACE}"); time.sleep(.3)
        typ("5"); typ("-")
        seen = []
        for t in (0.1, 0.5, 1.5, 3.5):
            time.sleep(t if not seen else t - [0.1, 0.5, 1.5, 3.5][len(seen)-1]); seen.append(hint(w))
        v = E(sub).get_value()
        rec(label + "-typed-minus-persists", all(seen) and "-" not in v and v == "5", "hint at .1/.5/1.5/3.5s=%s box=%r" % (seen, v))
        if label == "salary": shot(w, "d1_hint_visible.png")
        # next minus-free keystroke clears
        typ("0"); time.sleep(.5)
        rec(label + "-digit-clears", not hint(w) and nums_ok(E(sub).get_value(), "50") if False else not hint(w), "hint after digit=%s box=%r" % (hint(w), E(sub).get_value()))
        # minus then backspace
        typ("-"); time.sleep(.5); h1 = hint(w)
        send_keys("{BACKSPACE}"); time.sleep(.5); h2 = hint(w)
        rec(label + "-backspace-clears", h1 and not h2, "after minus=%s after backspace=%s box=%r" % (h1, h2, E(sub).get_value()))
        # double minus
        typ("--"); time.sleep(.5); h3 = hint(w); v3 = E(sub).get_value()
        rec(label + "-double-minus", h3 and "-" not in v3, "hint=%s box=%r" % (h3, v3))
        # navigation key (Left) with hint showing
        send_keys("{LEFT}"); time.sleep(.4); h4 = hint(w)
        print("  (info) hint after LEFT arrow:", h4)
        # leave via Tab clears
        send_keys("{TAB}"); time.sleep(.6)
        rec(label + "-tab-clears", not hint(w), "hint after Tab=%s" % hint(w))
        # minus then click elsewhere (heading) clears
        b = E(sub); b.click_input(); send_keys("^a{BACKSPACE}"); typ("-"); time.sleep(.4); h5 = hint(w)
        for t in w.descendants(control_type="Text"):
            if t.element_info.name == "Income" and t.rectangle().width() > 0: t.click_input(); break
        time.sleep(.6)
        rec(label + "-click-away-clears", h5 and not hint(w), "after minus=%s after clicking heading=%s" % (h5, hint(w)))
        # empty box + only minus, then Enter
        b = E(sub); b.click_input(); send_keys("^a{BACKSPACE}"); typ("-"); time.sleep(.3); send_keys("{ENTER}"); time.sleep(.6)
        print("  (info) hint after minus+Enter:", hint(w), "alive", p.poll() is None, "cfg", salary(), tagrate())
        send_keys("{TAB}"); time.sleep(.4)
    def dismiss():
        for b in w.descendants(control_type="Button"):
            if b.element_info.name == "Later" and b.rectangle().width() > 0: b.click_input(); time.sleep(.6); print("  (harness) dismissed morning dialog"); return True
        return False
    dismiss()
    # paste cases in salary
    s = E("Previous salary"); s.click_input()
    for txt, exp in (("-500", "500"), ("5-0", "50"), ("--", "")):
        dismiss(); E("Previous salary").click_input(); send_keys("^a{BACKSPACE}"); time.sleep(.3); print("  pre-paste box", repr(E("Previous salary").get_value()), "focus", E("Previous salary").has_keyboard_focus(), "edits focused", [x.element_info.name[:12] for x in w.descendants(control_type="Edit") if x.has_keyboard_focus()]); clip(txt); send_keys("^v"); time.sleep(.8)
        v = E("Previous salary").get_value().replace(" ", "")
        rec("paste-" + txt, hint(w) and v == exp, "hint=%s box=%r" % (hint(w), v))
        if txt == "-500": shot(w, "d1_paste.png")
        if txt == "--":
            shot(w, "d1_fail_after_dashdash.png")
            print("  DIAG box", repr(E("Previous salary").get_value()), "focused edits", [x.element_info.name[:12] for x in w.descendants(control_type="Edit") if x.has_keyboard_focus()])
            typ("7"); time.sleep(.6); print("  DIAG after typing 7:", repr(E("Previous salary").get_value()), "hint", hint(w), "proc alive", p.poll() is None)
            send_keys("^a{BACKSPACE}"); typ("8"); time.sleep(.6); print("  DIAG after BS,8:", repr(E("Previous salary").get_value()))
        typ("1"); time.sleep(.5)
        print("  (info) after digit hint:", hint(w))
    # paste valid after minus clears
    send_keys("^a{BACKSPACE}"); typ("-"); time.sleep(.4); clip("123"); send_keys("^a^v"); time.sleep(.6)
    rec("paste-valid-clears", not hint(w), "hint=%s box=%r" % (hint(w), E("Previous salary").get_value()))
    # rapid sequence minus, digit, minus, digit
    send_keys("^a{BACKSPACE}"); typ("-1-2-"); time.sleep(.6)
    rec("rapid-ends-with-minus", hint(w) and E("Previous salary").get_value().replace(" ", "") == "12", "hint=%s box=%r" % (hint(w), E("Previous salary").get_value()))
    send_keys("^a{BACKSPACE}"); typ("-1-2"); time.sleep(.6)
    rec("rapid-ends-with-digit", not hint(w), "hint=%s box=%r" % (hint(w), E("Previous salary").get_value()))
    send_keys("{TAB}"); time.sleep(.5)
    print("alive", p.poll() is None, salary(), tagrate())
finally:
    kill(p)
print("SUMMARY", [(n, "PASS" if ok else "FAIL") for n, ok in R])
