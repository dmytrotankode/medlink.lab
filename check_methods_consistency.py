import re

with open('medlink_lab_frontend/run_prototype.html', 'r', encoding='utf-8') as f:
    c = f.read()

template = c.split('<script>')[0]
calls = set()
for m in re.finditer(r'@[\w.-]+="([^"(]+)', template):
    method = m.group(1).strip()
    if '=' not in method and '!' not in method and not method.startswith('{'):
        # also ignore simple js statements like "showBiomaterialModal = false"
        calls.add(method)

methods_part = c.split('methods: {')[1].split('mounted()')[0]

print('Found event handlers in template:')
missing = []
for call in sorted(calls):
    # check if call is in methods
    is_in = (call + '(' in methods_part) or (call + ' (' in methods_part) or (call + ':' in methods_part)
    print(f'  {call}: in_methods={is_in}')
    if not is_in:
        missing.append(call)

print('\nMISSING METHODS:')
for m in missing:
    print(' ', m)
