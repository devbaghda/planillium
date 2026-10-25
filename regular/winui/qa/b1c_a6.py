from h import *
w.set_focus()
# L11 Esc
typein(NAME,"escme"); typein(NEW,"123"); send_keys("{ESC}"); time.sleep(.8)
log("L11 Esc: name",repr(val(NAME)),"amt",repr(val(NEW)),"tags",list(cfg()[1]),"focus",foc())
typein(NAME,"clickme"); typein(NEW,"-5"); 
from pywinauto import mouse
r=edit(SAL).rectangle(); mouse.click(coords=(r.right+250,r.top+10)); time.sleep(.8)
log("L11 click outside: name",repr(val(NAME)),"amt",repr(val(NEW)),"tags",list(cfg()[1]),msgs())
# L13 spinners
log("L13 spin-like buttons",[b.element_info.name for b in w.descendants(control_type="Button") if any(k in b.element_info.name for k in("Spin","Increase","Decrease"))])
typein(SAL,"14000"); log("fmt",repr(val(SAL))); typein(SAL,"1234567"); log("fmt",repr(val(SAL)))
typein(SAL,"2925"); send_keys("{ENTER}")
# L12 screenshots
typein(SAL,"-500"); time.sleep(.7); w.capture_as_image().save("neg_sal.png")
typein(NEW,"-500"); time.sleep(.7); w.capture_as_image().save("neg_new.png")
