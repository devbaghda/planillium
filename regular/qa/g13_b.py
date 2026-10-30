exec(open("g13_a.py",encoding="utf-8").read().split("find(name=\"Settings\"")[0])
r=CB().rectangle(); blank=(r.right+250, r.bottom+120); print("under", under(blank))
def msgs(): return [t for t in texts() if "cap" in t.lower() and ("must" in t.lower() or "above" in t.lower() or "more than" in t.lower())]
# qa-extra 18: -5 Save -> message; leave box -> message matches text
click(CB()); keys("^a-5"); click(find(name="Save",ct="Button")); print("X18 -5 Save:", res(), msgs(), "cfg", cfgai())
click(CB()); keys("^a6{TAB}"); print("X18 after 6 Tab:", res(), msgs(), "val", val(CB()), "cfg", cfgai())
# qa-extra 23: Esc / blank on cap, Model, key
click(CB()); keys("^a9{ESC}"); print("X23 cap Esc: val",val(CB()),"focus",focused(),"cfg",cfgai()); keys("1"); print("  typed 1 -> cap",val(CB()))
click(CB()); keys("^a4"); click(coords=blank); print("X23 cap blank: val",val(CB()),"focus",focused(),"cfg",cfgai())
click(MB()); keys("^axx-esc{ESC}"); print("X23 model Esc: val",val(MB()),"focus",focused(),"cfg",cfgai())
click(MB()); keys("^aclaude-g13-blank"); click(coords=blank); print("X23 model blank: val",val(MB()),"focus",focused(),"cfg",cfgai())
click(KB()); keys("sk-ant-FAKE-x23"); click(coords=blank); print("X23 key blank: focus",focused(), summ())
# qa-extra 27: cap, type 6, blank, Shift+Tab -> Model
click(CB()); keys("^a6"); click(coords=blank); keys("+{TAB}"); print("X27 Shift+Tab after blank park:", focused(), "cfg", cfgai())
click(coords=blank)
print("END", summ())
