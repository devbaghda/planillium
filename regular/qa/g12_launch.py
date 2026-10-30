import subprocess, os
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad"
EXE=r"D:\CLAUDE\Planillium\.claude\worktrees\agent-a8a9395131ec69578\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
env=dict(os.environ, MENTOR_ROOT=S+r"\mroot-g12", MENTOR_INSTANCE_SUFFIX="verify",
         PLANILLIUM_AI_BASE_URL="http://127.0.0.1:8765", PLANILLIUM_SCRATCH_AI_KEY="sk-ant-FAKE-qa-scratch-0000")
p=subprocess.Popen([EXE],env=env,cwd=os.path.dirname(EXE))
open(S+r"\g12\pid.txt","w").write(str(p.pid)); print(p.pid)
