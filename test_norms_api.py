import urllib.request
import json

res = urllib.request.urlopen('http://localhost:8088/api/laboratory/norms/combinations')
data = json.loads(res.read().decode('utf-8'))
print('Combinations count:', len(data.get('data', [])))
for item in data.get('data', [])[:6]:
    print(item.get('test_code'), '|', item.get('norm_name'), '|', item.get('norm_low'), '-', item.get('norm_high'), item.get('unit'))

# Test resolve
req = urllib.request.Request(
    'http://localhost:8088/api/laboratory/norms/resolve',
    data=json.dumps({'test_code': 'PROG', 'gender': 'F', 'menstrual_phase': 'LUTEAL'}).encode('utf-8'),
    headers={'Content-Type': 'application/json'},
    method='POST'
)
res2 = urllib.request.urlopen(req)
data2 = json.loads(res2.read().decode('utf-8'))
print('Resolver PROG Luteal:', data2.get('matched'), data2.get('rule', {}).get('norm_name'), data2.get('rule', {}).get('norm_low'), '-', data2.get('rule', {}).get('norm_high'))
