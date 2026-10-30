from h import *
import json
CFG=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\mroot-g12\config.json"
def cfgai():
    try: return json.load(open(CFG,encoding="utf-8")).get("ai")
    except Exception as x: return f"noconfig {x}"
def cap(): return find(aid="AiCapBox")
def msgs(): return [t for t in texts() if "cap" in t.lower() or "must" in t.lower() or "negative" in t.lower() or "number" in t.lower()]
find(name="AI", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
dump("ai0.txt")
r=cap().rectangle(); print("cap rect", r, "cfg", cfgai(), "status", status())
# set a saved value first: 8 + Enter
click(cap()); keys("^a8{ENTER}"); time.sleep(0.8)
print("SETUP 8+Enter: val",val(cap()),"focus",focused(),"cfg",cfgai(),"status",status())
# A1: type 12, Esc
click(cap()); keys("^a12"); keys("{ESC}")
print("A1 after Esc: val",val(cap()),"focus",focused(),"msgs",msgs())
keys("9"); print("A1 after typing 9: val",val(cap()),"focus",focused())
shot("a1.png")
# A3: -5 Enter then Esc
click(cap()); keys("^a-5{ENTER}")
print("A3 after Enter: val",val(cap()),"focus",focused(),"msgs",msgs(),"status",status())
shot("a3_enter.png")
keys("{ESC}")
print("A3 after Esc: val",val(cap()),"focus",focused(),"msgs",msgs(),"status",status(),"cfg",cfgai())
shot("a3_esc.png")
# A4: type 6, click blank right of and below boxes
r=cap().rectangle()
click(cap()); keys("^a6")
blank=(r.right+250, r.bottom+120)
print("blank point", blank)
click(coords=blank); time.sleep(0.8)
print("A4 after blank click: val",val(cap()),"focus",focused(),"cfg",cfgai(),"status",status())
keys("7"); print("A4 after typing 7: val",val(cap()))
shot("a4.png")
# A2 Model
m=find(aid="AiModelBox"); before=val(m); print("model saved", before)
click(m); keys("^axyz-typed"); print("model typed", val(find(aid="AiModelBox"))); keys("{ESC}")
print("A2 model after Esc: val",val(find(aid="AiModelBox")),"focus",focused(),"cfg",cfgai(),"status",status())
keys("9"); print("A2 model after 9:", val(find(aid="AiModelBox")))
# Model blank click
click(find(aid="AiModelBox")); keys("^aabc"); click(coords=blank)
print("A2 model blank click: focus",focused(),"val",val(find(aid="AiModelBox")),"cfg",cfgai())
# Key
k=find(aid="AiKeyBox"); print("key texts before", [t for t in texts() if "key" in t.lower()])
click(k); keys("sk-ant-TYPED-qa-0001"); print("key focus while typing", focused())
keys("{ESC}")
print("A2 key after Esc: focus",focused(),"status",status(),"keytexts",[t for t in texts() if "key" in t.lower()])
try: print("key value (pattern)", val(find(aid="AiKeyBox")))
except Exception as x: print("key value n/a", x)
shot("a2_key.png")
click(find(aid="AiKeyBox")); keys("x");
print("key after re-click + x: (if box had kept text, length would show)", focused())
keys("{ESC}")
click(find(aid="AiKeyBox")); keys("abc"); click(coords=blank)
print("A2 key blank click: focus",focused())
keys("{ESC}")
print("cfg end", cfgai())
