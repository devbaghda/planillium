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

    def st(tag): print(tag, [(e.element_info.name[:8], val(e), e.rectangle().top) for e in edits(w)], hints(w), focused(w), flush=True)
    def st(tag): print(tag, [(e.element_info.name[:8], val(e), e.rectangle().top) for e in edits(w)], hints(w), focused(w), flush=True)
    cc(fe(w, SAL), "-500"); time.sleep(.8)
    typ("7"); time.sleep(.5); typ("0"); time.sleep(.6)
    send_keys("{TAB}"); time.sleep(1.0); st("after tab")
    fe(w, SAL).click_input(); send_keys("{HOME}"); send_keys("{DELETE}"); time.sleep(.8); st("deleted minus")
    send_keys("{ENTER}"); time.sleep(1.0); st("enter")
    cc(fe(w, SAL), "-8"); time.sleep(.5); bx, by = blank(w); mouse.click(coords=(bx, by)); time.sleep(1.0); st("blank")
    e = fe(w, SAL); print(e.rectangle())
    e.click_input(); time.sleep(.25); st("clicked")
    send_keys("^a"); send_keys("{BACKSPACE}"); typ("1234"); st("typed")
finally:
    kill(p)
