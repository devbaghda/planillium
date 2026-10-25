# Does a timed prompt (kickoff / review) closing over Settings>Income drop the cursor into the salary box?
from b1d_h import *
launch(seed=False); w().set_focus(); time.sleep(1)
def dlg_buttons():
    return [(b.element_info.name, b.element_info.automation_id) for b in w().descendants(control_type="Button") if b.element_info.automation_id in ("PrimaryButton","SecondaryButton","CloseButton")]
log("buttons at start", dlg_buttons())
for i in range(3):
    bs = dlg_buttons()
    if not bs: break
    w().child_window(auto_id="CloseButton").click_input(); time.sleep(2)
goto(); log("on Income, focus", foc())
typein(SAL, "3000"); send_keys("{ENTER}"); time.sleep(1); log("parked", foc())
t0 = time.time()
while time.time() - t0 < 150:
    bs = dlg_buttons()
    if bs: break
    time.sleep(3)
log("dialog after", int(time.time()-t0), "s:", bs, "focus", foc())
shot("b1d_dialog_open.png")
if bs:
    names = dict(bs)
    w().child_window(auto_id="CloseButton").click_input(); time.sleep(2)
    log("after close", foc(), "more dialogs", dlg_buttons())
    nine("after dialog closed by mouse")
    bs = dlg_buttons()
    if bs:
        w().child_window(auto_id="CloseButton").click_input(); time.sleep(2)
        nine("after 2nd dialog closed")
