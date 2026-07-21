#!/usr/bin/env python3
"""Table-aware extraction of the RCAS FSD .docx.

Pitfalls handled (these broke the prior audit):
  * `<w:t ...>` must NOT match `<w:tbl>`/`<w:tc>`/`<w:tr>` -> use `<w:t(?: [^>]*)?>`
  * self-closing `<w:p .../>` paragraphs -> don't rely on nesting depth
We use ElementTree, which handles all of that natively.
"""
import sys, zipfile, xml.etree.ElementTree as ET

W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
docx = sys.argv[1]

with zipfile.ZipFile(docx) as z:
    xml = z.read("word/document.xml")
root = ET.fromstring(xml)
body = root.find(f"{W}body")

def para_text(p):
    return "".join(t.text or "" for t in p.iter(f"{W}t"))

def row_cells(tr):
    cells = []
    for tc in tr.findall(f"{W}tc"):
        txt = " ".join(para_text(p).strip() for p in tc.findall(f"{W}p"))
        cells.append(txt.strip())
    return cells

out = []
tbl_n = 0
for el in body:
    tag = el.tag
    if tag == f"{W}p":
        txt = para_text(el).strip()
        if txt:
            out.append(txt)
    elif tag == f"{W}tbl":
        tbl_n += 1
        out.append(f"\n===== TABLE {tbl_n} =====")
        for tr in el.findall(f"{W}tr"):
            cells = row_cells(tr)
            out.append(" | ".join(cells))
        out.append(f"===== END TABLE {tbl_n} =====\n")

print("\n".join(out))
