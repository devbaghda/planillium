from grp_h import *
import grp_h as G
G.attach(); w().set_focus()
for fb, it in (("cat","-"),("app","-"),("page","-"),("tag","-"),("app","Other"),("app","Telegram"),("app","Word"),("app","Outlook")):
    G.toggle(fb, it); log(f"{fb}={it}: {G.showing()} | {G.tcount()}"); G.toggle(fb, it)
log("cleared:", G.showing())
# combination: tag '-' + app Other
G.toggle("tag", "-"); G.toggle("app", "Other"); log("tag- & app Other:", G.showing(), G.tcount())
G.toggle("app", "Other"); G.toggle("tag", "-")
G.toggle("page", "-"); G.toggle("app", "Telegram"); log("page- & app Telegram:", G.showing())
G.toggle("app", "Telegram"); G.toggle("page", "-"); log("cleared2:", G.showing())
