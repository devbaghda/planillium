from h import *
import json
CFG=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\mroot-g12\config.json"
find(name="Settings", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
find(name="Income", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
sal=find(name="Previous salary", ct="Edit", starts=True)
click(sal); keys("{ESC}"); print("parked:",focused()); keys("+{TAB}"); print("Shift+Tab after park from salary ->",focused())
keys("{ESC}")
find(name="AI", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
cap=find(aid="AiCapBox"); click(cap); keys("^a-5{TAB}")
print("cap -5 Tab: val",val(find(aid="AiCapBox")),"focus",focused(),"cfg",json.load(open(CFG,encoding="utf-8")).get("ai"),
      [t for t in texts() if "cap must" in t])
click(find(aid="AiCapBox")); keys("{ESC}")
print("cap Esc: val",val(find(aid="AiCapBox")),[t for t in texts() if "cap must" in t or "Key saved" in t or "key is saved" in t])
