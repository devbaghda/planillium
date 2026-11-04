from g12r3_h import *
page("Hours and reminders")
g=find(name="Grace before alert", ct="Edit", starts=True); r=g.rectangle(); print("rect",r,"val",val(g))
click(g); print("focus",focused()); time.sleep(0.6); shot("c3_grace.png")
