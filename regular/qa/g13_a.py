from g13_h import *
import json, ctypes
from pywinauto.uia_defines import IUIA
CFG=os.path.join(os.path.dirname(S),"mroot-g13","config.json")
def cfgai():
    try: return json.load(open(CFG,encoding="utf-8")).get("ai")
    except Exception as x: return f"noconfig {x}"
def summ(): return [t for t in texts() if t.startswith("No key") or t.startswith("Key saved") or t.startswith("A key is saved")]
def res():
    try: return find(aid="AiResult").window_text()
    except Exception: return "?"
class PT(ctypes.Structure): _fields_=[("x",ctypes.c_long),("y",ctypes.c_long)]
def under(pt):
    import comtypes.gen.UIAutomationClient as C; e=IUIA().iuia.ElementFromPoint(C.tagPOINT(*pt)); return f"{e.CurrentControlType}|{e.CurrentName}|{e.CurrentAutomationId}"
def st(tag): print(f"{tag}: summ={summ()} res={res()!r} focus={focused()} cfg={cfgai()}")
KB=lambda: find(aid="AiKeyBox"); MB=lambda: find(aid="AiModelBox"); CB=lambda: find(aid="AiCapBox")
def typekey(k):
    click(KB()); f=focused(); assert "AiKeyBox" in f, f; keys(k)
def probe(tag):
    # key-box-empty probe: Save; if the box held a key, the summary would turn to Key saved
    click(find(name="Save",ct="Button")); st(tag+" PROBE after Save")
find(name="Settings", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
find(name="AI", ct="ListItem").iface_selection_item.Select(); time.sleep(1.5)
r=CB().rectangle(); blank=(r.right+250, r.bottom+120)
print("blank", blank, "under:", under(blank))
st("START")
# ACC1
typekey("sk-ant-FAKE-g13-acc1"); click(coords=blank); st("ACC1 after blank"); shot("acc1_blank.png")
click(MB()); keys("^aclaude-g13-model{ENTER}"); st("ACC1 after Model Enter"); shot("acc1_model.png")
probe("ACC1")
# ACC2
typekey("sk-ant-FAKE-g13-acc2"); click(coords=blank); st("ACC2 after blank")
click(CB()); keys("^a7{ENTER}"); st("ACC2 after cap Enter"); shot("acc2_cap.png")
probe("ACC2")
# ACC5a: Tab out, then Enter in Model
typekey("sk-ant-FAKE-g13-acc5a"); keys("{TAB}"); st("ACC5a after Tab"); shot("acc5a_tab.png")
keys("^aclaude-g13-m2{ENTER}"); st("ACC5a after Model Enter (key text left in box)"); shot("acc5a_model.png")
click(coords=blank); st("ACC5a after blank (fix should empty box)")
probe("ACC5a")
# ACC5b: click another box, cap Enter
typekey("sk-ant-FAKE-g13-acc5b"); click(CB()); st("ACC5b after click cap"); keys("^a8{ENTER}"); st("ACC5b after cap Enter"); shot("acc5b.png")
keys("{ESC}"); click(coords=blank); st("ACC5b after blank")
probe("ACC5b")
# ACC4 Esc
typekey("sk-ant-FAKE-g13-acc4"); keys("{ESC}"); st("ACC4 after Esc"); keys("9"); st("ACC4 after typing 9"); shot("acc4.png")
probe("ACC4")
# Sibling: Enter in key box
typekey("sk-ant-FAKE-g13-enter"); keys("{ENTER}"); st("SIB key Enter"); shot("sib_keyenter.png")
click(coords=blank); st("SIB key Enter then blank")
probe("SIB key")
# ACC3 variant 1: blank then Save (regular: blank discards)
typekey("sk-ant-FAKE-g13-acc3a"); click(coords=blank); st("ACC3a after blank"); click(find(name="Save",ct="Button")); st("ACC3a after Save"); shot("acc3a.png")
# ACC3 deliberate: type key, Save
typekey("sk-ant-FAKE-g13-acc3b"); click(find(name="Save",ct="Button")); st("ACC3b after Save"); shot("acc3b.png")
