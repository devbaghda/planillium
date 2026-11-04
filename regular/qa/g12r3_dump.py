from g12r3_h import *
import sys
page(sys.argv[1]); out=dump("dump_"+sys.argv[1]+".txt")
print("\n".join(l for l in out if l.split("|")[0] in ("Edit","Button","ListItem","CheckBox","Spinner","ComboBox","Text") ))
