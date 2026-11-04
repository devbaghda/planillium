from common import *
make_root(); p = launch()
try:
    w = win(p); print(w.window_text())
    dump(w)
finally: kill(p)
