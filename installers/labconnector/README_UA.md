# MedLink LIS Analyzer Connector 4.0 — інструкція з встановлення

> **ТОВ «МедЛінк» (MedLink LLC) © 2026.** Коннектор зв'язує лабораторні аналізатори (ASTM E1381/E1394,
> HL7 v2 MLLP, текстові протоколи RAPIDPoint / DRI-CHEM / Integra / Cyan / UC-1000 / IRIS …) із сервером
> MedLink LIS 4.0, працює службою Windows або systemd, буферизує результати при відсутності зв'язку та надає
> локальну сторінку статусу `http://localhost:5088/` і агент друку штрихкод-етикеток (ZPL).

---

## 1. Як це працює

```
 Аналізатор ──TCP/COM/файл──▶ MedLink.LabConnector ──HTTPS──▶ Сервер ЛІС (/api/v1/lab/connector/*)
                                   │   ▲
                 http://localhost:5088/ │ (статус, друк етикеток для веб-інтерфейсу ЛІС)
```

1. Прилад надсилає **запит замовлення** за штрихкодом пробірки (ASTM `Q`, HL7 `QRY^Q02`, RAPID `PAT_DEMOG_REQ`).
   Коннектор питає сервер (`GET /connector/orders/by-barcode/{штрихкод}`) і відповідає приладу у його форматі
   (Cobas `TSDWN^REPLY`, Mindray `DSR^Q03`, Sysmex тощо — шаблон `orderTemplate` профілю).
2. Прилад надсилає **результати** → коннектор розбирає їх (парсер профілю `parserKind`), застосовує мапінг
   параметрів (`parameterMap`: код приладу → код тесту ЛІС, коефіцієнт, одиниця) і відправляє
   `POST /connector/results`. Якщо сервер недоступний — результати зберігаються в **офлайн-буфер**
   (`data/offline_buffer.db`) і відправляються автоматично, щойно зв'язок відновиться.
3. Для приладів без режиму запиту (`autoQueryOrders=false`) коннектор періодично завантажує чергу замовлень
   (`GET /connector/orders/pending`) та передає її приладу пакетом (HumaStar, Bio-Ksel …).
4. Усі повідомлення (вхідні та вихідні, у повному вигляді) журналюються на сервері (`/connector/messages`)
   та локально у `data/logs/raw-<КОД>-дата.log`.

Конфігурація приладів (протокол, порт/COM, профіль типу, мапінг параметрів) ведеться **на сервері ЛІС**
у розділі **Аналізатори** та прив'язується до коннектора. Коннектор отримує її командою `setup` і далі —
при кожному heartbeat (кожні 30 с), а також за командою «Перечитати конфігурацію».

---

## 2. Отримання ключа інсталяції у веб-інтерфейсі ЛІС

1. Увійдіть у MedLink LIS як **адміністратор лабораторії** (роль `LAB_ADMIN`).
2. Меню **Лабораторія → Аналізатори → вкладка «Коннектори»** → кнопка **«Додати коннектор»**.
3. Вкажіть назву (наприклад, «Лабораторія, ПК біля Cobas») → ЛІС створить запис зі статусом `PENDING`
   та покаже **одноразовий ключ інсталяції** (`installKey`) і готову команду `setup`.
   Ключ діє до першого використання; при потребі натисніть **«Перевипустити ключ»** (`rotate-key`).
4. У картці коннектора додайте/прив'яжіть **аналізатори**: тип із каталогу (64 профілі Simplex:
   Cobas c111/c311/e411, Sysmex XN/XS/CS-2500/CA-600, Mindray BS-240/BS-30/C3100, Pentra, Tosoh, Stago,
   Maglumi, HumaStar, Beckman Access, RAPIDPoint, DRI-CHEM, Integra, Urisys, iChroma …), режим зв'язку
   (TCP-сервер / TCP-клієнт / COM / файл), параметри порту та мапінг кодів тестів.
5. Після встановлення коннектора його статус зміниться на `ACTIVE`, а на сторінці з'являться heartbeat,
   кількість буферизованих результатів і журнал обміну.

---

## 3. Встановлення на Windows

### 3.1. Майстер інсталяції (рекомендовано)

1. Запустіть `MedLink_LabConnector_Setup_v4.0.0.exe` **від імені адміністратора**.
2. На кроці **«Підключення до сервера ЛІС»** введіть адресу сервера (`https://lis.clinic.ua`) і ключ
   інсталяції, назву коннектора; за потреби змініть порт сторінки статусу (5088).
3. (Необов'язково) **«Друк етикеток»**: ім'я принтера Windows (RAW/ZPL, напр. `ZDesigner GK420t`) або
   IP мережевого принтера (порт 9100).
4. Майстер скопіює файли у `C:\Program Files\MedLink\LabConnector`, виконає реєстрацію
   (`MedLink.LabConnector.exe setup …`), створить службу **MedLinkLabConnector** (запуск «автоматично
   (відкладено)», автоперезапуск при збоях), додасть правила брандмауера та ярлики
   «Статус коннектора MedLink» (меню Пуск / робочий стіл).

### 3.2. PowerShell без інсталятора

```powershell
# з каталогу публікації win-x64 (publish.ps1 -Rid win-x64) або розпакованого zip
.\install-windows.ps1 -ServerUrl https://lis.clinic.ua -InstallKey XXXX-XXXX -Name "Лабораторія ПК1" `
                      -PrinterName "ZDesigner GK420t"
# видалення
.\uninstall-windows.ps1            # лише служба та брандмауер
.\uninstall-windows.ps1 -RemoveFiles -RemoveData
```

### 3.3. Вручну (CLI)

```bat
cd "C:\Program Files\MedLink\LabConnector"
MedLink.LabConnector.exe setup --server https://lis.clinic.ua --install-key XXXX-XXXX --name "Лаб ПК1"
MedLink.LabConnector.exe install-service
MedLink.LabConnector.exe status
```

---

## 4. Встановлення на Linux (Debian/Ubuntu, systemd)

```bash
# варіант А — скрипт
sudo ./install-linux.sh --server https://lis.clinic.ua --install-key XXXX-XXXX --name "Лаб ПК1" \
                        [--printer-name Zebra_GK420t | --printer-host 192.168.1.50]

# варіант Б — .deb
sudo apt install ./medlink-labconnector_4.0.0_amd64.deb
sudo -u medlink /opt/medlink/labconnector/MedLink.LabConnector setup --server https://lis.clinic.ua \
     --install-key XXXX-XXXX --data-dir /var/lib/medlink/labconnector
sudo systemctl start medlink-labconnector

# перевірка
systemctl status medlink-labconnector
journalctl -u medlink-labconnector -f
/opt/medlink/labconnector/MedLink.LabConnector status
```

Файли: програма `/opt/medlink/labconnector`, дані `/var/lib/medlink/labconnector`
(`appsettings.local.json`, `config.cache.json`, `offline_buffer.db`, `logs/`). Служба працює від
користувача `medlink` (групи `dialout` — COM/USB-порти, `lp` — принтери).

### Docker

```bash
docker build -f installers/labconnector/Dockerfile -t medlink/labconnector:4.0 .
docker run --rm -v labconnector-data:/data medlink/labconnector:4.0 setup --server https://lis.clinic.ua --install-key XXXX
docker run -d --name labconnector --restart unless-stopped -p 5088:5088 -p 5100:5100 \
  --device /dev/ttyUSB0:/dev/ttyUSB0 -v labconnector-data:/data medlink/labconnector:4.0
```
Порти TCP-серверних приладів (`-p`) та COM-пристрої (`--device`) додайте відповідно до конфігурації.

---

## 5. Команди CLI

| Команда | Призначення |
|---|---|
| `run` | запуск служби (за замовчуванням) |
| `setup --server URL --install-key KEY [--name NAME] [--data-dir DIR] [--status-port 5088] [--insecure]` | реєстрація на сервері, запис `data/appsettings.local.json`, завантаження конфігурації |
| `test --analyzer CODE --file PATH [--json]` | прогнати файл повідомлення через парсер (CODE — код аналізатора з конфігурації або код типу з каталогу, напр. `COBAS411`) |
| `preview-order --analyzer CODE --barcode B [--tests GLU,ALT] [--query-token "1^B^S1^SC"] [--position 7]` | показати замовлення, яке буде відправлено приладу (записи + кадри ASTM / MLLP) |
| `status [--json]` | стан запущеної служби (`GET /status.json`) |
| `install-service` / `uninstall-service` | служба Windows (sc.exe) або systemd unit |
| `list-profiles [--json]` | каталог 64 профілів типів аналізаторів |

Змінна `MEDLINK_CONNECTOR_DATA` або `--data-dir` задає каталог даних (за замовчуванням `data` біля програми).

---

## 6. Сторінка статусу та агент друку (`http://localhost:5088/`)

- Стан зв'язку із сервером, версія конфігурації, офлайн-буфер, список аналізаторів (протокол, підключення,
  останнє повідомлення, лічильники, остання помилка), останні 50 повідомлень.
- Кнопки: **Перечитати конфігурацію**, **Відправити тестове повідомлення** (ASTM: ENQ→ACK→EOT тест лінії),
  **Очистити буфер**. JSON: `GET /status.json`, `GET /api/messages`, `GET /healthz`.
- **Друк етикеток** для веб-інтерфейсу ЛІС (CORS увімкнено для будь-якого походження):

| Метод | Шлях | Тіло / відповідь |
|---|---|---|
| `POST` | `/print/zpl` | `{ "zpl": "^XA…^XZ", "printer": { "host": "192.168.1.50", "port": 9100 } \| { "name": "ZDesigner GK420t" } \| null, "copies": 1 }` → `{ ok, message, printer, bytes }` (502 при помилці) |
| `GET` | `/print/printers` | `{ "printers": [...], "default": "…" }` — Windows: `Get-Printer`/`wmic`, Linux: `lpstat -a` |
| `POST` | `/print/test` | друк тестової етикетки 40×25 мм (тіло — необов'язковий `printer`) |

Принтер за замовчуванням: `appsettings.json` / `appsettings.Printing.json` → `Printing:DefaultLabelPrinter`
(черга ОС) або `Printing:Host` / `Printing:Port` (мережевий, порт 9100, таймаут 5 с). Windows друкує RAW через
`winspool` (OpenPrinter/StartDocPrinter/WritePrinter), Linux — `lp -d NAME -o raw`.

---

## 7. Файли конфігурації

| Файл | Хто пише | Вміст |
|---|---|---|
| `appsettings.json` | інсталятор (не змінюється setup) | значення за замовчуванням, `Local:*`, `Printing:*`, локальні `Analyzers` |
| `appsettings.Printing.json` | інсталятор (параметри принтера) | `Printing:*` |
| `data/appsettings.local.json` | `setup` | `Server:BaseUrl`, `Server:ApiKey`, `Server:ConnectorId`, назва, порт статусу |
| `data/config.cache.json` | служба | остання конфігурація із сервера (робота офлайн) |
| `data/offline_buffer.db` | служба | SQLite-черга невідправлених результатів/журналу |
| `data/logs/connector-дата.log`, `raw-<КОД>-дата.log` | служба | журнал служби та «сирий» трафік приладів (зберігаються 14 днів) |

Локальні аналізатори без сервера: секція `Analyzers` в `appsettings.json` (та сама форма, що в конфігурації
сервера — див. приклади у файлі). Локальні записи перекривають серверні за `code`/`analyzerId`.

---

## 8. Усунення несправностей

**COM-порт (Windows).** Переконайтесь, що порт видно у Диспетчері пристроїв (Порти COM і LPT) і його не займає
інша програма (стара AconnectAstm, термінал). Параметри (швидкість/парність/стоп-біти) мають збігатися з
налаштуваннями приладу; типові: 9600-8-N-1, для Cobas c111 — 9600-8-N-1, Sysmex — 9600 або 19200.
Для USB-перехідників ставте драйвер виробника (FTDI/Prolific); після переключення USB-гнізда номер COM може
змінитись — оновіть його у картці аналізатора в ЛІС і натисніть «Перечитати конфігурацію».
**COM-порт (Linux).** `ls -l /dev/ttyUSB* /dev/ttyS*`; користувач `medlink` має бути у групі `dialout`
(`groups medlink`); у картці аналізатора вкажіть `/dev/ttyUSB0`. `dmesg | tail` покаже, чи розпізнано перехідник.

**TCP.** Для режиму «TCP-сервер» (прилад підключається до ПК) відкрийте порт у брандмауері
(інсталятор додає правило для портів із конфігурації; при додаванні нових приладів — повторно запустіть
`install-windows.ps1` або додайте правило вручну: `netsh advfirewall firewall add rule name="MedLink LabConnector"
dir=in action=allow protocol=TCP localport=5100`). У приладі вкажіть IP цього ПК та порт. Для режиму
«TCP-клієнт» перевірте доступність: `Test-NetConnection 192.168.1.101 -Port 5100` / `nc -vz 192.168.1.101 5100`.
Коннектор перепідключається кожні 5 с і надсилає keep-alive кожні 60 с тиші.

**Сервер ЛІС недоступний.** Сторінка статусу показує «офлайн» і зростання буфера. Результати не втрачаються —
відправляться автоматично. Перевірте `https://lis…/api/v1/lab/connector/config` з браузера, сертифікат
(для самопідписаних — `Server:AllowInvalidCertificate=true` або `setup --insecure`), проксі.
Ключ `X-MedLink-ApiKey` відкликано (HTTP 401) — перевипустіть у ЛІС і виконайте `setup` повторно.

**Прилад не отримує замовлення.** Переконайтесь, що пробірка зареєстрована у ЛІС зі штрихкодом саме таким,
як сканує прилад (`test --analyzer … --file …` покаже розпізнаний штрихкод), а тести мають коди приладу
(`parameterMap`/`analyzerCode`). Перегляньте `data/logs/raw-*.log`: `Q|1|…` → `→ ASTM: 4 кадр(ів) підтверджено`.
Якщо прилад відповідає NAK на кадри — перевірте опцію `checksum` (controlSum) у профілі та довжину кадру
(`maxFrameLen`, для Cobas — поділ списку тестів 165/170 виконується автоматично).

**Служба не стартує.** Windows: Переглядач подій → Журнали Windows → Програми (джерело «MedLink LIS Analyzer
Connector») та `data\logs\connector-*.log`. Linux: `journalctl -u medlink-labconnector -n 100`. Типова причина —
зайнятий порт 5088 (`netstat -ano | findstr 5088`) → змініть `Local:StatusPort`.

**Друк етикеток.** `GET http://localhost:5088/print/printers` має показати принтер; `POST /print/test` друкує
тест. Для мережевого Zebra перевірте порт 9100 (`Test-NetConnection IP -Port 9100`). Для локальної черги на
Windows принтер має використовувати драйвер «Generic / Text Only» або рідний ZPL-драйвер (RAW-дані передаються
без обробки).

---

## 9. Збірка з вихідних кодів

```bash
./installers/labconnector/publish.sh                 # win-x64 + linux-x64 + linux-arm64 → artifacts/labconnector/<rid>/
./installers/labconnector/build-deb.sh linux-x64     # .deb
# Windows: .\installers\labconnector\publish.ps1 -Rid win-x64 -BuildInstaller   (Inno Setup 6)
dotnet test                                          # 96 тестів протоколів/коннектора
```

Підтримка: support@medlink.ua · Документація контракту: `docs/API_CONTRACT.md` (розділи 3, 4, 6).
