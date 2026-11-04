# G5 memory measurement (agentic). Reusable before/after: same root, same data, same steps.
# Usage (from any folder):  set PYTHONIOENCODING=utf-8 ; py qa/g5_measure.py <cmd> [label]
#   build   - fresh scratch root, launch once on the EMPTY DB (measures empty idle), kill, fill ~9300 diary rows
#   large   - launch on the filled root and run the measured scenario
#   all     - build then large
# Output: printed table + qa/g5_scratch/g5_<label>.json (label defaults to "run").
# Safety: scratch root under qa/g5_scratch only; only the PID this script starts is touched/killed;
# UI is driven by UIA patterns (no mouse/keyboard), so the user's desktop is not disturbed.
import os, sys, time, json, shutil, subprocess, ctypes, datetime, random, sqlite3, warnings
from ctypes import wintypes
warnings.filterwarnings("ignore")

HERE = os.path.dirname(os.path.abspath(__file__))
APP = os.path.normpath(os.path.join(HERE, "..", "Planillium.App"))
EXE = os.environ.get("G5_EXE") or os.path.join(APP, r"bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe")
SCR = os.path.join(HERE, "g5_scratch")
ROOT = os.path.join(SCR, "root")
DB = os.path.join(ROOT, "data", "progress.db")
assert os.path.normcase("\\agentic\\winui-agentic\\qa\\g5_scratch\\") in os.path.normcase(ROOT + "\\")
assert "regular" not in os.path.normcase(EXE).split(os.sep)
# G5_PAGES (fill): distinct page titles, default 2600; G5_SHORT=1 (large): stop after step D.
IDLE_MIN = float(os.environ.get("G5_IDLE_MIN", "4"))   # idle minutes with live refresh on Reports
SETTLE = float(os.environ.get("G5_SETTLE", "6"))       # seconds to wait before each reading

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
psapi = ctypes.WinDLL("psapi", use_last_error=True)

class PMCEX(ctypes.Structure):
    _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD), ("PeakWorkingSetSize", ctypes.c_size_t),
                ("WorkingSetSize", ctypes.c_size_t), ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPagedPoolUsage", ctypes.c_size_t), ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t), ("PagefileUsage", ctypes.c_size_t),
                ("PeakPagefileUsage", ctypes.c_size_t), ("PrivateUsage", ctypes.c_size_t)]

def mem(pid):
    """(private working set MB, private bytes MB, working set MB) of pid. Private WS = working-set pages
    not marked Shared (QueryWorkingSet), which is Task Manager's 'Memory' column."""
    h = k32.OpenProcess(0x0400 | 0x0010, False, pid)  # QUERY_INFORMATION | VM_READ
    if not h: raise OSError(ctypes.get_last_error())
    try:
        c = PMCEX(); c.cb = ctypes.sizeof(c)
        psapi.GetProcessMemoryInfo(h, ctypes.byref(c), c.cb)
        n = c.WorkingSetSize // 4096 + 65536
        while True:
            buf = (ctypes.c_size_t * (n + 1))()
            if psapi.QueryWorkingSet(h, buf, ctypes.sizeof(buf)): break
            if ctypes.get_last_error() != 24: raise OSError(ctypes.get_last_error())
            n = buf[0] + 65536
        cnt = buf[0]
        arr = memoryview(buf).cast("B").cast("Q")[1:cnt + 1]
        shared = sum(1 for e in arr if e & 0x100)
        priv_ws = (cnt - shared) * 4096
        return round(priv_ws / 2**20, 1), round(c.PrivateUsage / 2**20, 1), round(c.WorkingSetSize / 2**20, 1)
    finally:
        k32.CloseHandle(h)

ROWS = []
def reading(pid, step, note=""):
    time.sleep(SETTLE)
    p, pb, ws = mem(pid)
    ROWS.append({"step": step, "private_ws_mb": p, "private_bytes_mb": pb, "ws_mb": ws, "note": note,
                 "t": datetime.datetime.now().strftime("%H:%M:%S")})
    print("%-34s privWS %7.1f  privBytes %7.1f  WS %7.1f  %s" % (step, p, pb, ws, note), flush=True)

# ---------------- scratch root ----------------
def make_root():
    if os.path.isdir(ROOT): shutil.rmtree(ROOT)
    for d in ("data", r"plans\active", r"plans\queued", r"plans\archive"): os.makedirs(os.path.join(ROOT, d))
    today = datetime.date.today()
    cfg = {"end_of_day_summary_time": "23:59", "user_name": "G5", "diary_retention_days": 365,
           "income": {"potential_monthly_net_eur": 2925, "employed": False},
           "paid_tags": [{"tag": "studioshoo", "label": "Studioshoo", "monthly_net_eur": 1600}]}
    json.dump(cfg, open(os.path.join(ROOT, "config.json"), "w"), indent=2)
    d = today.isoformat()
    json.dump({"last_kickoff": d, "last_review": d}, open(os.path.join(ROOT, "data", "winui_state.json"), "w"))
    start = today - datetime.timedelta(days=60)
    tasks = [{"day": i, "task": "G5 task %d" % i, "detail": "d", "category": "c", "duration_min": 60} for i in range(1, 121)]
    plan = {"id": "g5-plan", "name": "G5 plan", "color": "#0a84ff", "start_date": start.isoformat(), "total_days": 120,
            "phases": [{"phase": 1, "name": "One", "tasks": tasks}]}
    json.dump(plan, open(os.path.join(ROOT, "plans", "active", "g5-plan.json"), "w"), indent=2)

def fill_db(days=300, per_day=31, seed=5):
    rnd = random.Random(seed)
    today = datetime.date.today()
    apps = ["Google Chrome", "Telegram", "Visual Studio Code", "Microsoft Word", "Microsoft Excel", "File Explorer",
            "Slack", "Figma", "Notion", "Spotify", "Discord", "Outlook", "Zoom", "Firefox", "Obsidian", "Steam",
            "Photoshop", "PowerShell", "Terminal", "Acrobat Reader", "WhatsApp", "TickTick", "Calculator", "Paint", "VLC"]
    sites = ["GitHub", "YouTube", "Coursera", "Stack Overflow", "Gmail", "LinkedIn", "Reddit", "Wikipedia",
             "Google Docs", "Booking", "Amazon", "Netflix", "Medium", "Habr", "Twitch"]
    pages = ["%s - item %d" % (rnd.choice(sites), n) for n in range(int(os.environ.get("G5_PAGES", "2600")))]  # distinct pages (default 2600)
    cats = ["on_plan"] * 40 + ["off_plan"] * 25 + ["neutral"] * 20 + ["idle"] * 5 + ["paid"] * 10
    tags = [None] * 70 + ["routine"] * 8 + ["documents"] * 6 + ["selfdev"] * 8 + ["procrastination"] * 8
    con = sqlite3.connect(DB)
    rows, score, n = [], [], 0
    for back in range(days, 0, -1):
        day = today - datetime.timedelta(days=back)
        t = 6 * 60
        for _ in range(per_day):
            dur = 27
            cat = rnd.choice(cats)
            app = rnd.choice(apps)
            win = "%s - %s" % (rnd.choice(pages), app) if rnd.random() < 0.85 else app
            tag = "studioshoo" if cat == "paid" else rnd.choice(tags)
            desc = ("note %d" % rnd.randint(1, 500)) if rnd.random() < 0.15 else None
            s = "%02d:%02d" % divmod(t, 60); e = "%02d:%02d" % divmod(t + dur, 60)
            rows.append((day.isoformat(), s, e, dur, cat, win, desc, tag)); t += dur; n += 1
        score.append((day.isoformat() + "T21:00:00", day.isoformat(), rnd.randint(-20, 40), "daily_score", ""))
    con.executemany("INSERT INTO time_diary(date,start_time,end_time,duration_min,category,window,description,tag) "
                    "VALUES (?,?,?,?,?,?,?,?)", rows)
    con.executemany("INSERT OR IGNORE INTO score_ledger(ts,date,delta,reason,detail) VALUES (?,?,?,?,?)", score)
    for back in range(1, 40, 2):
        con.execute("INSERT OR IGNORE INTO task_completions(plan_id,plan_day,task_text,completed,completed_at,last_updated) "
                    "VALUES ('g5-plan',?,?,1,?,?)", (61 - back, "G5 task %d" % (61 - back),
                    (today - datetime.timedelta(days=back)).isoformat(), (today - datetime.timedelta(days=back)).isoformat()))
    con.commit()
    print("filled", n, "diary rows; total", con.execute("select count(*) from time_diary").fetchone()[0], flush=True)
    con.close()

# ---------------- app ----------------
PROC = [None]
def launch(page="today"):
    from pywinauto import Application
    env = dict(os.environ, MENTOR_ROOT=ROOT, MENTOR_INSTANCE_SUFFIX="verify", MENTOR_PAGE=page,
               PLANILLIUM_AI_BASE_URL="http://127.0.0.1:9/")
    p = subprocess.Popen([EXE], env=env, cwd=os.path.dirname(EXE)); PROC[0] = p
    for _ in range(60):
        time.sleep(0.5)
        try:
            w = Application(backend="uia").connect(process=p.pid).top_window()
            if w.rectangle().width() > 100: break
        except Exception: pass
    time.sleep(4)
    return p.pid, Application(backend="uia").connect(process=p.pid).top_window()

def kill():
    p = PROC[0]
    if p is not None:
        try: p.kill(); p.wait(10)
        except Exception: pass
    PROC[0] = None

def find(win, ctype, pred, tries=6):
    for _ in range(tries):
        for e in win.descendants(control_type=ctype):
            if pred(e.element_info.name or ""): return e
        time.sleep(1)
    raise RuntimeError("not found: %s" % ctype)

def nav(win, name):
    li = find(win, "ListItem", lambda n: n == name)
    li.select()

def period(win, label):
    find(win, "RadioButton", lambda n: n == label).select()

def dismiss(win):
    for b in win.descendants(control_type="Button"):
        if b.element_info.automation_id == "PrimaryButton" or b.element_info.name == "Later":
            try: b.invoke(); time.sleep(.8)
            except Exception: pass

def run_build():
    make_root()
    pid, win = launch("today")
    try:
        dismiss(win)
        time.sleep(10)
        reading(pid, "empty: Today idle")
        nav(win, "Reports"); reading(pid, "empty: Reports Week")
        time.sleep(30); reading(pid, "empty: Reports +30s")
    finally:
        kill()
    time.sleep(2)
    fill_db()

def run_large():
    # UIA tree walks make WinUI create automation peers for every element they pass, which itself costs
    # memory on a big tree. So every control that survives Render() (nav items, the four period radios) is
    # found ONCE while the diary is still small and reused; only the rebuilt "All time" box and "Show all"
    # link are searched for, each while the list holds 40 rows. Same steps before and after the change.
    pid, win = launch("today")
    try:
        dismiss(win)
        time.sleep(10)
        reading(pid, "A Today idle")
        nav_today = find(win, "ListItem", lambda n: n == "Today")
        find(win, "ListItem", lambda n: n == "Reports").select()
        reading(pid, "B Reports Week, diary=today")
        radios = {lab: find(win, "RadioButton", lambda n, l=lab: n == l) for lab in ("Day", "Week", "Month", "Year")}
        find(win, "CheckBox", lambda n: n.startswith("Apply filters across")).toggle()
        reading(pid, "C All time, default rows shown")
        for lab in ("Day", "Week", "Month", "Year"):
            radios[lab].select(); reading(pid, "D %s, default rows" % lab)
        if os.environ.get("G5_SHORT"): return   # steps A-D only (quick attribution runs)
        radios["Week"].select()
        for i in (1, 2):
            time.sleep(60 - SETTLE); reading(pid, "E idle Week default rows +%d min" % i)
        status = find(win, "Text", lambda n: n.startswith("Showing "))
        note = status.element_info.name
        find(win, "Hyperlink", lambda n: n.startswith("Show all")).invoke()
        reading(pid, "F Show all", note)
        for lab in ("Day", "Week", "Month", "Year"):
            radios[lab].select(); reading(pid, "G %s, show all" % lab)
        for r in range(3):
            for lab in ("Day", "Week", "Month", "Year"):
                radios[lab].select(); time.sleep(2)
        radios["Week"].select(); reading(pid, "G after 3 more period cycles, Week")
        t0 = time.time(); i = 0
        while time.time() - t0 < IDLE_MIN * 60:
            time.sleep(60 - SETTLE); i += 1
            reading(pid, "H idle Week show all +%d min" % i)
        nav_today.select(); reading(pid, "I Today (Reports cached)")
        time.sleep(60 - SETTLE); reading(pid, "I Today +1 min")
    finally:
        kill()

if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "all"
    label = sys.argv[2] if len(sys.argv) > 2 else "run"
    if cmd in ("build", "all"): run_build()
    if cmd in ("large", "all"): run_large()
    if cmd == "fill": fill_db()
    out = os.path.join(SCR, "g5_%s_%s.json" % (label, cmd))
    json.dump(ROWS, open(out, "w"), indent=1)
    print("wrote", out)
