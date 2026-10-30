# Group QA 2026-10-29 harness: reuses b1d_h with new paths. Scratch MENTOR_ROOT only.
import sys, os, sqlite3, datetime, random, time, json, subprocess
sys.path.insert(0, r"D:\CLAUDE\Planillium\regular\qa")
import b1d_h as H
from b1d_h import *
S = r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\088eee2e-e75e-4a3b-b802-911c91a66e70\scratchpad\g"
H.ROOT = S + r"\root"; H.CFG = H.ROOT + r"\config.json"; H.FIXTURE = S + r"\fixture_config.json"; H.PIDF = S + r"\pid.txt"
H.EXE = r"D:\CLAUDE\Planillium\.claude\worktrees\qa-b1h\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
os.environ["PLANILLIUM_AI_BASE_URL"] = "http://127.0.0.1:59998/"
os.environ["PLANILLIUM_TEST_KEY"] = "x"
def relaunch():
    """start the exe on the existing root (no wipe)"""
    H.kill()
    env = dict(os.environ, MENTOR_ROOT=H.ROOT, MENTOR_INSTANCE_SUFFIX="verify")
    p = subprocess.Popen([H.EXE], env=env); open(H.PIDF, "w").write(str(p.pid)); H._w = None; time.sleep(7)
WINS = ([("Report.docx - Word", "on_plan", "Writing", "Studioshoo")] * 10 + [("Inbox - Outlook", "off_plan", None, None)] * 8
        + [("Family Chat - Telegram", "off_plan", "chat", None)] * 3 + [("Telegram", "off_plan", None, None)] * 2
        + [("Untitled window", "off_plan", None, None)] * 1 + [("Settings", "idle", None, None)] * 1)
def seed(n=2400):
    db = sqlite3.connect(H.ROOT + r"\data\progress.db"); random.seed(1)
    today = datetime.date.today(); rows = []
    for i in range(n):
        d = today - datetime.timedelta(days=i % 60); w_, c, x, t = WINS[i % len(WINS)]
        h = 8 + (i // 60) % 12; m = (i // 720) * 10 % 60
        rows.append((d.isoformat(), f"{h:02d}:{m:02d}", f"{h:02d}:{m+5:02d}", 5, c, w_, x, t))
    db.executemany("INSERT INTO time_diary (date,start_time,end_time,duration_min,category,window,description,tag) VALUES (?,?,?,?,?,?,?,?)", rows)
    db.commit(); db.close()
def dump(path, root=None):
    with open(path, "w", encoding="utf-8") as f:
        for e in (root or w()).descendants():
            try:
                ei = e.element_info; r = e.rectangle()
                f.write(f"{ei.control_type}|{ei.name}|{ei.automation_id}|{r.left},{r.top},{r.width()}x{r.height()}\n")
            except Exception as ex: f.write("ERR " + str(ex) + "\n")
HW = 984382
def attach(h=None):
    global HW
    if h: HW = h
    H._w = Application(backend="uia").connect(handle=HW).window(handle=HW); return H._w
def byname(name, ct=None, exact=True):
    for e in w().descendants(control_type=ct) if ct else w().descendants():
        try:
            n = e.element_info.name
            if (n == name) if exact else (n and name in n): return e
        except Exception: pass
    raise Exception("not found: " + name)
def click(name, ct=None, exact=True, wait=2):
    byname(name, ct, exact).click_input(); time.sleep(wait)
def nav(name): click(name, "ListItem", wait=3)
def wheel_down(n=30):
    r = w().rectangle(); x = r.left + 1100; y = r.top + 600
    for i in range(n): mouse.scroll(coords=(x, y), wheel_dist=-5)
    time.sleep(1.5)
FB = {"cat": "Filter diary by category", "app": "Filter diary by app", "page": "Filter diary by page", "tag": "Filter diary by tag"}
def showing():
    t = texts("Showing"); return t[0] if t else None
def toggle(fb, item, idx=0):
    click(FB[fb], "Button", wait=1.2)
    cs = [e for e in w().descendants(control_type="CheckBox") if e.element_info.name == item]
    cs[idx].click_input(); time.sleep(1.5); send_keys("{ESC}"); time.sleep(1.5)
def tcount():
    b = [e.element_info.name for e in w().descendants(control_type="Button") if (e.element_info.name or "").startswith("Mark on-plan")]
    return b[0] if b else None
def attach_pid():
    import ctypes, ctypes.wintypes as W
    pid = int(open(H.PIDF).read()); u = ctypes.windll.user32; found = []
    cb = ctypes.WINFUNCTYPE(ctypes.c_bool, W.HWND, W.LPARAM)
    def f(h, l):
        p = W.DWORD(); u.GetWindowThreadProcessId(h, ctypes.byref(p)); b = ctypes.create_unicode_buffer(100); u.GetWindowTextW(h, b, 100)
        if p.value == pid and b.value == "Planillium": found.append(h)
        return True
    u.EnumWindows(cb(f), 0); u.ShowWindow(found[0], 3); time.sleep(1); return attach(found[0])
def skip_welcome():
    try: click("Skip", "Button")
    except Exception: pass
