from g13_h import *
for n in range(6):
    dlg=[e for e in w().descendants(control_type="Window") if e.element_info.name and e.element_info.name!="Popup"]
    if not dlg: print("no dialog"); break
    d=dlg[0]; print("DIALOG", d.element_info.name)
    eds=[e for e in d.descendants(control_type="Edit")]
    if eds and not val(eds[0]):
        try: eds[0].iface_value.SetValue("QA")
        except Exception as x: print("edit",x)
    btns={e.element_info.automation_id:e for e in d.descendants(control_type="Button")}
    print(" buttons", [(k,b.element_info.name) for k,b in btns.items()])
    b=next((x for x in btns.values() if x.element_info.name=="Later"),None) or btns.get("PrimaryButton") or btns.get("CloseButton") or btns.get("SecondaryButton")
    b.invoke(); time.sleep(2)
print("fg ours:", fgpid()==PID)
