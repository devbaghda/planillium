from h import *
import json
CFG=r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\mroot-g12\config.json"
def cfg():
    c=json.load(open(CFG,encoding="utf-8")); return c.get("income"), c.get("paid_tags")
def sal(): return find(name="Previous salary", ct="Edit", starts=True)
def tag(): return find(name="Studioshoo", ct="Edit", starts=True)
def msgs(): return [t for t in texts() if any(s in t for s in ("too large","negative","Saved","kept","must","Amount"))]
# B5 Esc on salary
click(sal()); keys("^a4000"); keys("{ESC}")
print("B5 Esc: val",val(sal()),"focus",focused(),"cfg",cfg(),"msgs",msgs())
keys("9"); print("B5 after 9: val",val(sal()))
# regression Tab / Shift+Tab after park
keys("{TAB}"); print("Tab after park ->",focused())
keys("{ESC}"); keys("+{TAB}"); print("Shift+Tab after park ->",focused())
keys("{ESC}")
# B6 999999999999 Tab
click(sal()); keys("^a999999999999"); print("B6 while typing: val",val(sal()),"msgs",msgs())
shot("b6_typing.png")
keys("{TAB}")
print("B6 after Tab: val",val(sal()),"focus",focused(),"cfg",cfg(),"msgs",msgs())
shot("b6_tab.png")
keys("{ESC}")
# B7 normal amounts
click(sal()); keys("^a3100"); keys("{TAB}")
print("B7 3100 Tab: val",val(sal()),"cfg",cfg(),"msgs",msgs())
keys("{ESC}")
click(sal()); keys("^a2925{ENTER}")
print("B7 2925 Enter: val",val(sal()),"focus",focused(),"cfg",cfg(),"msgs",msgs())
# blank click saves on salary
r=sal().rectangle()
click(sal()); keys("^a3000"); click(coords=(r.right+500, r.bottom+400))
print("Blank click salary: val",val(sal()),"focus",focused(),"cfg",cfg())
keys("9"); print("after 9: val", val(sal()))
click(sal()); keys("^a2925{ENTER}")
# tag rate box: too large, Esc
click(tag()); keys("^a150000"); keys("{TAB}")
print("Tag 150000 Tab: val",val(tag()),"focus",focused(),"cfg",cfg(),"msgs",msgs())
shot("tag_big.png")
keys("{ESC}")
click(tag()); keys("^a2000"); keys("{ESC}")
print("Tag Esc: val",val(tag()),"focus",focused(),"cfg",cfg())
# New payable tag: too large
click(find(aid="NewPaidTagName")); keys("Big"); click(find(aid="NewPaidTagRate")); keys("200000")
click(find(aid="NewPaidTagAdd"))
print("New tag 200000 Add: name",val(find(aid="NewPaidTagName")),"rate",val(find(aid="NewPaidTagRate")),"cfg",cfg(),"msgs",msgs(),"focus",focused())
shot("newtag_big.png")
keys("{ESC}")
# New payable tag normal: Studio 1000 via Enter
click(find(aid="NewPaidTagName")); keys("^aStudio"); click(find(aid="NewPaidTagRate")); keys("^a1000{ENTER}")
print("New tag Studio 1000 Enter: cfg",cfg(),"focus",focused(),"edits",[(e.element_info.name,val(e)) for e in w().descendants(control_type="Edit")])
shot("newtag_ok.png")
