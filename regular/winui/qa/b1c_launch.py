import subprocess, os
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\cf7cbaac-b682-4ea0-9f95-3643c4f351b6\scratchpad"
env=dict(os.environ, MENTOR_ROOT=S+r"\root3", MENTOR_INSTANCE_SUFFIX="verify")
exe=r"D:\CLAUDE\Planillium\.claude\worktrees\qa-b1c\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
p=subprocess.Popen([exe],env=env); print(p.pid)
open(S+r"\b1c\pid.txt","w").write(str(p.pid))
