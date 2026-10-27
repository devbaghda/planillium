# QA helpers for B1f (agentic). Real mouse input via SendInput (cursor moved, button held), UIA focus checks,
# safety guards: clicks only inside the app window, keys only while the app is foreground, only our own PID.
import os, sys, time, json, shutil, subprocess, ctypes, datetime, warnings
from ctypes import wintypes
warnings.filterwarnings("ignore")
os.environ["PYTHONIOENCODING"] = "utf-8"
from pywinauto import Application, Desktop
import pywinauto.keyboard as _kb
from pywinauto.uia_defines import IUIA

HERE = os.path.dirname(os.path.abspath(__file__))
WT_APP = os.path.normpath(os.path.join(HERE, "..", "Planillium.App"))
EXE = os.environ.get("QA_EXE") or os.path.join(WT_APP, r"bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe")
SCRATCH = os.environ.get("QA_SCRATCH") or r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\eb855074-fe02-405f-98a4-f990b76f2f3d\scratchpad\qa_b1f"
ROOT = os.path.join(SCRATCH, os.environ.get("QA_ROOTNAME", "root"))
CFG = os.path.join(ROOT, "config.json")
assert "D:\\CLAUDE\\Planillium\\data" not in ROOT and "scratchpad" in ROOT

u = ctypes.windll.user32
PID = [None]; WIN = [None]; PROC = [None]

# ---------------- scratch root / launch ----------------
def make_root(with_fixture=False):
    if os.path.isdir(ROOT): shutil.rmtree(ROOT)
    for d in ("data", r"plans\active", r"plans\queued", r"plans\archive"): os.makedirs(os.path.join(ROOT, d))
    c = {"end_of_day_summary_time": "23:59", "user_name": "QA"}
    if with_fixture:
        c["income"] = {"potential_monthly_net_eur": 2925, "employed": False}
        c["paid_tags"] = [{"tag": "Studioshoo", "label": "Studioshoo", "monthly_net_eur": 1600}]
    json.dump(c, open(CFG, "w"), indent=2)
    d = datetime.date.today().isoformat()
    json.dump({"last_kickoff": d, "last_review": d}, open(os.path.join(ROOT, "data", "winui_state.json"), "w"))

def cfg(): return json.load(open(CFG, encoding="utf-8"))
def salary(): return cfg().get("income", {}).get("potential_monthly_net_eur")
def tags(): return [(p.get("tag"), p.get("monthly_net_eur")) for p in cfg().get("paid_tags", [])]

def launch():
    env = dict(os.environ, MENTOR_ROOT=ROOT, MENTOR_INSTANCE_SUFFIX="verify", MENTOR_PAGE="settings",
               PLANILLIUM_AI_BASE_URL="http://127.0.0.1:9/")
    p = subprocess.Popen([EXE], env=env, cwd=os.path.dirname(EXE))
    PROC[0] = p; PID[0] = p.pid
    for _ in range(40):
        time.sleep(0.5)
        try:
            w = Application(backend="uia").connect(process=p.pid).top_window()
            if w.rectangle().width() > 100: break
        except Exception: pass
    time.sleep(3)
    WIN[0] = Application(backend="uia").connect(process=p.pid).top_window()
    return p

def w(): return WIN[0]
def refresh():
    WIN[0] = Application(backend="uia").connect(process=PID[0]).top_window(); return WIN[0]

def kill():
    p = PROC[0]
    if p is not None:
        try: p.kill(); p.wait(5)
        except Exception: pass

# ---------------- guards ----------------
class Abort(Exception): pass
def fg_ok():
    h = u.GetForegroundWindow(); pid = wintypes.DWORD()
    u.GetWindowThreadProcessId(h, ctypes.byref(pid))
    return pid.value == PID[0]
def bring_front():
    for _ in range(10):
        if fg_ok(): return True
        try: WIN[0].set_focus()
        except Exception: pass
        time.sleep(.3)
    return fg_ok()
def inside(x, y):
    r = WIN[0].rectangle()
    return r.left + 2 <= x <= r.right - 2 and r.top + 2 <= y <= r.bottom - 2

# ---------------- real input (SendInput) ----------------
class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", ctypes.c_long), ("dy", ctypes.c_long), ("mouseData", ctypes.c_ulong), ("dwFlags", ctypes.c_ulong),
                ("time", ctypes.c_ulong), ("dwExtraInfo", ctypes.c_size_t)]
class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", ctypes.c_ushort), ("wScan", ctypes.c_ushort), ("dwFlags", ctypes.c_ulong), ("time", ctypes.c_ulong),
                ("dwExtraInfo", ctypes.c_size_t)]
class _U(ctypes.Union): _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT), ("pad", ctypes.c_byte * 32)]
class INPUT(ctypes.Structure): _fields_ = [("type", ctypes.c_ulong), ("u", _U)]
def _send_mouse(flags, data=0):
    i = INPUT(type=0); i.u.mi = MOUSEINPUT(0, 0, data & 0xffffffff, flags, 0, 0)
    n = u.SendInput(1, ctypes.byref(i), ctypes.sizeof(INPUT))
    if n != 1: raise Abort("SendInput failed")
def move(x, y):
    if not inside(x, y): raise Abort("cursor move outside window %r" % ((x, y),))
    u.SetCursorPos(int(x), int(y)); time.sleep(0.04)
def click(x, y, hold=0.12):
    """Real click: cursor moved, button down, held `hold` seconds, button up."""
    if not inside(x, y): raise Abort("click outside window %r" % ((x, y),))
    if not fg_ok(): raise Abort("app not foreground; refused click")
    move(x, y)
    _send_mouse(0x0002)
    if hold: time.sleep(hold)
    _send_mouse(0x0004)
def rclick(x, y, hold=0.1):
    if not inside(x, y): raise Abort("rclick outside window")
    if not fg_ok(): raise Abort("not fg")
    move(x, y); _send_mouse(0x0008); time.sleep(hold); _send_mouse(0x0010)
def drag(x1, y1, x2, y2, steps=12, hold=0.1):
    if not (inside(x1, y1) and inside(x2, y2)): raise Abort("drag outside window")
    if not fg_ok(): raise Abort("not fg")
    move(x1, y1); _send_mouse(0x0002); time.sleep(hold)
    sw, sh = u.GetSystemMetrics(0), u.GetSystemMetrics(1)
    for s in range(1, steps + 1):
        px = int(x1 + (x2 - x1) * s / steps); py = int(y1 + (y2 - y1) * s / steps)
        i = INPUT(type=0); i.u.mi = MOUSEINPUT(int(px * 65535 / (sw - 1)), int(py * 65535 / (sh - 1)), 0, 0x8001, 0, 0)
        u.SendInput(1, ctypes.byref(i), ctypes.sizeof(INPUT)); time.sleep(0.03)
    time.sleep(hold); _send_mouse(0x0004)
def wheel(x, y, notches=-1):
    if not fg_ok(): raise Abort("not fg")
    move(x, y); _send_mouse(0x0800, 120 * notches)
def keys(s):
    if not fg_ok(): raise Abort("app not foreground; refused keys %r" % s)
    _kb.send_keys(s, with_spaces=True, pause=0.04)
def eclick(el, hold=0.12, dx=None):
    r = el.rectangle()
    x = (r.left + r.right) // 2 if dx is None else r.left + dx
    click(x, (r.top + r.bottom) // 2, hold)

# ---------------- UIA queries ----------------
def focused():
    """(automation id, name, control type name, pid) of the element with keyboard focus."""
    try:
        e = IUIA().iuia.GetFocusedElement()
        return (e.CurrentAutomationId or "", e.CurrentName or "", e.CurrentLocalizedControlType or "", e.CurrentProcessId)
    except Exception as ex:
        return ("?", repr(ex)[:40], "", 0)
def at(x, y):
    try:
        e = IUIA().iuia.ElementFromPoint(wintypes.POINT(int(x), int(y)))
        return (e.CurrentAutomationId or "", (e.CurrentName or "")[:30], e.CurrentLocalizedControlType or "")
    except Exception: return ("?", "", "")
def reveal(getter):
    """Scroll Settings until getter()'s element is fully inside the pane. Zero rect = direction unknown: alternate."""
    for i in range(16):
        el = getter(); P = byid("SectionScroll").rectangle(); r = el.rectangle() if el is not None else None
        if r is not None and r.width() > 0 and r.top >= P.top and r.bottom <= P.bottom: return getter()
        d = -2 if (r is None or r.width() == 0) and i % 6 < 3 else (-2 if r is not None and r.width() > 0 and r.bottom > P.bottom else 2)
        wheel(P.left + 300, P.top + 100, d); time.sleep(.6)
    return getter()
def edits(): return list(WIN[0].descendants(control_type="Edit"))
def fe(sub):
    for e in edits():
        n = e.element_info.name or ""; a = e.element_info.automation_id or ""
        if sub == a or sub in n: return e
def byid(aid, ct=None):
    for attempt in range(3):
        for e in WIN[0].descendants(control_type=ct) if ct else WIN[0].descendants():
            if e.element_info.automation_id == aid: return e
        time.sleep(.5); refresh()
def val(e):
    try: return e.get_value()
    except Exception: return e.window_text()
def nums(s): return "".join(ch for ch in (s or "") if ch.isdigit() or ch in "-.,")
def salary_box(): return fe("Previous salary") or fe("monthly net (EUR)")
def is_salary_focus(f=None):
    f = f or focused()
    sb = salary_box()
    if sb is None: return False
    try: return sb.has_keyboard_focus()
    except Exception: return False
def section(name):
    for li in WIN[0].descendants(control_type="ListItem"):
        if li.element_info.name == name: eclick(li); time.sleep(1.2); return True
    return False
def shot(path):
    from PIL import Image
    hwnd = WIN[0].handle; rc = wintypes.RECT(); u.GetWindowRect(hwnd, ctypes.byref(rc))
    W, H = rc.right - rc.left, rc.bottom - rc.top
    hdc = u.GetDC(0); g = ctypes.windll.gdi32; mdc = g.CreateCompatibleDC(hdc)
    bmp = g.CreateCompatibleBitmap(hdc, W, H); g.SelectObject(mdc, bmp)
    u.PrintWindow(hwnd, mdc, 2)
    class BI(ctypes.Structure): _fields_=[("biSize",ctypes.c_uint32),("biWidth",ctypes.c_int32),("biHeight",ctypes.c_int32),("biPlanes",ctypes.c_uint16),("biBitCount",ctypes.c_uint16),("biCompression",ctypes.c_uint32),("biSizeImage",ctypes.c_uint32),("x",ctypes.c_int32),("y",ctypes.c_int32),("c",ctypes.c_uint32),("i",ctypes.c_uint32)]
    bi = BI(40, W, -H, 1, 32, 0, 0, 0, 0, 0, 0); buf = ctypes.create_string_buffer(W * H * 4)
    g.GetDIBits(mdc, bmp, 0, H, buf, ctypes.byref(bi), 0)
    Image.frombuffer("RGBA", (W, H), buf, "raw", "BGRA", 0, 1).save(path)
    g.DeleteObject(bmp); g.DeleteDC(mdc); u.ReleaseDC(0, hdc)

def dismiss_dialogs():
    for b in WIN[0].descendants(control_type="Button"):
        if b.element_info.automation_id == "PrimaryButton" or b.element_info.name == "Later":
            try: eclick(b); time.sleep(.8)
            except Exception: pass

LOG = []
def log(*a):
    s = " ".join(str(x) for x in a)
    LOG.append(s); print(s.encode("ascii", "replace").decode(), flush=True)
