from g14_h import *
import json
CFG=os.path.join(S,"mroot","config.json")
def cfg():
    try: return json.load(open(CFG,encoding="utf-8")).get("ai")
    except Exception as x: return f"noconfig {x}"
def dismiss():
    for _ in range(4):
        hit=[e for e in w().descendants(control_type="Button") if e.element_info.automation_id in ("PrimaryButton","CloseButton","SecondaryButton") and e.element_info.rectangle.width()>0]
        if not hit: return
        print("dialog", hit[0].element_info.name); hit[0].invoke(); time.sleep(1.5)
def inline():
    return [t for t in texts() if "cap" in t.lower() and ("must" in t.lower() or "above" in t.lower() or "number" in t.lower())]
CB=lambda: find(aid="AiCapBox"); MB=lambda: find(aid="AiModelBox")
dismiss()
find(name="Settings", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
find(name="AI", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5); dismiss()
print("START cap", val(CB()), "model", val(MB()), "cfg", cfg())
click(CB()); print("focus after click", focused()); keys("^a7{ENTER}")
print("CAP Enter: focus", focused(), "val", val(CB()), "status", status(), "cfg", cfg()); keys("9"); print("  after typing 9: cap", val(CB()), "model", val(MB()))
click(MB()); print("focus after click", focused()); keys("^aclaude-g14-model{ENTER}")
print("MODEL Enter: focus", focused(), "val", val(MB()), "status", status(), "cfg", cfg()); keys("9"); print("  after typing 9: cap", val(CB()), "model", val(MB()))
shot("c3_after_enter.png")
find(name="Reports", ct="ListItem").iface_selection_item.Select(); time.sleep(2)
find(name="Settings", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
find(name="AI", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
print("REOPEN cap", val(CB()), "model", val(MB()))
click(CB()); keys("^a-5{ENTER}")
print("CAP -5 Enter: focus", focused(), "val", val(CB()), "status", status(), "inline", inline(), "cfg", cfg())
shot("c3_minus5.png")
keys("{ESC}"); print("after Esc: focus", focused(), "val", val(CB()), "cfg", cfg())
