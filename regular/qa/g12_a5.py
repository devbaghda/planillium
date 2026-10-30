from h import *
import json
CFG=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\mroot-g12\config.json"
# restore model box text via Esc path first
click(find(aid="AiModelBox")); keys("{ESC}")
print("model after Esc", val(find(aid="AiModelBox")))
for pt in [(1300,800),(900,300)]:
    click(find(aid="AiCapBox")); keys("^a4")
    click(coords=pt)
    print("blank",pt,"focus",focused(),"cap",val(find(aid="AiCapBox")),"cfg",json.load(open(CFG,encoding="utf-8")).get("ai"))
    keys("{ESC}")
find(name="Income", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
for e in w().descendants(control_type="Edit"):
    i=e.element_info; print("EDIT",i.name,"|",i.automation_id,"|",i.rectangle,"|",val(e))
for e in w().descendants(control_type="Button"):
    i=e.element_info; print("BTN",i.name,"|",i.automation_id,"|",i.rectangle)
print("cfg income", json.load(open(CFG,encoding="utf-8")).get("income"), json.load(open(CFG,encoding="utf-8")).get("paid_tags"))
shot("inc0.png")
