from g13_h import *
time.sleep(3)
app=Application(backend="uia").connect(process=PID)
for wn in app.windows():
    print("WIN", wn.element_info.name)
for e in w().descendants():
    i=e.element_info
    if i.control_type in ("Button","Window","Text") and i.name: print(i.control_type,"|",i.name[:70],"|",i.automation_id)
