from common import *
import ctypes
from ctypes import wintypes
from pywinauto import mouse
R = []
from pywinauto.controls.uiawrapper import UIAWrapper
_orig_click = UIAWrapper.click_input
def _ensure(self):
    try:
        top = self.top_level_parent().rectangle()
    except Exception:
        return
    for _ in range(8):
        r = self.rectangle()
        if r.top < top.top + 135: mouse.scroll(coords=(top.left + 500, top.top + 400), wheel_dist=3)
        elif r.bottom > top.top + 700: mouse.scroll(coords=(top.left + 500, top.top + 400), wheel_dist=-3)
        else: break
        time.sleep(.3)
def _dismiss():
    try:
        for b in Desktop(backend="uia").window(title="Planillium").descendants(control_type="Button"):
            if b.element_info.name == "Later":
                b.click_input(); time.sleep(.6); print("   [dismissed popup]", flush=True); return
    except Exception: pass
def _patched(self, *a, **k):
    try:
        if self.element_info.control_type == "Edit": _ensure(self)
    except Exception: pass
    return _orig_click(self, *a, **k)
UIAWrapper.click_input = _patched
def rec(line, ok, ev):
    R.append((line, "PASS" if ok else "FAIL", ev)); print(("%s: %s -- %s" % (line, "PASS" if ok else "FAIL", ev)).encode("ascii","replace").decode(), flush=True)
def edits(w): return list(w.descendants(control_type="Edit"))
def fe(w, sub):
    for e in edits(w):
        if sub in (e.element_info.name or "") or sub == e.element_info.automation_id: return e
def val(e):
    try: return e.get_value()
    except Exception: return e.window_text()
def nums(s): return "".join(ch for ch in s if ch not in "    ")
def typ(s): send_keys(s, with_spaces=True, pause=0.05)
def cc(e, s):
    e.click_input(); time.sleep(.25); send_keys("^a"); send_keys("{BACKSPACE}"); typ(s)
def hints(w):
    out = []
    for t in w.descendants(control_type="Text"):
        try:
            if "negative" in (t.element_info.name or "") and t.rectangle().width() > 0 and t.element_info.automation_id != "SaveStatus": out.append(t.rectangle().top)
        except Exception: pass
    return out
def fe_text(w, aid):
    for t in w.descendants(control_type="Text"):
        if t.element_info.automation_id == aid: return t.element_info.name
    return ""
def focused(w):
    r = []
    for e in edits(w):
        try:
            if e.has_keyboard_focus(): r.append(e.element_info.name or e.element_info.automation_id)
        except Exception: pass
    return r
def blank(w):
    sal = fe(w, "Previous salary").rectangle()
    return (sal.right + 300, sal.top + 20)
def shot(w, path):
    from PIL import Image
    hwnd = w.handle; rc = wintypes.RECT(); ctypes.windll.user32.GetWindowRect(hwnd, ctypes.byref(rc))
    W, H = rc.right-rc.left, rc.bottom-rc.top
    hdc = ctypes.windll.user32.GetDC(0); mdc = ctypes.windll.gdi32.CreateCompatibleDC(hdc)
    bmp = ctypes.windll.gdi32.CreateCompatibleBitmap(hdc, W, H); ctypes.windll.gdi32.SelectObject(mdc, bmp)
    ctypes.windll.user32.PrintWindow(hwnd, mdc, 2)
    class BI(ctypes.Structure): _fields_=[("biSize",ctypes.c_uint32),("biWidth",ctypes.c_int32),("biHeight",ctypes.c_int32),("biPlanes",ctypes.c_uint16),("biBitCount",ctypes.c_uint16),("biCompression",ctypes.c_uint32),("biSizeImage",ctypes.c_uint32),("x",ctypes.c_int32),("y",ctypes.c_int32),("c",ctypes.c_uint32),("i",ctypes.c_uint32)]
    bi = BI(40, W, -H, 1, 32, 0, 0, 0, 0, 0, 0); buf = ctypes.create_string_buffer(W*H*4)
    ctypes.windll.gdi32.GetDIBits(mdc, bmp, 0, H, buf, ctypes.byref(bi), 0)
    Image.frombuffer("RGBA", (W, H), buf, "raw", "BGRA", 0, 1).save(path)
SAL = "Previous salary"; TAG = "Studioshoo"
make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception as ex: print("no welcome", ex)
    time.sleep(1); w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1.5); w = win(p)
    print("start", salary(), tagrate(), val(fe(w, SAL)), val(fe(w, TAG)))

    # 1
    cc(fe(w, SAL), "3000"); send_keys("{ENTER}"); time.sleep(1.2)
    f = focused(w); c1 = salary(); typ("9"); time.sleep(.8)
    rec("1", c1 == 3000 and not f and nums(val(fe(w, SAL))) == "3000" and salary() == 3000,
        "after Enter: config=%r focusedEdits=%r; typed 9 -> box=%r config=%r" % (c1, f, val(fe(w, SAL)), salary()))
    # 2
    cc(fe(w, TAG), "1700"); send_keys("{ENTER}"); time.sleep(1.2)
    f = focused(w); c2 = tagrate(); typ("9"); time.sleep(.8)
    rec("2", c2[0] == 1700 and not f and nums(val(fe(w, TAG))) == "1700", "cfg=%r focusedEdits=%r, typed 9 -> box=%r config=%r" % (c2, f, val(fe(w, TAG)), tagrate()))
    # 3
    cc(fe(w, SAL), "3100"); send_keys("{TAB}"); time.sleep(1.2)
    f = focused(w)
    rec("3", salary() == 3100 and SAL not in " ".join(f), "after Tab config=%r focusedEdits=%r" % (salary(), f))
    # 4
    cc(fe(w, SAL), "3200"); bx, by = blank(w); mouse.click(coords=(bx, by)); time.sleep(1.2)
    f = focused(w); typ("9"); time.sleep(.8)
    ok_a = salary() == 3200 and not f and nums(val(fe(w, SAL))) == "3200"
    cc(fe(w, SAL), "3300"); fe(w, TAG).click_input(); time.sleep(1.2)
    f2 = focused(w)
    ok_b = salary() == 3300 and any(TAG in x for x in f2)
    cc(fe(w, TAG), "1750"); mouse.click(coords=(bx, by)); time.sleep(1.2); f3 = focused(w); typ("9"); time.sleep(.8)
    ok_c = tagrate()[0] == 1750 and not f3 and nums(val(fe(w, TAG))) == "1750"
    rec("4", ok_a and ok_b and ok_c, "blank click salary: cfg=%r focus=%r box=%r | click other box: cfg=%r focus=%r | blank click tag: cfg=%r focus=%r box=%r" % (salary(), f, val(fe(w, SAL)), salary(), f2, tagrate(), f3, val(fe(w, TAG))))

    # 6
    n0 = len(cfg()["paid_tags"])
    n = fe(w, "NewPaidTagName"); r = fe(w, "NewPaidTagRate")
    cc(n, "bad"); r.click_input(); send_keys("^a"); send_keys("{BACKSPACE}"); typ("-"); typ("500")
    time.sleep(.6)
    send_keys("{ENTER}"); time.sleep(1); st1 = fe_text(w, "SaveStatus"); h1 = hints(w); c1 = len(cfg()["paid_tags"])
    fe(w, "NewPaidTagName").click_input(); send_keys("{ENTER}"); time.sleep(1); st2 = fe_text(w, "SaveStatus"); h2 = hints(w); c2 = len(cfg()["paid_tags"])
    r = fe(w, "NewPaidTagRate"); r.click_input(); send_keys("^a"); send_keys("{BACKSPACE}"); time.sleep(.4)
    send_keys("{ENTER}"); time.sleep(1); st3 = fe_text(w, "SaveStatus"); c3 = len(cfg()["paid_tags"])
    fe(w, "NewPaidTagName").click_input(); send_keys("{ENTER}"); time.sleep(1); st4 = fe_text(w, "SaveStatus"); c4 = len(cfg()["paid_tags"])
    rec("6", c1 == n0 and c2 == n0 and c3 == n0 and c4 == n0 and bool(h1) and bool(h2) and "number" in (st3 + st4).lower(),
        "minus: rateEnter added=%s hint=%s status=%r; nameEnter added=%s hint=%s status=%r; empty rate: rateEnter status=%r nameEnter status=%r tags=%d->%d" % (c1 != n0, bool(h1), st1, c2 != n0, bool(h2), st2, st3, st4, n0, c4))
    send_keys("{ESC}"); time.sleep(.8)
    # 7-10
    for label, key, getc in (("salary", SAL, salary), ("tag", TAG, lambda: tagrate()[0])):
        base = getc()
        cc(fe(w, key), "-500"); time.sleep(.8); print("   dbg", label, [(e.element_info.name[:8], val(e)) for e in edits(w)], hints(w), focused(w), flush=True)
        t7 = val(fe(w, key)); hv7 = hints(w); c7 = getc()
        rec("7-" + label, t7 == "-500" and len(hv7) >= 1 and c7 == base, "box=%r hints=%d config=%r (was %r)" % (t7, len(hv7), c7, base))
        typ("7"); time.sleep(.5); typ("0"); time.sleep(.6)
        t8 = val(fe(w, key)); hv8 = hints(w)
        rec("8-" + label, nums(t8) == "-50070" and len(hv8) >= 1 and getc() == base, "box=%r hints=%d config=%r" % (t8, len(hv8), getc()))
        send_keys("{TAB}"); time.sleep(1.0)
        t10 = val(fe(w, key)); hv10 = hints(w); c10 = getc(); f10 = focused(w)
        rec("10-tab-" + label, nums(t10) == "-50070" and len(hv10) >= 1 and c10 == base, "after Tab box=%r hints=%d config=%r focus=%r" % (t10, len(hv10), c10, f10))
        fe(w, key).click_input(); send_keys("{HOME}"); send_keys("{DELETE}"); time.sleep(.8)
        t9 = val(fe(w, key)); hv9 = hints(w)
        rec("9-" + label, nums(t9) == "50070" and len(hv9) == 0, "after deleting minus box=%r hints=%d" % (t9, len(hv9)))
        send_keys("{ENTER}"); time.sleep(1.0)
        print("   saved after Enter:", getc())
        cc(fe(w, key), "-8"); time.sleep(.5); bx, by = blank(w); mouse.click(coords=(bx, by)); time.sleep(1.0)
        t = val(fe(w, key)); hv = hints(w)
        rec("10-click-" + label, t == "-8" and len(hv) >= 1 and getc() != 8 and not focused(w), "after blank click box=%r hints=%d config=%r focus=%r" % (t, len(hv), getc(), focused(w)))
        cc(fe(w, key), "1234"); send_keys("{ENTER}"); time.sleep(1.0)
    # click another box with invalid text
    cc(fe(w, SAL), "-9"); fe(w, TAG).click_input(); time.sleep(1.0)
    rec("10-click-box", val(fe(w, SAL)) == "-9" and len(hints(w)) >= 1 and salary() == 1234, "salary box=%r hints=%d cfg=%r focus=%r" % (val(fe(w, SAL)), len(hints(w)), salary(), focused(w)))
    # 12 screenshots
    cc(fe(w, SAL), "-500"); time.sleep(.8); shot(w, "b1c_hint_salary.png")
    cc(fe(w, "NewPaidTagRate"), "-500"); time.sleep(.8); shot(w, "b1c_hint_newtag.png")
    # 11
    n0 = len(cfg()["paid_tags"])
    cc(fe(w, "NewPaidTagName"), "esc1"); fe(w, "NewPaidTagRate").click_input(); send_keys("^a"); typ("77"); send_keys("{ESC}"); time.sleep(1)
    e1 = val(fe(w, "NewPaidTagName")) == "" and len(cfg()["paid_tags"]) == n0
    cc(fe(w, "NewPaidTagName"), "out1"); fe(w, "NewPaidTagRate").click_input(); typ("88"); bx, by = blank(w); mouse.click(coords=(bx, by)); time.sleep(1)
    e2 = val(fe(w, "NewPaidTagName")) == "" and len(cfg()["paid_tags"]) == n0
    rec("11", e1 and e2, "Esc cleared=%s, outside click cleared=%s, tags unchanged=%s, status=%r" % (e1, e2, len(cfg()["paid_tags"]) == n0, fe_text(w, "SaveStatus")))
    # 5
    def add_line(name, rate, how):
        n = fe(w, "NewPaidTagName"); r = fe(w, "NewPaidTagRate")
        cc(n, name); r.click_input(); send_keys("^a"); send_keys("{BACKSPACE}"); typ(rate)
        if how == "name": n.click_input(); send_keys("{ENTER}")
        elif how == "rate": send_keys("{ENTER}")
        else: [b for b in w.descendants(control_type="Button") if b.element_info.name == "Add"][0].click_input()
        time.sleep(1.3)
    add_line("zed", "500", "name"); f1 = focused(w); a1 = cfg()["paid_tags"]
    add_line("zed2", "500", "rate"); f2 = focused(w)
    add_line("zed3", "500", "button"); a3 = cfg()["paid_tags"]
    ent = [x for x in a3 if x["tag"].startswith("zed")]
    shape_ok = len(ent) == 3 and all(set(x.keys()) == set(ent[-1].keys()) and x["monthly_net_eur"] == 500 for x in ent)
    rec("5", shape_ok and not f1 and not f2, "tags=%r focusAfterNameEnter=%r focusAfterRateEnter=%r" % (ent, f1, f2))
    w = win(p)
    # 13
    cc(fe(w, SAL), "14000"); send_keys("{TAB}"); time.sleep(1)
    spin = [b for b in w.descendants(control_type="Button") if "Increase" in (b.element_info.name or "") or "Decrease" in (b.element_info.name or "")]
    v = val(fe(w, SAL))
    rec("13", nums(v) == "14000" and v != "14000" and not spin, "box=%r spinners=%d cfg=%r" % (v, len(spin), salary()))
    rm = [b for b in w.descendants(control_type="Button") if "emove" in (b.element_info.name or "")]
    if rm: rm[-1].click_input(); time.sleep(1.2); print("after Remove focus:", focused(w), "alive", p.poll() is None)
    print("final cfg", cfg())
finally:
    kill(p)
print("SUMMARY"); [print(r) for r in R]
