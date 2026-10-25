from common import *
import subprocess
def hint(w):
    for x in w.descendants():
        try:
            if "egative" in (x.element_info.name or ""): return True
        except Exception: pass
    return False
make_root(); p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception: pass
    w = win(p)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1)
    rate = [x for x in w.descendants(control_type="Edit") if x.element_info.name.startswith("Monthly net")][0]
    rate.click_input(); send_keys("^a{BACKSPACE}"); send_keys("-"); time.sleep(.6)
    print("newtag box:", repr(rate.get_value()), "hint:", hint(w))
    sal = [x for x in w.descendants(control_type="Edit") if "salary" in x.element_info.name][0]
    sal.click_input(); send_keys("^a{BACKSPACE}")
    subprocess.run(["powershell","-command","Set-Clipboard -Value '-500'"])
    send_keys("^v"); time.sleep(.6)
    print("paste -500: box", repr(sal.get_value()), "hint:", hint(w))
    send_keys("^a{BACKSPACE}")
    subprocess.run(["powershell","-command","Set-Clipboard -Value '5-0'"])
    send_keys("^v"); time.sleep(.6)
    print("paste 5-0: box", repr(sal.get_value()), "hint:", hint(w))
    # fast poll
    send_keys("^a{BACKSPACE}7"); 
    send_keys("-"); 
    seen = [hint(w) for _ in range(3)]
    print("fast poll after '-'", seen)
finally: kill(p)
