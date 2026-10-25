# B1d QA harness (regular). Scratch MENTOR_ROOT only; launches its own Debug exe from the QA worktree.
import os, sys, time, json, shutil, subprocess
from pywinauto import Application, mouse
from pywinauto.keyboard import send_keys
from pywinauto.uia_defines import IUIA

S = r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\cf7cbaac-b682-4ea0-9f95-3643c4f351b6\scratchpad"
ROOT = S + r"\b1d\root"
CFG = ROOT + r"\config.json"
FIXTURE = S + r"\b1d\fixture_config.json"
EXE = r"D:\CLAUDE\Planillium\.claude\worktrees\qa-b1d\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
PIDF = S + r"\b1d\pid.txt"
SHOTS = r"D:\CLAUDE\Planillium\regular\qa"

SAL = "Previous salary — monthly net (€)"; TAG = "Studioshoo — monthly net (€)"
NEW = "Monthly net (€)"; NAME = "New payable tag"

def log(*a): print(*a, flush=True)

def kill():
    if os.path.exists(PIDF):
        pid = open(PIDF).read().strip()
        if pid: subprocess.run(["taskkill", "/PID", pid, "/F"], capture_output=True)
        os.remove(PIDF); time.sleep(1.5)

def launch(seed=True):
    kill()
    if os.path.exists(ROOT): shutil.rmtree(ROOT)
    os.makedirs(ROOT + r"\data"); os.makedirs(ROOT + r"\plans\active")
    shutil.copy(FIXTURE, CFG)
    # pre-mark today's kickoff + review as done so the timed prompts don't steal focus mid-run
    import datetime
    if not seed: today = None
    else: today = datetime.date.today().isoformat()
    if today: json.dump({"last_kickoff": today, "last_review": today, "name_asked": True},
              open(ROOT + r"\data\winui_state.json", "w"))
    else: json.dump({"name_asked": True}, open(ROOT + r"\data\winui_state.json", "w"))
    env = dict(os.environ, MENTOR_ROOT=ROOT, MENTOR_INSTANCE_SUFFIX="verify")
    p = subprocess.Popen([EXE], env=env)
    open(PIDF, "w").write(str(p.pid))
    time.sleep(7)
    return p.pid

_w = None
def w():
    global _w
    if _w is None:
        _w = Application(backend="uia").connect(process=int(open(PIDF).read())).top_window()
    return _w

def cfg():
    c = json.load(open(CFG, encoding="utf-8"))
    return c["income"]["monthly_net_eur"], {t["tag"]: t["monthly_net_eur"] for t in c.get("paid_tags", [])}

def edit(title):
    for e in w().descendants(control_type="Edit"):
        if e.element_info.name == title: return e
    raise Exception("no edit " + title)
def val(t):
    try: return edit(t).get_value()
    except Exception as ex: return "ERR " + str(ex)
def foc():
    try:
        f = IUIA().get_focused_element()
        return (str(f.CurrentControlType), f.CurrentName, f.CurrentAutomationId)
    except Exception as ex: return ("err", str(ex), "")
def texts(sub):
    out = []
    for e in w().descendants(control_type="Text"):
        try:
            n = e.element_info.name
            if n and sub in n: out.append(n)
        except Exception: pass
    return out
def red():
    # red line texts (negative / refusal) + status strip
    keys = ("negative", "needs", "Give the new tag", "already a payable")
    out = []
    for e in w().descendants(control_type="Text"):
        try:
            n = e.element_info.name
            if n and any(k in n for k in keys):
                r = e.rectangle()
                out.append((n, r.width() > 0))
        except Exception: pass
    return out
def saved(): return texts("Saved")
def hourly():
    return [n for n in texts("/h")]
def typein(t, s, clear=True):
    e = edit(t); e.click_input(); time.sleep(.3)
    if clear: send_keys("^a{BACKSPACE}")
    if s: send_keys(s, with_spaces=True)
    time.sleep(.5)
def snap(): return cfg(), [val(t) for t in (SAL, TAG, NEW, NAME)]
def goto():
    w().child_window(auto_id="SettingsItem").click_input(); time.sleep(1.5)
    w().child_window(title="Income", control_type="ListItem").click_input(); time.sleep(1.5)
def nine(label):
    """Type 9 and report whether any box/config changed (the stray-cursor probe)."""
    b = snap(); f = foc(); send_keys("9"); time.sleep(.6); a = snap()
    same = (a == b)
    log(f"  [{label}] focus={f} type9-unchanged={same}" + ("" if same else f" BEFORE={b} AFTER={a}"))
    if not same:
        # undo the stray 9 so later steps start clean
        send_keys("{BACKSPACE}"); time.sleep(.3)
    return same, f
def shot(name):
    import ctypes, ctypes.wintypes
    from PIL import Image
    hwnd = w().handle
    r = ctypes.wintypes.RECT(); ctypes.windll.user32.GetWindowRect(hwnd, ctypes.byref(r))
    wd, ht = r.right - r.left, r.bottom - r.top
    hdc = ctypes.windll.user32.GetWindowDC(hwnd); mdc = ctypes.windll.gdi32.CreateCompatibleDC(hdc)
    bmp = ctypes.windll.gdi32.CreateCompatibleBitmap(hdc, wd, ht); ctypes.windll.gdi32.SelectObject(mdc, bmp)
    ctypes.windll.user32.PrintWindow(hwnd, mdc, 2)
    class BIH(ctypes.Structure):
        _fields_ = [("biSize", ctypes.c_uint32), ("biWidth", ctypes.c_int32), ("biHeight", ctypes.c_int32), ("biPlanes", ctypes.c_uint16), ("biBitCount", ctypes.c_uint16), ("biCompression", ctypes.c_uint32), ("biSizeImage", ctypes.c_uint32), ("a", ctypes.c_int32), ("b", ctypes.c_int32), ("c", ctypes.c_uint32), ("d", ctypes.c_uint32)]
    bih = BIH(ctypes.sizeof(BIH), wd, -ht, 1, 32, 0, 0, 0, 0, 0, 0)
    buf = ctypes.create_string_buffer(wd * ht * 4)
    ctypes.windll.gdi32.GetDIBits(mdc, bmp, 0, ht, buf, ctypes.byref(bih), 0)
    Image.frombuffer("RGBA", (wd, ht), buf, "raw", "BGRA", 0, 1).save(SHOTS + "\\" + name)
    ctypes.windll.gdi32.DeleteObject(bmp); ctypes.windll.gdi32.DeleteDC(mdc); ctypes.windll.user32.ReleaseDC(hwnd, hdc)
