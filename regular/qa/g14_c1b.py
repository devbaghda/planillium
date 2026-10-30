import ctypes, time, win32gui, win32process, win32ui, win32con
from PIL import Image
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\g14"
PID=int(open(S+r"\pid.txt").read())
res=[]
win32gui.EnumWindows(lambda h,_: res.append(h) if win32process.GetWindowThreadProcessId(h)[1]==PID and win32gui.IsWindowVisible(h) else None,None)
h=res[0]; l,t,r,b=win32gui.GetWindowRect(h); W,H=r-l,b-t
dc=win32gui.GetWindowDC(h); m=win32ui.CreateDCFromHandle(dc); sd=m.CreateCompatibleDC(); bm=win32ui.CreateBitmap(); bm.CreateCompatibleBitmap(m,W,H); sd.SelectObject(bm)
ctypes.windll.user32.PrintWindow(h, sd.GetSafeHdc(), 2); i=bm.GetInfo()
Image.frombuffer('RGB',(i['bmWidth'],i['bmHeight']),bm.GetBitmapBits(True),'raw','BGRX',0,1).save(S+r"\c1_msg.png")
ok=[]; win32gui.EnumChildWindows(h, lambda c,_: ok.append(c) if win32gui.GetWindowText(c)=="OK" else None, None)
win32gui.PostMessage(ok[0], win32con.BM_CLICK, 0, 0); time.sleep(2)
k=ctypes.windll.kernel32; hp=k.OpenProcess(0x1000,0,PID); code=ctypes.c_ulong(); k.GetExitCodeProcess(hp,ctypes.byref(code)); print("exit code", code.value, "(259=still running)")
