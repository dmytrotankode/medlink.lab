# -*- coding: utf-8 -*-
"""
apply_medlink_restrained_styling.py
Applies strict MedLink (evomis) styling to run_prototype.html & index.html:
- Quasar theme variables ($primary: #4274A7, $secondary: #4274A7, $accent: #0178BC, $positive: #21ba45, $negative: #d04f45)
- Typography: 'Source Sans Pro', sans-serif (14.5px)
- Unified dark charcoal drawer (#212121) without rainbow icons
- Clean white enterprise pipeline stepper (no pitch-dark sci-fi gradient, no neon)
- Cohesive MedLink guide boxes & architecture banners (border-left #4274A7, no rainbow borders)
- Restrained clinical badges and tables
"""

import re
import os

PROTOTYPE_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\run_prototype.html"
INDEX_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_frontend\index.html"

with open(PROTOTYPE_PATH, "r", encoding="utf-8") as f:
    html = f.read()

# 1. Update font to Source Sans Pro
old_font = '<link href="https://fonts.googleapis.com/css?family=Roboto:100,300,400,500,700,900|Material+Icons|Material+Icons+Outlined" rel="stylesheet" type="text/css">'
new_font = """  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Source+Sans+Pro:wght@300;400;600;700;800&family=JetBrains+Mono:wght@400;500;600&display=swap" rel="stylesheet">
  <link href="https://fonts.googleapis.com/css?family=Material+Icons|Material+Icons+Outlined" rel="stylesheet" type="text/css">"""
if old_font in html:
    html = html.replace(old_font, new_font)

# 2. Refine CSS in style block
css_replacement = """    [v-cloak] { display: none !important; }
    :root {
      --q-color-primary: #4274A7;
      --q-color-secondary: #4274A7;
      --q-color-accent: #0178BC;
      --q-color-positive: #21ba45;
      --q-color-negative: #d04f45;
      --q-color-info: #0178BC;
      --q-color-warning: #f2c037;
      --q-color-dark: #212121;
      --medlink-bg: #f5f5f5;
      --medlink-card-bg: #ffffff;
      --medlink-border: #e0e0e0;
      --medlink-text: #333333;
      --medlink-text-muted: #828999;
      --medlink-hover-row: #ecf1f6;
    }
    body {
      font-family: 'Source Sans Pro', -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background-color: var(--medlink-bg);
      color: var(--medlink-text);
      font-size: 14.5px;
      margin: 0;
      padding: 0;
    }
    #main-header {
      background-color: #ffffff;
      border-bottom: 1px solid #e0e0e0;
      color: #333333;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    .brand-gradient-line {
      height: 3px;
      width: 100%;
      background: linear-gradient(90deg, #0178BC 0%, #318F94 51%, #5EC58C 100%);
    }
    .nav-drawer {
      background-color: #212121 !important;
      color: #e0e0e0 !important;
    }
    .drawer-header {
      border-bottom: 1px solid #333333;
      background-color: #1a1a1a;
      padding: 10px 14px;
    }
    .drawer-header-title {
      font-size: 11px;
      letter-spacing: 0.08em;
      font-weight: 700;
      color: #9e9e9e;
      text-transform: uppercase;
    }
    .nav-drawer .q-item {
      color: #c5c5c5 !important;
      border-left: 4px solid transparent;
      transition: all 0.15s ease;
      min-height: 38px;
      padding: 6px 16px;
    }
    .nav-drawer .q-item:hover {
      background-color: rgba(255, 255, 255, 0.06) !important;
      color: #ffffff !important;
    }
    .nav-active-item {
      background-color: rgba(255, 255, 255, 0.12) !important;
      color: #ffffff !important;
      border-left: 4px solid #4274A7 !important;
      font-weight: 600;
    }
    .nav-active-item .q-icon {
      color: #42a5f5 !important;
    }
    .nav-drawer .q-item .q-icon {
      color: #828999 !important;
    }
    .nav-active-item .q-icon {
      color: #42a5f5 !important;
    }

    .medlink-card {
      border-radius: 4px;
      border: 1px solid var(--medlink-border);
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      background: #ffffff;
    }
    .q-table thead th {
      background-color: #f8fafc;
      color: #475569;
      font-weight: 700;
      font-size: 13px;
      border-bottom: 1px solid #cbd5e1;
    }
    .q-table tbody tr:hover {
      background-color: var(--medlink-hover-row) !important;
    }
    .q-table tbody td {
      font-size: 13.5px;
    }

    /* Enterprise Clean Pipeline Stepper */
    .pipeline-stepper-card {
      background: #ffffff;
      color: var(--medlink-text);
      border-radius: 4px;
      padding: 12px 16px;
      margin-bottom: 16px;
      border: 1px solid var(--medlink-border);
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    .pipeline-step-item {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      padding: 6px 10px;
      border-radius: 4px;
      transition: all 0.15s;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      font-size: 12px;
      color: #475569;
    }
    .pipeline-step-item:hover {
      background: #f1f5f9;
      border-color: #cbd5e1;
    }
    .pipeline-step-active {
      background: rgba(66, 116, 167, 0.08) !important;
      border-color: #4274A7 !important;
      color: #1e3a5f !important;
      font-weight: 600;
    }
    .pipeline-step-done {
      border-color: rgba(33, 186, 69, 0.4) !important;
      background: rgba(33, 186, 69, 0.05) !important;
      color: #1e7e34 !important;
    }
    .pipeline-num-badge {
      width: 20px;
      height: 20px;
      border-radius: 50%;
      background: #cbd5e1;
      color: #475569;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 11px;
      font-weight: 700;
      flex-shrink: 0;
    }
    .pipeline-step-active .pipeline-num-badge {
      background: #4274A7;
      color: #ffffff;
    }
    .pipeline-step-done .pipeline-num-badge {
      background: #21ba45;
      color: #ffffff;
    }

    /* Process Guide & Integration Notes */
    .process-guide-card {
      background: #ffffff;
      border: 1px solid var(--medlink-border);
      border-left: 4px solid #4274A7;
      border-radius: 4px;
      padding: 12px 16px;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      margin-bottom: 16px;
    }
    .guide-box {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-top: 2px solid #4274A7;
      border-radius: 4px;
      padding: 10px 12px;
      height: 100%;
    }
    .guide-box-title {
      font-size: 12px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 6px;
      display: flex;
      align-items: center;
      gap: 6px;
      color: #2c3e50;
    }
    .guide-box-desc {
      font-size: 12.5px;
      color: #475569;
      line-height: 1.5;
    }
    .guide-what { border-top-color: #4274A7; }
    .guide-why { border-top-color: #318F94; }
    .guide-where { border-top-color: #0178BC; }

    .medlink-extend-banner {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-left: 4px solid #4274A7;
      border-radius: 4px;
      padding: 10px 14px;
      margin-bottom: 14px;
      font-size: 13px;
      color: #334155;
    }

    /* Barcode Sticker Preview */
    .barcode-sticker {
      width: 280px;
      border: 2px solid #000000;
      padding: 8px;
      background: #ffffff;
      font-family: 'JetBrains Mono', Consolas, monospace;
      color: #000000;
      margin: 0 auto;
      box-shadow: 0 2px 4px rgba(0,0,0,0.08);
    }
    .barcode-lines {
      height: 40px;
      background: repeating-linear-gradient(90deg, #000 0, #000 2px, #fff 2px, #fff 4px, #000 4px, #000 7px, #fff 7px, #fff 9px);
      margin: 6px 0;
    }

    /* PDF Report Document Sheet */
    .pdf-report-sheet {
      background: #ffffff;
      color: #1e293b;
      padding: 30px;
      max-width: 800px;
      margin: 0 auto;
      border: 1px solid #cbd5e1;
      box-shadow: 0 4px 12px rgba(0,0,0,0.1);
      font-family: 'Source Sans Pro', Arial, sans-serif;
    }
    .pdf-header {
      border-bottom: 2px solid #4274A7;
      padding-bottom: 12px;
      margin-bottom: 16px;
    }
    .pdf-table {
      width: 100%;
      border-collapse: collapse;
      margin: 16px 0;
    }
    .pdf-table th, .pdf-table td {
      border: 1px solid #e2e8f0;
      padding: 6px 10px;
      font-size: 12px;
      text-align: left;
    }
    .pdf-table th {
      background: #f1f5f9;
      font-weight: 700;
      color: #475569;
    }
    .pdf-stamp {
      border: 2px dashed #4274A7;
      border-radius: 50%;
      width: 110px;
      height: 110px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      color: #4274A7;
      font-size: 10px;
      text-align: center;
      transform: rotate(-8deg);
      font-weight: 700;
    }

    /* Charts & Cryo Grid */
    .levey-jennings-container {
      background: #ffffff;
      border: 1px solid var(--medlink-border);
      border-radius: 4px;
      padding: 16px;
      position: relative;
    }
    .lj-svg {
      width: 100%;
      height: 280px;
      overflow: visible;
    }
    .lj-grid-line {
      stroke-dasharray: 4, 4;
      stroke-width: 1.2;
    }
    .lj-point {
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .lj-point:hover {
      r: 6;
      stroke: #4274A7;
      stroke-width: 2.5;
    }
    .cryo-grid-8x12 {
      display: grid;
      grid-template-columns: repeat(12, 1fr);
      gap: 5px;
      background: #f8fafc;
      padding: 10px;
      border-radius: 4px;
      border: 1px solid var(--medlink-border);
    }
    .cryo-cell {
      aspect-ratio: 1;
      border-radius: 3px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 10px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s;
      user-select: none;
      border: 1px solid transparent;
    }
    .cryo-cell:hover {
      transform: scale(1.1);
      z-index: 2;
      box-shadow: 0 2px 4px rgba(0,0,0,0.15);
    }
    .cryo-cell-empty {
      background: #ffffff;
      color: #94a3b8;
      border-color: #e2e8f0;
    }
    .cryo-cell-serum {
      background: #d04f45;
      color: #ffffff;
    }
    .cryo-cell-plasma {
      background: #7c3aed;
      color: #ffffff;
    }
    .cryo-cell-edta {
      background: #4274A7;
      color: #ffffff;
    }
    .cryo-cell-warning {
      background: #f2c037;
      color: #333333;
    }
    .cryo-cell-selected {
      border: 2px solid #212121 !important;
      box-shadow: 0 0 0 2px #4274A7;
      transform: scale(1.1);
      z-index: 3;
    }
    .eucast-scale {
      height: 16px;
      border-radius: 8px;
      position: relative;
      background: linear-gradient(to right, #ef9a9a 0%, #ef9a9a 40%, #ffe082 40%, #ffe082 60%, #a5d6a7 60%, #a5d6a7 100%);
    }
    .eucast-pointer {
      position: absolute;
      top: -4px;
      width: 5px;
      height: 24px;
      background: #212121;
      border-radius: 2px;
      box-shadow: 0 0 3px rgba(0,0,0,0.4);
      transform: translateX(-50%);
    }
    .tat-waterfall-bar {
      height: 22px;
      border-radius: 3px;
      display: flex;
      align-items: center;
      padding: 0 8px;
      font-size: 11px;
      font-weight: 600;
      color: #ffffff;
      transition: width 0.4s ease;
    }"""

# Replace the style block
style_pattern = r"<style>.*?</style>"
html = re.sub(style_pattern, f"<style>\n{css_replacement}\n  </style>", html, flags=re.DOTALL)

# 3. Clean up drawer icon colors: remove color="cyan-2", color="amber-3", color="purple-3", etc. in drawer
# Find drawer block
drawer_match = re.search(r"<q-drawer.*?content-class=\"nav-drawer\".*?>.*?</q-drawer>", html, flags=re.DOTALL)
if drawer_match:
    drawer_content = drawer_match.group(0)
    # Remove icon color attributes inside drawer
    cleaned_drawer = re.sub(r'(<q-icon[^>]*)\s+color="[^"]+"', r'\1', drawer_content)
    # Replace header label color text-cyan-3 with text-grey-5
    cleaned_drawer = cleaned_drawer.replace('text-cyan-3', 'text-grey-5').replace('text-cyan-2', 'text-grey-4')
    # Replace drawer badge colors with calm ones
    cleaned_drawer = cleaned_drawer.replace('color="teal"', 'color="primary"').replace('color="purple"', 'color="grey-8"')
    html = html.replace(drawer_content, cleaned_drawer)

# 4. Clean up stepper text colors
html = html.replace('color="cyan-3"', 'color="primary"')
html = html.replace('text-cyan-3', 'text-primary')
html = html.replace('border-top: 1px solid #334155;', 'border-top: 1px solid #e2e8f0;')

# 5. Save updated files
with open(PROTOTYPE_PATH, "w", encoding="utf-8") as f:
    f.write(html)
with open(INDEX_PATH, "w", encoding="utf-8") as f:
    f.write(html)

print(f"Updated {PROTOTYPE_PATH} and {INDEX_PATH} with restrained MedLink styling.")
