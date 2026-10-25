import subprocess, os, time, sys
env=dict(os.environ, MENTOR_ROOT=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\cf7cbaac-b682-4ea0-9f95-3643c4f351b6\scratchpad\root", MENTOR_INSTANCE_SUFFIX="verify")
exe=r"D:\CLAUDE\Planillium\.claude\worktrees\qa-b1b-r5\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
p=subprocess.Popen([exe],env=env)
print(p.pid)
open(r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\cf7cbaac-b682-4ea0-9f95-3643c4f351b6\scratchpad\pid.txt","w").write(str(p.pid))
