# -*- coding: utf-8 -*-
with open('script_0.js', 'r', encoding='utf-8') as f:
    text = f.read()

# Look for Ukrainian words with apostrophe inside single quotes:
# З'єднання, об'єм, пам'ять, кур'єр, зв'язок, ім'я, тощо
replacements = [
    ("'З'єднання'", "'З\\'єднання'"),
    ("'з'єднання'", "'з\\'єднання'"),
    ("'об'єм'", "'об\\'єм'"),
    ("'кур'єр'", "'кур\\'єр'"),
    ("'зв'язок'", "'зв\\'язок'"),
    ("'ім'я'", "'ім\\'я'"),
    ("label: 'З'єднання'", "label: 'З\\'єднання'"),
    ("label: 'Об'єм'", "label: 'Об\\'єм'")
]

for old, new in replacements:
    text = text.replace(old, new)

with open('script_test.js', 'w', encoding='utf-8') as f:
    f.write(text)

print("Saved script_test.js, checking syntax with node...")
