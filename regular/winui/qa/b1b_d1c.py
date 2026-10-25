from b1b_t import *
def focused():
    from pywinauto.uia_defines import IUIA
    el=IUIA().iuia.GetFocusedElement(); return el.CurrentName, el.CurrentClassName
def setup():
    typein(SAL,"5000"); send_keys("{TAB}"); time.sleep(.5)
for label,pt in (("blank right of salary box",(1000,420)),("caption text (Diary time carrying...)",(800,583)),("sidebar Settings title",(900,280))):
    setup(); edit(SAL).click_input(); time.sleep(.4)
    mouse.click(coords=pt); time.sleep(.5); f=focused(); send_keys("9"); time.sleep(.6)
    log(label,"-> focus",f,"salary box",repr(val(SAL)),"cfg",cfg()[0])
