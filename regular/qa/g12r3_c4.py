from g12r3_h import *
def inc(): c=cfg(); return {k:v for k,v in c.items() if k not in ('user_name','working_hours','end_of_day_summary_time','scoring','activity_rules','idle_activity_rules') and not k.startswith(('reminder','idle_thr','diary','late'))}
def capmsg(): return [t for t in texts() if "cap" in t.lower() and ("must" in t.lower() or "number" in t.lower() or "more than" in t.lower())]
def big(): return [t for t in texts() if "too large" in t.lower()]
B=(850,250)
page("Income")
S=lambda: find(name="Previous salary", ct="Edit", starts=True); T=lambda: find(name="Studioshoo", ct="Edit", starts=True)
s0,t0=val(S()),val(T()); print("start salary",s0,"tag",t0,"cfg",inc(), find(aid="SumIncome").window_text())
click(S()); keys("^a2500{ESC}"); print("G12-5 salary Esc:", val(S()), focused(), "cfg", inc())
click(T()); keys("^a1234{ESC}"); print("G12-5 tag Esc:", val(T()), focused(), "cfg", inc())
click(S()); keys("^a999999999999"); m1=big(); keys("{TAB}"); time.sleep(0.5)
print("G12-6 salary huge: while typed", m1, "| after Tab box", val(S()), "msg", big(), "status", status(), "focus", focused(), "cfg", inc(), find(aid="SumIncome").window_text()); shot("c4_g6.png")
click(coords=B)
page("AI")
CB=lambda: find(aid="AiCapBox"); MB=lambda: find(aid="AiModelBox"); KB=lambda: find(aid="AiKeyBox"); B2=(850,400)
click(CB()); keys("^a8{ENTER}"); print("setup cap 8 Enter:", val(CB()), cfg().get("ai"))
click(CB()); keys("^a12{ESC}"); print("G12-1 cap Esc:", val(CB()), focused(), "msg", capmsg(), cfg().get("ai"))
click(CB()); keys("^a-5{ENTER}"); m=capmsg(); keys("{ESC}"); print("G12-3 -5 Enter msg", m, "| after Esc:", val(CB()), focused(), "msg", capmsg(), cfg().get("ai")); shot("c4_g3.png")
click(MB()); keys("^aclaude-g12r3-model"); click(coords=B2); print("G12-10 model blank:", val(MB()), focused(), cfg().get("ai"))
click(CB()); keys("^a6"); click(coords=B2); print("G12-11 cap 6 blank:", val(CB()), focused(), cfg().get("ai"))
keys("9"); print("   typed 9:", "cap", val(CB()), "model", val(MB()), focused(), summ())
click(find(name="Save",ct="Button")); print("   Save probe (key box empty?):", summ(), cfg().get("ai"))
# G13-2
click(KB()); assert "AiKeyBox" in focused(); keys("sk-ant-FAKE-g12r3"); click(coords=B2); print("G13-2 key+blank:", focused(), summ())
click(CB()); keys("^a7{ENTER}"); print("   cap 7 Enter:", val(CB()), summ(), cfg().get("ai")); shot("c4_g13.png")
click(find(name="Save",ct="Button")); print("   Save probe:", summ(), cfg().get("ai"))
# G12-10 reopen
page("Income"); page("AI"); print("G12-10 reopen Model:", val(MB()))
