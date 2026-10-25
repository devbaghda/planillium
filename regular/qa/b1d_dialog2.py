# Repro attempt for the first-run anomaly (salary became 55 while a timed dialog was up).
from b1d_h import *
launch(seed=False); w().set_focus(); time.sleep(1)
def dlg(): return [(b.element_info.name) for b in w().descendants(control_type="Button") if b.element_info.automation_id in ("PrimaryButton","CloseButton")]
goto(); typein(SAL, "", clear=False); mouse.click(coords=(w().rectangle().left+850, w().rectangle().top+230)); time.sleep(1)
t0=time.time()
while time.time()-t0<150 and not dlg(): time.sleep(2)
log("dialog", dlg(), "focus", foc())
for step in range(4):
    typein(NEW, "55"); time.sleep(.5)
    log(f" step{step} after typein NEW: dlg={dlg()} focus={foc()} snap={snap()}")
    mouse.click(coords=(w().rectangle().left+850, w().rectangle().top+230)); time.sleep(1.2)
    log(f" step{step} after blank click: dlg={dlg()} focus={foc()} snap={snap()}")
