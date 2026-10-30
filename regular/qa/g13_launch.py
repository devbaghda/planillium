import subprocess, os
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad"
EXE=r"D:\CLAUDE\Planillium\.claude\worktrees\qa-g13-reg\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
env=dict(os.environ, MENTOR_ROOT=S+r"\mroot-g13", MENTOR_INSTANCE_SUFFIX="verify-g13",
         PLANILLIUM_AI_BASE_URL="http://127.0.0.1:8766")
env.pop("PLANILLIUM_SCRATCH_AI_KEY", None)
p=subprocess.Popen([EXE],env=env,cwd=os.path.dirname(EXE))
open(S+r"\g13\pid.txt","w").write(str(p.pid)); print(p.pid)
