import ctypes, time, win32gui, win32process
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\g14"
PID=int(open(S+r"\pid.txt").read())
res=[]
def cb(h,_):
    if win32process.GetWindowThreadProcessId(h)[1]==PID and win32gui.IsWindowVisible(h): res.append(h)
win32gui.EnumWindows(cb,None)
for h in res:
    print("WIN", repr(win32gui.GetWindowText(h)))
    def cc(c,_):
        t=win32gui.GetWindowText(c)
        if t: print("  CHILD", win32gui.GetClassName(c), repr(t))
    win32gui.EnumChildWindows(h,cc,None)
