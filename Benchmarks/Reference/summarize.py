import csv,collections,statistics as S,sys
rows=list(csv.reader(open(sys.argv[1] if len(sys.argv)>1 else "matches.csv")))
levels = ["Easy","Normal","Hard","VeryHard","Monster"]
def calculate(rs):
 allsc=[r for r in rs if r[5]!="retry"]
 main=[r for r in rs if r[5]=="main"]
 m=[int(r[7]) for r in main]
 v=[int(r[7]) for r in allsc]
 calc=lambda v: f'{sum(x>0 for x in v)}-{sum(x==0 for x in v)}-{sum(x<0 for x in v)}'
 return f'{calc(m)} / {len(m)}; {calc(v)} all, draws-only={len(rs)-len(v)}, draftfinish={len(v)-len(m)}; avgMargin={S.mean(m):.1f} avgTurns={S.mean(int(r[6]) for r in main):.1f}' if m else str(collections.Counter(r[5] for r in rs))
for kind in sorted({r[0] for r in rows}):
 print(kind)
 for level in levels:
  print(level,calculate([r for r in rows if r[0]==kind and r[2]==level]))
 for map in dict.fromkeys(r[1] for r in rows if r[0]==kind):
  print(map)
  for level in levels:
   print(level,calculate([r for r in rows if r[0]==kind and r[1]==map and r[2]==level]))
