# B1h QA: Esc in saved money boxes (lines 1-5). Scratch MENTOR_ROOT only; own Debug exe from qa-b1h worktree.
import sys; sys.path.insert(0, r"D:\CLAUDE\Planillium\regular\qa")
import b1d_h as H
from b1d_h import *
S = r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\4dbcc78c-85fb-4187-b811-2ed538794a5b\scratchpad\b1h"
H.ROOT = S + r"\root"; H.CFG = H.ROOT + r"\config.json"; H.FIXTURE = S + r"\fixture_config.json"; H.PIDF = S + r"\pid.txt"
H.EXE = r"D:\CLAUDE\Planillium\.claude\worktrees\qa-b1h\regular\winui\Planillium.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Planillium.App.exe"
os.environ["PLANILLIUM_AI_BASE_URL"] = "http://127.0.0.1:59999/"
launch(); w().set_focus(); time.sleep(1); goto()
log("start", snap(), "focus", foc())
for box in ((SAL, TAG) if "reg" not in sys.argv else ()):
    for label, s in (("2500", "2500"), ("empty", ""), ("abc", "abc"), ("spaces", "   "), ("-5", "-5")):
        typein(box, s); log(f"{box[:10]} typed {label}: box={val(box)!r} red={red()}")
        send_keys("{ESC}"); time.sleep(.7)
        log(f"  Esc -> box={val(box)!r} cfg={cfg()} red={red()} saved={saved()}")
        nine(f"{box[:10]} {label}+Esc")
    shot("b1h_esc_" + ("sal" if box == SAL else "tag") + ".png")
# line 5 regression
typein(SAL, "3000"); send_keys("{ENTER}"); time.sleep(1); log("SAL 3000 Enter: cfg", cfg(), "box", val(SAL), "saved", saved()); nine("after SAL Enter")
typein(TAG, "1700"); send_keys("{ENTER}"); time.sleep(1); log("TAG 1700 Enter: cfg", cfg(), "box", val(TAG)); nine("after TAG Enter")
typein(SAL, "2500"); send_keys("{ESC}"); time.sleep(.7); log("after save, SAL 2500 Esc -> box", val(SAL), "cfg", cfg())
typein(NAME, "zz"); typein(NEW, "400"); send_keys("{ESC}"); time.sleep(.7); log("new-tag Esc: line", repr(val(NAME)), repr(val(NEW)), "cfg", cfg(), "focus", foc()); nine("after new-tag Esc")
typein(SAL, "2800"); send_keys("{TAB}"); time.sleep(1); log("SAL 2800 Tab: cfg", cfg(), "box", val(SAL), "focus", foc())
typein(TAG, "1650"); r = w().rectangle(); mouse.click(coords=(r.left+850, r.top+230)); time.sleep(1); log("TAG 1650 blank click: cfg", cfg(), "box", val(TAG), "focus", foc()); nine("after blank click")
typein(SAL, ""); send_keys("{TAB}"); time.sleep(1); log("SAL empty Tab (B1d revert): box", val(SAL), "cfg", cfg())
typein(TAG, "-5"); send_keys("{TAB}"); time.sleep(1); log("TAG -5 Tab (B1d minus kept): box", repr(val(TAG)), "red", red(), "cfg", cfg())
send_keys("{ESC}"); time.sleep(.7); log("  then Esc? focus", foc())
H.kill(); log("done")
