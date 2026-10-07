# Рендер ТЗ (docs/ТЗ_ЛІС_MedLink_v4.md) у HTML зі стилями та скріншотами (docs/ТЗ_ЛІС_MedLink_v4.html).
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
blockquote{border-left:4px solid #4274A7;margin:8px 0;padding:4px 12px;background:#f6f9fc}nav.toc{background:#f6f9fc;padding:8px 16px;border-radius:6px}"""


def main():
    text = io.open(SRC, encoding='utf-8').read()
    html = markdown.markdown(text, extensions=['tables', 'fenced_code', 'toc', 'sane_lists'])
    html = re.sub(r'<table>', '<table>', html)
    doc = f'<!DOCTYPE html><html lang="uk"><head><meta charset="utf-8"><title>ТЗ ЛІС MedLink v4</title><style>{CSS}</style></head><body>{html}</body></html>'
    io.open(DST, 'w', encoding='utf-8', newline='\n').write(doc)
    print('ТЗ HTML:', DST, len(doc) // 1024, 'КБ')


if __name__ == '__main__':
    main()
