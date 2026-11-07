from pathlib import Path
import re
text=Path('tmp/manual.txt').read_text(encoding='utf-8')
parts=re.split(r'\n--- PDF PAGE (\d+) ---\n',text)
rows=[]
for i in range(1,len(parts),2):
    lines=[s.strip() for s in parts[i+1].splitlines() if s.strip()]
    rows.append(f'- PDF page {parts[i]}: '+ ' / '.join(lines[:3])[:220].rstrip())
Path('docs/research/lightburn-manual-index.md').write_text('# LightBurn 2.2 manual index\n\nGenerated November 5, 2026; local reference PDF. Physical PDF page numbers below. Full text extracted locally; index is not a completed review of every feature.\n\n'+'\n'.join(rows),encoding='utf-8')
print('Indexed',len(rows),'pages')
for term in ['User Interface Tour','Creating and Importing Artwork','Image Mode','Dither','Rotary Setup','Camera Control','Cut Settings','What\u2019s New']:
    hits=[]
    for i in range(1,len(parts),2):
        if term.casefold() in parts[i+1].casefold(): hits.append(parts[i])
    print(term, ', '.join(hits[:18]))
