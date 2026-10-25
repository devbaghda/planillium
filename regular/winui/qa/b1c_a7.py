from h import *
w.set_focus()
def rd(): return [e.element_info.name for e in w.descendants(control_type="Text") if e.element_info.name.startswith("= ")]
typein(SAL,"2925"); send_keys("{ENTER}"); typein(TAG,"1600"); send_keys("{ENTER}"); time.sleep(.8)
log("readouts valid",rd(),cfg())
typein(TAG,"-1600"); time.sleep(.6); log("readouts neg tag",rd())
typein(TAG,"1600"); send_keys("{ENTER}")
