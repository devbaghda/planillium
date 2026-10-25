import sys, time, os
from pywinauto import Application
PID = int(os.environ.get("QPID", "31460"))
def app():
    return Application(backend="uia").connect(process=PID)
def win():
    return app().top_window()
def find(name=None, ct=None, aid=None, parent=None, **kw):
    root = parent or win()
    spec = dict()
    if name is not None: spec["title"] = name
    if ct: spec["control_type"] = ct
    if aid: spec["auto_id"] = aid
    return root.child_window(**spec)
def alltexts(depth=12, parent=None):
    out = []
    def walk(e, d):
        if d > depth: return
        try:
            ct = e.element_info.control_type; n = e.element_info.name; aid = e.element_info.automation_id
        except Exception: return
        if n or ct in ("Edit",):
            out.append((d, ct, n, aid))
        for c in e.children(): walk(c, d + 1)
    walk(parent or win(), 0)
    return out
def show(depth=12, parent=None, skip=("Pane","Group","Custom")):
    for d, ct, n, aid in alltexts(depth, parent):
        if ct in skip: continue
        print(("  " * d + ct + " | " + repr(n)[:150] + " | " + aid).encode("utf-8","replace").decode("utf-8","replace"))
