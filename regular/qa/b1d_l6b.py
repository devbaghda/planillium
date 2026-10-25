# Line 6: readout while typing "500 123" key by key (no leaving), salary and tag box; paste path too.
from b1d_h import *
launch(); w().set_focus(); time.sleep(1); goto()
for box in (SAL, TAG):
    typein(box, "")
    for ch in "500 123":
        send_keys("{SPACE}" if ch == " " else ch); time.sleep(.35)
        log(f"  {box[:10]} after {ch!r}: box={val(box)!r} hourly={hourly()}")
    send_keys("{ENTER}"); time.sleep(.8); log("  Enter:", val(box), hourly(), cfg())
import subprocess
subprocess.run(["powershell","-NoProfile","-Command","Set-Clipboard -Value '500 123'"])
typein(SAL, ""); send_keys("^v"); time.sleep(.6); log("  paste salary:", repr(val(SAL)), hourly())
send_keys("{ENTER}"); time.sleep(.8); log("  Enter:", hourly(), cfg())
