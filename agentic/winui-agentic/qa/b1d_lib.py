# QA helpers for B1d: safe wrappers (foreground + inside-window guards) over common.py
from common import *
import ctypes
from ctypes import wintypes
from pywinauto import mouse
import pywinauto.keyboard as _kb
from pywinauto.controls.uiawrapper import UIAWrapper
os.environ["PYTHONIOENCODING"] = "utf-8"
PID = [None]; WIN = [None]
def fg_ok():
    h = ctypes.windll.user32.GetForegroundWindow(); pid = wintypes.DWORD()
    ctypes.windll.user32.GetWindowThreadProcessId(h, ctypes.byref(pid))
    return pid.value == PID[0]
def bring_front():
    try:
        w = WIN[0]; w.set_focus()
    except Exception: pass
    for _ in range(10):
        if fg_ok(): return True
        time.sleep(.3)
    return fg_ok()
def inside(x, y):
    r = WIN[0].rectangle()
    return r.left <= x <= r.right and r.top <= y <= r.bottom
class Abort(Exception): pass
def safe_keys(s, **k):
    if not fg_ok(): raise Abort("app not foreground; refused keys %r" % s)
    return _kb.send_keys(s, **k)
send_keys = safe_keys
def typ(s): safe_keys(s, with_spaces=True, pause=0.05)
def sclick(x, y):
    if not inside(x, y): raise Abort("click outside window %r" % ((x, y),))
    mouse.click(coords=(x, y))
_orig = UIAWrapper.click_input
def _ci(self, *a, **k):
    r = self.rectangle(); cx, cy = (r.left + r.right)//2, (r.top + r.bottom)//2
    if not inside(cx, cy): raise Abort("element click outside window")
    if not fg_ok(): bring_front()
    return _orig(self, *a, **k)
UIAWrapper.click_input = _ci
def edits(w): return list(w.descendants(control_type="Edit"))
def fe(w, sub):
    for e in edits(w):
        if sub in (e.element_info.name or "") or sub == e.element_info.automation_id: return e
def val(e):
    try: return e.get_value()
    except Exception: return e.window_text()
def nums(s): return "".join(ch for ch in s if ch not in "     ")
def cc(e, s):
    e.click_input(); time.sleep(.25); safe_keys("^a"); safe_keys("{BACKSPACE}")
    if s: typ(s)
def focused_any(w):
    """name of focused element (edit/button/list), or None"""
    out = []
    for e in w.descendants():
        try:
            if e.has_keyboard_focus(): out.append((e.element_info.control_type, e.element_info.name or e.element_info.automation_id))
        except Exception: pass
    return out
def focused_edits(w):
    r = []
    for e in edits(w):
        try:
            if e.has_keyboard_focus(): r.append(e.element_info.name or e.element_info.automation_id)
        except Exception: pass
    return r
def texts(w):
    out = []
    for t in w.descendants(control_type="Text"):
        try:
            n = t.element_info.name or ""
            if n and t.rectangle().width() > 0: out.append((n, t.element_info.automation_id, t.rectangle().top))
        except Exception: pass
    return out
def msgs(w, keys=("negative", "more than 0", "number", "name", "amount", "Saved")):
    return [(n, a) for n, a, _ in texts(w) if any(k.lower() in n.lower() for k in keys)]
def readouts(w): return [n for n, a, _ in texts(w) if n.startswith("=")]
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
R = []
def rec(line, ok, ev):
    R.append((line, "PASS" if ok else "FAIL", ev)); print(("%s: %s -- %s" % (line, "PASS" if ok else "FAIL", ev)).encode("ascii","replace").decode(), flush=True)
def start():
    make_root(); p = launch(); PID[0] = p.pid
    w = win(p); WIN[0] = w
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception as ex: print("no welcome", ex)
    time.sleep(1); w = win(p); WIN[0] = w
    # welcome/popups: dismiss "Later" if present
    for b in w.descendants(control_type="Button"):
        if b.element_info.name == "Later":
            b.click_input(); time.sleep(.6)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1.5); w = win(p); WIN[0] = w
    bring_front()
    return p, w
def blank(w):
    sal = fe(w, "Previous salary").rectangle()
    return (sal.right + 300, sal.top + 20)
