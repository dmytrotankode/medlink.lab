import urllib.request
import json

# Test 1: Layers list
res = json.loads(urllib.request.urlopen('http://localhost:8088/api/laboratory/norms/layers?test_code=GLU&method_code=HEX_IFCC').read())
print('GLU HEX_IFCC layers count:', len(res['data']))
for l in res['data'][:5]:
    p = l.get("priority_order")
    lt = l.get("layer_type")
    nn = l.get("norm_name")
    nl = l.get("norm_low")
    nh = l.get("norm_high")
    print(f'  [Prio {p}] {lt}: {nn} ({nl} - {nh})')

# Test 2: Resolve cascade for pregnant woman week 20 (should hit Layer 3 Pregnancy T2 with priority 100)
payload = {
    'test_code': 'GLU',
    'method_code': 'HEX_IFCC',
    'gender': 'F',
    'age': 28,
    'is_pregnant': True,
    'pregnancy_week': 20,
    'measured_value': 5.8
}
req = urllib.request.Request(
    'http://localhost:8088/api/laboratory/norms/resolve-cascade',
    data=json.dumps(payload).encode('utf-8'),
    headers={'Content-Type': 'application/json'}
)
cas_res = json.loads(urllib.request.urlopen(req).read())
print('\nCascade resolution result:')
print('  Winning Layer:', cas_res['winning_layer']['norm_name'])
print('  Applied Range:', cas_res['norm_low'], '-', cas_res['norm_high'], cas_res['unit'])
print('  Status Flag:', cas_res['status_flag'])
print('  Audit Trace Steps:', len(cas_res['audit_trace']))
for s in cas_res['audit_trace'][:4]:
    print(f'    Prio {s["priority"]} [{s["layer_type"]}]: matched={s["is_matched"]} -> {s["reason"]}')
