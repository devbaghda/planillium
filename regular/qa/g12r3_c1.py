from g12r3_h import *
import sys
sec=sys.argv[1]; page(sec)
py12=None
def box(n):
    return find(name=n, ct="Edit", starts=True)
for pt in [(850,250),(850,560),(900,700)]: print("blank?",pt,under(pt))
B=tuple(int(x) for x in sys.argv[2].split(","))
print("BLANK",B,under(B))
for n in sys.argv[3:]:
    click(box(n)); f0=focused(); keys("{TAB}"); rn=focused(); click(coords=B); click(box(n)); keys("+{TAB}"); rp=focused(); click(coords=B)
    click(box(n)); f1=focused(); click(coords=B); pk=focused(); keys("{TAB}"); pn=focused(); click(coords=B)
    click(box(n)); click(coords=B); keys("+{TAB}"); pp=focused(); click(coords=B)
    print(f"[{n}] in-box={f0}\n   ref Tab={rn}\n   ref ShTab={rp}\n   after blank={pk}\n   park Tab={pn}  {'SAME' if pn==rn else 'DIFF'}\n   park ShTab={pp}  {'SAME' if pp==rp else 'DIFF'}")
    if n.startswith("Previous salary"):
        click(box(n)); keys("{ESC}"); e=focused(); keys("+{TAB}"); print(f"   G12-12 Esc={e} then ShTab={focused()}  {'SAME' if focused()==rp else 'DIFF'}"); click(coords=B)
        click(box(n)); click(coords=B); v=val(box(n)); keys("9"); print("   type 9 after blank: salary", v, "->", val(box(n)), "focus", focused())
print("cfg income", {k:v for k,v in cfg().items() if 'income' in k.lower() or 'salary' in k.lower() or 'paid' in k.lower()})
