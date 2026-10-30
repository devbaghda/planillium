import sys, time, os, ctypes
from ctypes import wintypes
from pywinauto import Application, mouse, keyboard
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\g12"
PID=int(open(S+r"\pid.txt").read())
U=ctypes.windll.user32
def w(): return Application(backend="uia").connect(process=PID).top_window()
def fgpid():
    h=U.GetForegroundWindow(); p=wintypes.DWORD(); U.GetWindowThreadProcessId(h,ctypes.byref(p)); return p.value
def guard():
    if fgpid()!=PID:
        try: w().set_focus(); time.sleep(0.4)
        except Exception: pass
    if fgpid()!=PID: raise SystemExit(f"ABORT: foreground pid {fgpid()} is not ours {PID}")
def click(e=None, coords=None):
    guard()
    if e is not None: e.click_input()
    else: mouse.click(coords=coords)
    time.sleep(0.5)
def keys(k):
    guard(); keyboard.send_keys(k, with_spaces=True, pause=0.03); time.sleep(0.6)
def dump(fn, root=None, skip=("Pane","Group","Custom")):
    root=root or w(); out=[]
    for e in root.descendants():
        i=e.element_info
        if i.control_type in skip and not i.automation_id: continue
        r=i.rectangle
        out.append(f"{i.control_type}|{i.name}|{i.automation_id}|{r.left},{r.top},{r.width()}x{r.height()}")
    open(os.path.join(S,fn),"w",encoding="utf-8").write("\n".join(out)); return out
def focused():
    from pywinauto.uia_defines import IUIA
    f=IUIA().iuia.GetFocusedElement(); return f"{f.CurrentControlType}|{f.CurrentName}|{f.CurrentAutomationId}"
def find(name=None, ct=None, aid=None, starts=False):
    for e in w().descendants():
        i=e.element_info
        if ct and i.control_type!=ct: continue
        if aid and i.automation_id!=aid: continue
        if name is not None and not ((i.name or "").startswith(name) if starts else i.name==name): continue
        return e
    raise Exception(f"not found {name} {ct} {aid}")
def val(e):
    try: return e.iface_value.CurrentValue
    except Exception: return e.window_text()
def status():
    try: return find(aid="SaveStatus").window_text()
    except Exception as x: return f"?{x}"
def texts():
    return [e.element_info.name for e in w().descendants(control_type="Text") if e.element_info.name]
def shot(fn):
    import win32gui, win32ui
    from PIL import Image
    hwnd=w().handle; l,t,r,b=win32gui.GetWindowRect(hwnd); W,H=r-l,b-t
    dc=win32gui.GetWindowDC(hwnd); mdc=win32ui.CreateDCFromHandle(dc); sdc=mdc.CreateCompatibleDC()
    bmp=win32ui.CreateBitmap(); bmp.CreateCompatibleBitmap(mdc,W,H); sdc.SelectObject(bmp)
    U.PrintWindow(hwnd, sdc.GetSafeHdc(), 2)
    i=bmp.GetInfo(); im=Image.frombuffer('RGB',(i['bmWidth'],i['bmHeight']),bmp.GetBitmapBits(True),'raw','BGRX',0,1)
    im.save(os.path.join(S,fn)); win32gui.DeleteObject(bmp.GetHandle()); sdc.DeleteDC(); mdc.DeleteDC(); win32gui.ReleaseDC(hwnd,dc)
