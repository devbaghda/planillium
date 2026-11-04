# G12 QA (agentic): Esc / blank click / Tab on Settings boxes, salary ceiling, spin button. Real input, own PID only.
import os, sys, time, json, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from b1f_lib import *
import b1f_lib as L

def msgs():
    out = []
    for t in L.WIN[0].descendants(control_type="Text"):
        n = t.element_info.name or ""
        if any(k in n for k in ("too large", "must", "above", "Amount", "negative", "number", "Saved", "Key", "kept")):
            r = t.rectangle()
            if r.width() > 0: out.append(n[:110])
    return out
def F(): f = focused(); return "%s|%s|%s" % (f[0], f[1][:30], f[2])
def aicfg():
    c = cfg(); return {k: v for k, v in c.items() if "ai" in k.lower() or "model" in k.lower()}
BL = [None]
def find_blank():
    P = byid("SectionScroll").rectangle()
    for fx in (0.92, 0.85, 0.75):
        for fy in (0.85, 0.7, 0.5, 0.3):
            x = int(P.left + (P.right - P.left) * fx); y = int(P.top + (P.bottom - P.top) * fy)
            a = at(x, y)
            if a[2] in ("pane", "group", "scroll viewer", "custom", "") and a[0] in ("", "SectionScroll") and not a[1]:
                return (x, y, a)
    return None
def blank():
    b = find_blank(); BL[0] = b
    if b is None: raise Abort("no blank point")
    bring_front(); click(b[0], b[1]); time.sleep(1)
    return b
def step(name): bring_front(); time.sleep(.3); log("== " + name)

def cred_count():
    o = subprocess.run(["cmdkey", "/list"], capture_output=True, text=True).stdout
    return len(o.splitlines()), sum(1 for l in o.splitlines() if "lanill" in l or "anthropic" in l.lower())

try:
    make_root(with_fixture=True)
    c0 = cred_count(); log("cmdkey before", c0)
    launch(); time.sleep(2); dismiss_dialogs(); refresh(); bring_front()
    log("dialogs left:", [b.element_info.name for b in w().descendants(control_type="Button") if b.element_info.automation_id == "PrimaryButton"])
    log("sections:", [li.element_info.name for li in w().descendants(control_type="ListItem")][:20])
    PART = os.environ.get("PART", "AI,INC,SIB,SPIN")
    if "AI" in PART:
        ok = section("AI"); log("AI section", ok); refresh()
        log("blank point", find_blank())
        cap = lambda: fe("AiCapBox") or fe("Monthly AI limit"); mod = lambda: fe("AiModelBox") or fe("Model"); key = lambda: fe("AiKeyBox") or fe("Anthropic API key")
        log("AI edits", [(e.element_info.automation_id, e.element_info.name) for e in edits()])
        log("start cap=%r model=%r cfg=%r msgs=%r" % (val(cap()), val(mod()), aicfg(), msgs()))
        step("L1 cap type 7, Esc"); eclick(cap()); time.sleep(.8); keys("^a7"); time.sleep(1); keys("{ESC}"); time.sleep(1)
        log("focus", F(), "cap", val(cap()), "cfg", aicfg(), "msgs", msgs())
        step("L3 cap -5 Enter, Esc"); eclick(cap()); time.sleep(.8); keys("^a-5"); time.sleep(.5); keys("{ENTER}"); time.sleep(1)
        log("after Enter focus", F(), "cap", val(cap()), "msgs", msgs())
        keys("{ESC}"); time.sleep(1)
        log("after Esc focus", F(), "cap", val(cap()), "cfg", aicfg(), "msgs", msgs())
        step("L4 cap 12, blank click"); eclick(cap()); time.sleep(.8); keys("^a12"); time.sleep(.8); b = blank()
        log("blank", b, "focus", F(), "cap", val(cap()), "cfg", aicfg(), "msgs", msgs())
        step("L11 cap 6, blank, then 9"); eclick(cap()); time.sleep(.8); keys("^a6"); time.sleep(.8); blank()
        log("focus after blank", F()); keys("9"); time.sleep(1)
        log("after 9: focus", F(), "cap", val(cap()), "model", val(mod()), "keylen", len(val(key()) or ""), "cfg", aicfg())
        step("L13a cap 8, blank, Shift+Tab"); eclick(cap()); time.sleep(.8); keys("^a8"); time.sleep(.8); blank()
        keys("+{TAB}"); time.sleep(1); log("after Shift+Tab focus", F(), "cfg", aicfg())
        blank(); log("reset focus", F())
        step("L13a' cap, Esc, Shift+Tab"); eclick(cap()); time.sleep(.8); keys("{ESC}"); time.sleep(.8); keys("+{TAB}"); time.sleep(1)
        log("after Esc+Shift+Tab focus", F()); blank()
        step("L13a'' cap, blank, Tab"); eclick(cap()); time.sleep(.8); blank(); keys("{TAB}"); time.sleep(1)
        log("after blank+Tab focus", F()); blank()
        step("L2 model type, Esc"); m0 = val(mod()); eclick(mod()); time.sleep(.8); keys("^aqa-model-esc"); time.sleep(.8); keys("{ESC}"); time.sleep(1)
        log("focus", F(), "model", val(mod()), "was", m0, "cfg", aicfg())
        step("L2 key type, Esc"); eclick(key()); time.sleep(.8); keys("sk-ant-garbage123"); time.sleep(.8); keys("{ESC}"); time.sleep(1)
        log("focus", F(), "keylen", len(val(key()) or ""), "cfg", aicfg(), "msgs", msgs(), "cmdkey", cred_count())
        step("key type, blank click"); eclick(key()); time.sleep(.8); keys("sk-ant-garb2"); time.sleep(.8); blank()
        log("focus", F(), "keylen", len(val(key()) or ""), "cfg", aicfg(), "msgs", msgs())
        step("L10 model type, blank"); eclick(mod()); time.sleep(.8); keys("^aqa-model-new"); time.sleep(.8); blank()
        log("focus", F(), "model", val(mod()), "cfg", aicfg())
        step("model type, Enter"); eclick(mod()); time.sleep(.8); keys("^aqa-model-enter"); time.sleep(.8); keys("{ENTER}"); time.sleep(1)
        log("focus", F(), "model", val(mod()), "cfg", aicfg()); blank()
        step("reopen Settings")
        navs = [li for li in w().descendants(control_type="ListItem") if li.element_info.name in ("Today", "Reports", "Diary")]
        log("nav", [n.element_info.name for n in navs])
        if navs:
            eclick(navs[0]); time.sleep(2); refresh()
            st = [li for li in w().descendants(control_type="ListItem") if li.element_info.name == "Settings"]
            if st: eclick(st[0]); time.sleep(2.5); refresh(); section("AI"); refresh()
            log("after reopen model", val(mod()), "cap", val(cap()))
        shot(os.path.join(HERE, "g12_ai.png"))
    if "INC" in PART:
        step("Income"); section("Income"); refresh()
        log("income edits", [(e.element_info.automation_id, e.element_info.name) for e in edits()])
        sal = salary_box
        def tagbox():
            for e in edits():
                if "Studioshoo" in (e.element_info.name or ""): return e
        nn = lambda: fe("NewPaidTagName") or fe("New payable tag")
        log("start salary", val(sal()), salary(), "tag", val(tagbox()) if tagbox() else None, tags())
        step("L5 tag 1234 Esc"); eclick(tagbox()); time.sleep(.8); keys("^a1234"); time.sleep(.8); keys("{ESC}"); time.sleep(1)
        log("focus", F(), "tag", val(tagbox()), tags(), "msgs", msgs())
        step("L6 salary 999999999999 Tab"); eclick(sal()); time.sleep(.8); keys("^a999999999999"); time.sleep(1)
        log("while typed msgs", msgs()); keys("{TAB}"); time.sleep(1.2)
        log("focus", F(), "salary box", val(sal()), "stored", salary(), "msgs", msgs())
        shot(os.path.join(HERE, "g12_l6.png"))
        step("salary 999999999999 Enter"); eclick(sal()); time.sleep(.8); keys("^a999999999999"); time.sleep(.6); keys("{ENTER}"); time.sleep(1.2)
        log("focus", F(), "salary box", val(sal()), "stored", salary(), "msgs", msgs()); blank()
        step("salary 100000,01 blank"); eclick(sal()); time.sleep(.8); keys("^a100000,01"); time.sleep(.6); blank()
        log("focus", F(), "salary box", val(sal()), "stored", salary(), "msgs", msgs())
        step("salary 100000 Enter (at ceiling)"); eclick(sal()); time.sleep(.8); keys("^a100000"); time.sleep(.6); keys("{ENTER}"); time.sleep(1.2)
        log("focus", F(), "salary box", val(sal()), "stored", salary(), "msgs", msgs()); blank()
        step("tag 999999999999 Enter"); eclick(tagbox()); time.sleep(.8); keys("^a999999999999"); time.sleep(.6); keys("{ENTER}"); time.sleep(1.2)
        log("focus", F(), "tag", val(tagbox()), tags(), "msgs", msgs()); blank()
        step("L7 salary 2925 Enter"); eclick(sal()); time.sleep(.8); keys("^a2925"); time.sleep(.6); keys("{ENTER}"); time.sleep(1.2)
        log("focus", F(), "salary box", val(sal()), "stored", salary(), "msgs", msgs()); blank()
        step("L7b salary 3100,5 Tab"); eclick(sal()); time.sleep(.8); keys("^a3100,5"); time.sleep(.6); keys("{TAB}"); time.sleep(1.2)
        log("focus", F(), "salary box", val(sal()), "stored", salary(), "msgs", msgs()); blank()
        step("L12 salary click, Esc, Shift+Tab"); eclick(sal()); time.sleep(.8); keys("{ESC}"); time.sleep(.8); keys("+{TAB}"); time.sleep(1)
        log("focus", F()); blank()
        step("L13b tag click, blank, Tab"); eclick(tagbox()); time.sleep(.8); blank(); keys("{TAB}"); time.sleep(1)
        log("focus", F()); blank()
        step("tag click, blank, Shift+Tab"); eclick(tagbox()); time.sleep(.8); blank(); keys("+{TAB}"); time.sleep(1)
        log("focus", F()); blank()
        step("L14 new tag name abc Esc"); eclick(nn()); time.sleep(.8); keys("abc"); time.sleep(.8); keys("{ESC}"); time.sleep(1)
        log("focus", F(), "name", val(nn()), tags())
        ed = edits(); i = [k for k, e in enumerate(ed) if e.element_info.automation_id == "NewPaidTagName" or "New payable tag" in (e.element_info.name or "")]
        rate = ed[i[0] + 1] if i and i[0] + 1 < len(ed) else None
        log("rate box", rate.element_info.automation_id if rate else None, rate.element_info.name if rate else None)
        if rate:
            step("new tag rate 55 Esc"); eclick(rate); time.sleep(.8); keys("55"); time.sleep(.8); keys("{ESC}"); time.sleep(1)
            log("focus", F(), "rate", val(rate), tags())
            step("new tag name+rate 200000 Enter"); eclick(nn()); time.sleep(.8); keys("bigtag"); eclick(rate); time.sleep(.6); keys("200000"); time.sleep(.6); keys("{ENTER}"); time.sleep(1.2)
            log("focus", F(), tags(), "msgs", msgs()); blank()
    if "SIB" in PART:
        for sec, names in (("General", ["Your name"]), ("Hours and reminders", ["Work start", "Work end", "Day review at", "Grace before alert", "Repeat every", "Idle after", "Keep detailed diary", "Warn before day ends"]),
                           ("Activity keywords", ["On-plan"]), ("Idle-answer library", ["Off-plan"])):
            step("sibling " + sec); section(sec); refresh()
            for n in names:
                e = fe(n)
                if e is None: log(n, "not found"); continue
                e = reveal(lambda: fe(n)); v0 = val(e)
                eclick(e); time.sleep(.7); keys("^{END}7"); time.sleep(.7); keys("{ESC}"); time.sleep(.9)
                f1 = F(); v1 = val(fe(n))
                eclick(fe(n)); time.sleep(.7); blank(); f2 = F()
                log("%s: before=%r afterEsc=%r focusEsc=%s focusBlank=%s" % (n, (v0 or "")[:20], (v1 or "")[:20], f1, f2))
        section("Scoring"); refresh()
        sc = [e for e in edits()][:2]
        for e in sc:
            n = e.element_info.name; v0 = val(e)
            eclick(e); time.sleep(.7); keys("{END}7"); time.sleep(.7); keys("{ESC}"); time.sleep(.9)
            f1 = F(); v1 = val(fe(n)); eclick(fe(n)); time.sleep(.7); blank(); f2 = F()
            log("Scoring %s: before=%r afterEsc=%r focusEsc=%s focusBlank=%s" % (n, v0, v1, f1, f2))
    if "SPIN" in PART:
        step("L15 spin"); section("Hours and reminders"); refresh()
        g = reveal(lambda: fe("Grace before alert")); v0 = val(g); eclick(g); time.sleep(1)
        btns = [b for b in w().descendants(control_type="Button") if any(k in ((b.element_info.automation_id or "") + (b.element_info.name or "")) for k in ("Spin", "Increase", "Up"))]
        log("value", v0, "spin buttons", [(b.element_info.automation_id, b.element_info.name, b.rectangle()) for b in btns])
        up = [b for b in btns if "Up" in (b.element_info.automation_id or "") or "Increase" in (b.element_info.name or "")]
        if up:
            r = up[0].rectangle(); log("at spin", at((r.left + r.right) // 2, (r.top + r.bottom) // 2))
            eclick(up[0]); time.sleep(1.2); log("after spin click value", val(fe("Grace before alert")), "focus", F(), "cfg grace", {k: v for k, v in cfg().items() if "grace" in k.lower()})
        blank()
    log("cmdkey after", cred_count())
except Abort as ex:
    log("ABORT", ex)
except Exception as ex:
    import traceback; log("ERROR", traceback.format_exc()[-800:])
finally:
    kill()
