from common import *
import subprocess
def typ(s): send_keys(s, with_spaces=True, pause=0.05)
def hint(w):
    for x in w.descendants():
        try:
            if "negative" in (x.element_info.name or "") and x.rectangle().width() > 0: return True
        except Exception: pass
    return False
def clip(s):
    subprocess.run(["powershell", "-command", "Set-Clipboard -Value '%s'" % s]);
    print("  clip now:", subprocess.run(["powershell","-command","Get-Clipboard"],capture_output=True,text=True).stdout.strip())
make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception: pass
    w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1)
    E = lambda: [x for x in w.descendants(control_type="Edit") if x.element_info.name.startswith("Previous salary")][0]
    E().click_input()
    for txt in ("-500","1","5-0","1","--", "123", "-", "---5", "-1-2-"):
        if txt=="1":
            typ("1"); time.sleep(.5); print("typed 1 -> box", repr(E().get_value()), "hint", hint(w)); continue
        send_keys("^a{BACKSPACE}"); time.sleep(.3); print("  after clear box", repr(E().get_value()), "focus", E().has_keyboard_focus()); clip(txt); send_keys("^v"); time.sleep(.8)
        print("paste", txt, "-> box", repr(E().get_value()), "hint", hint(w))
    send_keys("^a{BACKSPACE}"); typ("-1-2-"); time.sleep(.6); print("typed -1-2- -> box", repr(E().get_value()), "hint", hint(w))
    send_keys("^a{BACKSPACE}"); typ("1-2-"); time.sleep(.6); print("typed 1-2- -> box", repr(E().get_value()), "hint", hint(w))
finally: kill(p)
