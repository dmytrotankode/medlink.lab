# Підготовка даних для відеоогляду через API запущеного екземпляра (VIDEO_BASE, типово http://localhost:5077):
# страхове замовлення з оплатою, реєстр у Сінево (відправлено), реєстр клініки «Здоров'я». Пише prepared.json.
import io
import json
import os
import time
import urllib.request

BASE = os.environ.get('VIDEO_BASE', 'http://localhost:5077')
REG, TECH, ADMIN = '0e000000-0000-0000-0000-000000000006', '0e000000-0000-0000-0000-000000000003', '0e000000-0000-0000-0000-000000000001'
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'artifacts', 'video')


def call(method, path, emp, body=None, raw=None, ctype='application/json'):
    data = raw if raw is not None else (None if body is None else json.dumps(body).encode())
    req = urllib.request.Request(BASE + path, data=data, method=method, headers={'Content-Type': ctype, 'X-MedLink-Employee-Id': emp})
    with urllib.request.urlopen(req) as r:
        txt = r.read()
        return json.loads(txt) if txt else None


def upload(path, emp, name, content):
    b = '----medlinkvideo'
    body = (f'--{b}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: text/csv\r\n\r\n').encode() + content.encode('utf-8') + f'\r\n--{b}--\r\n'.encode()
    return call('POST', path, emp, raw=body, ctype=f'multipart/form-data; boundary={b}')


for _ in range(120):
    try:
        urllib.request.urlopen(BASE + '/health'); break
    except Exception:
        time.sleep(2)

prepared = {}
# Страхова програма УНІКА: поліс пацієнта Бондаренко
ins = call('POST', '/api/v1/lab/orders', REG, {'patientId': '0b000000-0000-0000-0000-000000000003', 'payerId': 'UNIQA-GOLD', 'profileIds': ['PROF_CBC', 'PROF_LIPID'], 'testIds': ['VITD']})
call('POST', f"/api/v1/lab/orders/{ins['id']}/payments", REG, {'amount': float(ins['patientAmount']), 'method': 'CARD'})
call('POST', f"/api/v1/lab/orders/{ins['id']}/invoice", REG)
prepared['insuranceOrderId'] = ins['id']
for s in ins['samples']:
    call('POST', f"/api/v1/lab/samples/{s['barcode']}/collect", TECH, {'volumeMl': 4})
    call('POST', f"/api/v1/lab/samples/{s['barcode']}/receive", TECH, {})
prepared['barcode'] = ins['samples'][0]['barcode']

# Сінево: реєстр з вітаміном D — відправлено, прийнято
q = call('GET', '/api/v1/lab/send-out/queue', TECH)
ids = [x['orderTestId'] for x in q if x['sampleReady']]
if ids:
    so = call('POST', '/api/v1/lab/send-out', TECH, {'performerId': '0f0f0000-0000-0000-0000-000000000002', 'orderTestIds': ids})
    call('POST', f"/api/v1/lab/send-out/{so['id']}/dispatch", TECH, {'temperature': 4.5, 'courierName': 'Кур\'єр Сінево'})
    call('POST', f"/api/v1/lab/send-out/{so['id']}/accept", TECH, {'externalBatchNumber': 'SYN-778812'})

# Клініка «Здоров'я»: реєстр із двох пацієнтів
csv = ('lastName;firstName;middleName;birthDate;gender;phone;clinicNumber;doctor;tests;cito\n'
       'Савченко;Ірина;Петрівна;14.02.1987;F;+380501234500;ZD-1001;Іваненко О.П.;PROF_CBC,PROF_THYROID;\n'
       'Ткачук;Андрій;Олегович;03.09.1975;M;+380671234500;ZD-1002;Іваненко О.П.;PROF_BIOCHEM_BASE,VITD;так\n')
upload('/api/v1/lab/clients/CLINIC-ZD/registry?dryRun=false', REG, 'zdorovya.csv', csv)

os.makedirs(OUT, exist_ok=True)
json.dump(prepared, io.open(os.path.join(OUT, 'prepared.json'), 'w', encoding='utf-8'), indent=1)
print('prepared', prepared)
