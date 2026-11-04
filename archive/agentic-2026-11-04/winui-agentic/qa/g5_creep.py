# G5 round 2: where does the slow private-bytes creep come from? One scenario per launch, sampled
# every G5C_STEP seconds for G5C_MIN minutes, on the filled scratch root made by g5_measure.py build.
# Usage: set PYTHONIOENCODING=utf-8 ; py qa/g5_creep.py <scenario> [label]
#   today          - stay on Today (no Reports at all)
#   rep_today      - Reports Week, diary scope = today (live refresh reads ~today's rows)
#   rep_alltime    - Reports Week, "Apply filters across all time" on (live refresh reads ~9300 rows)
#   switch         - Reports, all time on, Day/Week/Month/Year switch every 3 s
# Each sample: private WS, private bytes, and the committed private memory split into the .NET GC
# reservation (largest private reservation, >= 1 GB) vs everything else (native heaps, WinUI, SQLite).
# Safety: same as g5_measure.py (scratch root, only the PID started here, UIA patterns only).
import os, sys, time, json, ctypes, datetime
from ctypes import wintypes
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import g5_measure as g

MINUTES = float(os.environ.get("G5C_MIN", "12"))
STEP = float(os.environ.get("G5C_STEP", "30"))

class MBI(ctypes.Structure):
    _fields_ = [("BaseAddress", ctypes.c_void_p), ("AllocationBase", ctypes.c_void_p),
                ("AllocationProtect", wintypes.DWORD), ("PartitionId", wintypes.WORD),
                ("RegionSize", ctypes.c_size_t), ("State", wintypes.DWORD), ("Protect", wintypes.DWORD),
                ("Type", wintypes.DWORD)]

MEM_COMMIT, MEM_PRIVATE = 0x1000, 0x20000
g.k32.VirtualQueryEx.argtypes = [wintypes.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
g.k32.VirtualQueryEx.restype = ctypes.c_size_t
g.k32.OpenProcess.restype = wintypes.HANDLE

def split(pid):
    """(gc_commit MB, other private commit MB, gc reservation GB) via VirtualQueryEx."""
    h = g.k32.OpenProcess(0x0400 | 0x0010, False, pid)
    try:
        addr, groups = 0, {}
        m = MBI()
        while addr < 0x7FFFFFFFFFFF:
            if not g.k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(m), ctypes.sizeof(m)): break
            if m.Type == MEM_PRIVATE and m.AllocationBase:
                gr = groups.setdefault(m.AllocationBase, [0, 0])
                gr[0] += m.RegionSize
                if m.State == MEM_COMMIT: gr[1] += m.RegionSize
            addr = (m.BaseAddress or 0) + m.RegionSize
        big = [k for k, (res, com) in groups.items() if res >= 2**30]
        gc_k = max(big, key=lambda k: groups[k][0]) if big else None
        gc_c = groups[gc_k][1] if gc_k else 0
        other = sum(c for k, (r, c) in groups.items() if k != gc_k)
        return round(gc_c / 2**20, 1), round(other / 2**20, 1), round((groups[gc_k][0] if gc_k else 0) / 2**30, 1)
    finally:
        g.k32.CloseHandle(h)

ROWS = []
def sample(pid, t0, note=""):
    p, pb, ws = g.mem(pid)
    gc, other, res = split(pid)
    r = {"min": round((time.time() - t0) / 60, 2), "priv_ws": p, "priv_bytes": pb, "gc_commit": gc,
         "other_commit": other, "gc_res_gb": res, "note": note, "t": datetime.datetime.now().strftime("%H:%M:%S")}
    ROWS.append(r)
    print("%6.2f min  privWS %6.1f  privBytes %6.1f  gcCommit %6.1f  otherCommit %6.1f  %s" %
          (r["min"], p, pb, gc, other, note), flush=True)

def main(scen):
    pid, win = g.launch("today")
    try:
        g.dismiss(win); time.sleep(10)
        radios = None
        if scen != "today":
            g.find(win, "ListItem", lambda n: n == "Reports").select(); time.sleep(4)
            radios = {lab: g.find(win, "RadioButton", lambda n, l=lab: n == l) for lab in ("Day", "Week", "Month", "Year")}
            if scen in ("rep_alltime", "switch"):
                g.find(win, "CheckBox", lambda n: n.startswith("Apply filters across")).toggle(); time.sleep(4)
        t0 = time.time(); sample(pid, t0, "start " + scen)
        nxt, i = t0 + STEP, 0
        order = ("Day", "Week", "Month", "Year")
        while time.time() - t0 < MINUTES * 60:
            if scen == "switch":
                radios[order[i % 4]].select(); i += 1; time.sleep(3)
            else:
                time.sleep(1)
            if time.time() >= nxt:
                sample(pid, t0, ("switches=%d" % i) if scen == "switch" else ""); nxt += STEP
    finally:
        g.kill()

if __name__ == "__main__":
    scen = sys.argv[1]; label = sys.argv[2] if len(sys.argv) > 2 else "run"
    main(scen)
    out = os.path.join(g.SCR, "g5c_%s_%s.json" % (label, scen))
    json.dump(ROWS, open(out, "w"), indent=1); print("wrote", out)
