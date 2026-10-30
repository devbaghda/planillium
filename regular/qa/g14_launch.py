import subprocess, os, sys
S=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad"
EXE=r"D:\CLAUDE\Planillium\.claude\worktrees\agent-a023b7161cebc3538\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
root, suffix = sys.argv[1], sys.argv[2]
env=dict(os.environ, MENTOR_ROOT=root, MENTOR_INSTANCE_SUFFIX=suffix,
         PLANILLIUM_AI_BASE_URL="http://127.0.0.1:8767")
env.pop("PLANILLIUM_SCRATCH_AI_KEY", None)
p=subprocess.Popen([EXE],env=env,cwd=os.path.dirname(EXE))
open(S+r"\g14\pid.txt","w").write(str(p.pid)); print(p.pid)
