from h import *
def openf():
    click(find("Filter diary by app","Button")); time.sleep(1)
    return [e for e in w().descendants(control_type="CheckBox") if e.element_info.name!="Apply filters across the last 90 days instead of just this day"]
def info():
    t=texts()
    return [x for x in t if "match" in x.lower() or "entr" in x.lower() or "Showing" in x][:4], len([x for x in w().descendants(control_type="CheckBox") if "Select" in (x.element_info.name or "") or x.element_info.name==""])
cbs=openf(); names=[c.element_info.name for c in cbs]; print("App entries:",names)
dump("appflyout.txt"); keys("{ESC}")
print("no filter:",info())
for n in names:
    cbs=openf(); c=[x for x in cbs if x.element_info.name==n][0]; click(c); keys("{ESC}"); time.sleep(1)
    print("entry",repr(n),"->",info())
    if n=="-": shot("app_dash.png")
    cbs=openf(); c=[x for x in cbs if x.element_info.name==n][0]; click(c); keys("{ESC}")
