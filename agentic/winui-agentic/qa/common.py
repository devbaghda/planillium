import os, sys, time, json, shutil, subprocess, warnings
warnings.filterwarnings("ignore")
from pywinauto import Application, Desktop
from pywinauto.keyboard import send_keys

WT = "D:/CLAUDE/Planillium/.claude/worktrees/qa-ag/agentic/winui-agentic"
EXE = WT + "/Planillium.App/bin/x64/Debug/net8.0-windows10.0.19041.0/Planillium.App.exe"
ROOT = "D:/CLAUDE/Planillium/.claude/worktrees/qa-ag/_scratch_root"
CFG = ROOT + "/config.json"

def make_root():
    if os.path.isdir(ROOT): shutil.rmtree(ROOT)
    for d in ("data", r"plans\active", r"plans\queued", r"plans\archive"): os.makedirs(os.path.join(ROOT, d))
    json.dump({"end_of_day_summary_time": "23:59", "income": {"potential_monthly_net_eur": 2925, "employed": False},
               "paid_tags": [{"tag": "Studioshoo", "label": "Studioshoo", "monthly_net_eur": 1600}]},
              open(CFG, "w"), indent=2)

    import datetime
    d = datetime.date.today().isoformat()
    json.dump({"last_kickoff": d, "last_review": d}, open(ROOT + "/data/winui_state.json", "w"))

def cfg():
    return json.load(open(CFG, encoding="utf-8"))
def salary(): return cfg().get("income", {}).get("potential_monthly_net_eur")
def tagrate(): return [p["monthly_net_eur"] for p in cfg().get("paid_tags", [])]

def launch():
    env = dict(os.environ, MENTOR_ROOT=ROOT, MENTOR_INSTANCE_SUFFIX="verify", MENTOR_PAGE="settings",
               PLANILLIUM_AI_BASE_URL="http://127.0.0.1:9/")
    p = subprocess.Popen([EXE], env=env, cwd=os.path.dirname(EXE))
    time.sleep(8)
    return p

def win(p):
    app = Application(backend="uia").connect(process=p.pid)
    return app.top_window()

def kill(p):
    try: p.kill()
    except Exception: pass

def dump(w, depth=14, skip=("Pane","Group")):
    def walk(e, d):
        if d > depth: return
        try: ct = e.element_info.control_type; n = e.element_info.name; aid = e.element_info.automation_id
        except Exception: return
        if (n or ct == "Edit") and ct not in skip:
            print(("  "*d + ct + " | " + repr(n)[:100] + " | " + aid).encode("ascii","replace").decode())
        for c in e.children(): walk(c, d+1)
    walk(w, 0)
