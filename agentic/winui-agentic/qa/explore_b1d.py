from b1d_lib import *
p, w = start()
try:
    print("fg", fg_ok(), w.rectangle())
    dump(w, 16)
    shot(w, "b1d_explore.png")
finally: kill(p)
