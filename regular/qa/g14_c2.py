from g14_h import *
def dismiss():
    for _ in range(4):
        found=False
        for e in w().descendants(control_type="Button"):
            if e.element_info.automation_id in ("PrimaryButton","CloseButton","SecondaryButton") and e.element_info.rectangle.width()>0:
                print("dialog btn", e.element_info.automation_id, e.element_info.name); e.invoke(); time.sleep(1.5); found=True; break
        if not found: return
dismiss()
print("window", w().rectangle())
find(name="Reports", ct="ListItem").iface_selection_item.Select(); time.sleep(3); dismiss()
wr=w().rectangle()
for per in ("Day","Week","Month","Year"):
    find(per,"RadioButton").iface_selection_item.Select(); time.sleep(2.5)
    o=dump(f"rep_{per}.txt", skip=("Pane","Group","Custom","Button","ListItem"))
    hdr=[l for l in o if l.startswith("Text|") and ("\n" in l.split('|')[1] or l.split('|')[1] in ("Paid","Income","Hours","Tag","App","Category","Total","Date","Day","Week","Month"))]
    print("==",per, "headers:")
    for l in o:
        p=l.split('|')
        if p[0]=="Text" and "\n" in p[1]: print("  2-line", repr(p[1]), p[3])
    shot(f"rep_{per}.png")
