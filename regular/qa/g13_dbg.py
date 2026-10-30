from g13_h import *
k=find(aid="AiKeyBox"); print("rect",k.rectangle(), k.element_info.control_type)
click(k); time.sleep(0.8); print("focus", focused(), "fg ours", fgpid()==PID)
shot("dbg.png")
