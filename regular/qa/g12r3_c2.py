from g12r3_h import *
page("Income"); B=(850,250)
N=lambda: find(aid="NewPaidTagName"); R=lambda: find(aid="NewPaidTagRate")
for nm,bx in (("name",N),("rate",R)):
    click(bx()); keys("{TAB}"); rn=focused(); click(coords=B)
    click(bx()); keys("+{TAB}"); rp=focused(); click(coords=B)
    click(bx()); keys("QaTag" if nm=="name" else "1234"); typed=(val(N()),val(R()))
    keys("{ESC}"); after=(val(N()),val(R())); f=focused(); keys("9"); after9=(val(N()),val(R()),focused())
    keys("{TAB}"); t=focused(); click(coords=B)
    click(bx()); keys("Zz" if nm=="name" else "77"); keys("{ESC}"); keys("+{TAB}"); st=focused(); click(coords=B)
    print(f"[{nm}] typed={typed} afterEsc={after} focus={f}\n  after 9: {after9}\n  Tab={t} ref={rn} {'SAME' if t==rn else 'DIFF'}\n  ShTab={st} ref={rp} {'SAME' if st==rp else 'DIFF'}")
print("tags on page:", [e.element_info.name for e in w().descendants(control_type="Edit")])
shot("c2_newtag.png")
