# Рендер ТЗ (docs/ТЗ_ЛІС_MedLink_v4.md) у HTML зі стилями та скріншотами (docs/ТЗ_ЛІС_MedLink_v4.html).
# Позначки редакцій (4.1 — жовті, 4.2 — блакитні, 4.3 — зелені): блоки <div class="chg-*" markdown="1">,
# мітки <span class="tag-*">, а клас рядка таблиці/пункту списку — маркер <span class="row-chg*"></span> на початку клітинки.
# Використання: python tools/render_tz_html.py
import io
import os
import re

import markdown

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'docs', 'ТЗ_ЛІС_MedLink_v4.md')
DST = os.path.join(ROOT, 'docs', 'ТЗ_ЛІС_MedLink_v4.html')

CSS = """body{font-family:Segoe UI,Arial,sans-serif;max-width:1200px;margin:24px auto;padding:0 16px;color:#262626;line-height:1.5}
table{border-collapse:collapse;width:100%;font-size:13px;margin:12px 0}th,td{border:1px solid #cfd8e3;padding:6px 8px;vertical-align:top;text-align:left}
th{background:#eef3f9}code{background:#f3f5f8;padding:1px 4px;border-radius:3px;font-size:12px}pre code{display:block;padding:10px;overflow:auto}
h1,h2,h3{color:#4274A7}h2{border-bottom:2px solid #4274A7;padding-bottom:4px;margin-top:36px}img{max-width:100%;border:1px solid #ccc;border-radius:4px;margin:8px 0}
blockquote{border-left:4px solid #4274A7;margin:8px 0;padding:4px 12px;background:#f6f9fc}nav.toc{background:#f6f9fc;padding:8px 16px;border-radius:6px}
.chg,mark.chg{background:#fff1a8}
tr.chg td{background:#fff1a8}
.chg-block{border-left:5px solid #f0b400;background:#fff8d6;padding:6px 14px;margin:12px 0;border-radius:0 4px 4px 0}
.chg-sec{border-left:6px solid #f0b400;background:#fffbe9;padding:2px 16px 8px;margin:18px 0;border-radius:0 6px 6px 0}
.chg-sec table td{background:#fffef6}.chg-sec th{background:#fbefc0}
.tag-new,.tag-mod,.tag-done{display:inline-block;font-size:11px;font-weight:700;border-radius:3px;padding:1px 6px;margin-right:6px;vertical-align:middle}
.tag-new{background:#2e9e5b;color:#fff}.tag-mod{background:#e08a00;color:#fff}.tag-done{background:#4274A7;color:#fff}
.c42{border-left-color:#2E7BD6;background:#F2F8FF}.c42 th{background:#D9E9FB}.c42 table td{background:#FBFDFF}
mark.chg42,.chg42{background:#CFE4FF}tr.chg42 td{background:#E3EFFF}
.tag-new42{display:inline-block;font-size:11px;font-weight:700;border-radius:3px;padding:1px 6px;margin-right:6px;vertical-align:middle;background:#2E7BD6;color:#fff}
.chg-block.b42{border-left-color:#2E7BD6;background:#EAF3FF}
.c43{border-left-color:#1E8E5A;background:#F1FAF5}.c43 th{background:#D3EEDD}.c43 table td{background:#FBFEFC}
mark.chg43,.chg43{background:#C9EDD6}tr.chg43 td{background:#E1F5E8}
.tag-new43{display:inline-block;font-size:11px;font-weight:700;border-radius:3px;padding:1px 6px;margin-right:6px;vertical-align:middle;background:#1E8E5A;color:#fff}
.chg-block.b43{border-left-color:#1E8E5A;background:#EAF7EF}
@media print{.chg,mark.chg,tr.chg td,.chg-block,.chg-sec{-webkit-print-color-adjust:exact;print-color-adjust:exact}}"""

ROW_MARK = re.compile(r'<(tr|li)>(\s*(?:<td>)?)<span class="row-(chg\w*)"></span>')


def main():
    text = io.open(SRC, encoding='utf-8').read()
    html = markdown.markdown(text, extensions=['tables', 'fenced_code', 'toc', 'sane_lists', 'md_in_html'])
    html = ROW_MARK.sub(lambda m: f'<{m.group(1)} class="{m.group(3)}">{m.group(2)}', html)
    title = re.search(r'^# .*?редакція[^\n]*?(\d\.\d)</mark>\s*$', text, re.M)
    version = title.group(1) if title else '4'
    doc = f'<!DOCTYPE html><html lang="uk"><head><meta charset="utf-8"><title>ТЗ ЛІС MedLink v{version}</title><style>{CSS}</style></head><body>{html}</body></html>'
    io.open(DST, 'w', encoding='utf-8', newline='\n').write(doc)
    print('ТЗ HTML:', DST, len(doc) // 1024, 'КБ')


if __name__ == '__main__':
    main()
