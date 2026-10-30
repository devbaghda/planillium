from h import *
from decimal import Decimal
def money(s):
    s=s.replace('€','').replace(' ','').replace(' ','').replace(' ','').replace('\xa0','').replace('.','').replace(',','.')
    return Decimal(s)
find(name="Reports", ct="ListItem").iface_selection_item.Select(); time.sleep(3)
for per in ("Week","Month","Year"):
    click(find(per,"RadioButton")); time.sleep(2.5); o=dump(f"rep_{per}.txt")
    hdr=[l.split('|') for l in o if l.startswith("Text|Income lost/gained") or l.startswith("Text|Of which paid work")]
    cols={h[1]:int(h[3].split(',')[0]) for h in hdr}
    rows={}
    for l in o:
        p=l.split('|')
        if p[0]!="Text" or '€' not in p[1]: continue
        x,y=int(p[3].split(',')[0]),int(p[3].split(',')[1])
        for name,cx in cols.items():
            if x==cx: rows.setdefault(name,[]).append((y,p[1]))
    # row labels: texts at leftmost column on same y
    for name,vals in rows.items():
        vals.sort(); body=vals[:-1]; tot=vals[-1]
        s=sum(money(v) for _,v in body)
        print(per,name,"rows",[v for _,v in body],"Total row",tot[1],"MATCH" if s==money(tot[1]) else "MISMATCH")
    shot(f"rep_{per}.png")
