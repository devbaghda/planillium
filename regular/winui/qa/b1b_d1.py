import sys
from b1b_t import *
def tx(name, left_max=1100):
    for e in w.descendants(control_type="Text"):
        r=e.rectangle()
        if e.element_info.name==name and 400<r.left<left_max: return e
def state(): return (cfg(), [val(t) for t in (SAL,TAG,NEW)])
heads=[("Income",None),("Paid work",None),("Previous salary — monthly net (€)",None),("Studioshoo — monthly net (€)",None),("New payable tag",None),("Settings",None)]
for t_ in (SAL,TAG,NEW):
  for hn,_ in heads:
    h=tx(hn)
    # snapshot values (after leaving so cfg is stable)
    edit(t_).click_input(); time.sleep(.3)
    h.click_input(); time.sleep(.4)
    b=state()
    send_keys("9"); time.sleep(.6)
    a=state()
    log(t_[:12],"| click on",hn[:20],"->", "UNCHANGED" if a==b else "CHANGED", a if a!=b else "")
    send_keys("{ESC}")
# click on blank space right of boxes (inside income panel, no text)
for t_ in (SAL,TAG):
    edit(t_).click_input(); time.sleep(.3)
    mouse.click(coords=(1000,420)); time.sleep(.4); b=state(); send_keys("9"); time.sleep(.6); a=state()
    log(t_[:12],"blank click ->","UNCHANGED" if a==b else "CHANGED",a if a!=b else "")
