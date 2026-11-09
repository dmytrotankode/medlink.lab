# Готовність MedLink LIS 4.0 до PostgreSQL (production)

Дата звіту: 2026-11-09. Стенд: https://med-one.avowiz.com/lab/ (сервер 94.130.180.117, PostgreSQL 17.11, БД `medlink_lab`, тестова БД `medlink_lab_test`, роль `lab_stand`).
Стан: PostgreSQL — **основна СУБД**; SQLite залишено лише як dev/тестовий провайдер.

## 1. Головне, що знайдено і виправлено

| № | Знахідка | Наслідок | Виправлення |
|---|---|---|---|
| 1 | **Тести з `LIS_TEST_PG` насправді йшли на SQLite.** `Program.cs` читав `Database:Provider` у коді верхнього рівня, де перевизначення з `WebApplicationFactory` ще не видно; PG лише очищувалась (`DROP SCHEMA`). | «Прогін на PG» нічого не перевіряв. | Провайдер визначається лінивo з остаточної конфігурації (`Program.cs`: `UsePostgres()`); перевірено: після прогону в `medlink_lab_test` є таблиці й дані. |
| 2 | Пошук (`Contains` у LINQ) на PG **чутливий до регістру** (кирилиця: «іванов» не знаходило «Іванов»). SQLite теж не збігав регістр кирилиці. | Пошук за ПІБ/номером/кодом працював лише при точному регістрі. | `Data/PgQueryInterceptor.cs`: `Contains/StartsWith/EndsWith(string)` → `ILIKE … ESCAPE '\'` з екрануванням `%`, `_`, `\`. Працює для всіх наявних і майбутніх запитів без змін у сервісах. |
| 3 | Колація БД `en_US.UTF-8` сортує українську **неправильно** (Ґ перед А, Ї перед І). | Списки пацієнтів/довідників у хибному порядку. | Той самий перехоплювач: `ORDER BY` за «людськими» текстовими полями (`*Name`, `Caption`, `Title`, `Description`, `Address`, `Comment`, `Note`, `Position`, `Specialty`) → `COLLATE "uk-x-icu"`; колація перевіряється при старті (якщо її немає в кластері — вимикається з попередженням). Налаштування `Database:Collation` (порожньо = вимкнено). |
| 4 | Лічильники нумераторів: окремі `UPDATE` скидання періоду та інкременту + створення рядка через `SaveChanges` (гонка перших викликів, побічний «зайвий» `SaveChanges` чужих змін). | Теоретичні дублі/помилка унікального ключа при паралельних запитах, особливо на межі місяця. | `NumeratorService`, `SectionJournalService`: створення `INSERT … ON CONFLICT DO NOTHING`, скидання періоду + інкремент **одним** `UPDATE … RETURNING` (рядок блокується, PG перевіряє умову повторно). Тест `Counters_and_numerators_do_not_duplicate_under_parallel_calls` (16 потоків × 12 викликів: без дублів і пропусків, перехід періоду). |
| 5 | Нові індекси моделі EF на **вже наявній** БД ніколи не створювалися (`SchemaUpgrader` додавав лише таблиці й колонки). | Продуктивність наявних стендів відставала від моделі. | `SchemaUpgrader` створює відсутні індекси моделі (з урахуванням обрізання імен PG до 63 байт); розбіжності **типів** колонок лише повідомляються (`DetectDriftAsync`), не змінюються автоматично. |
| 6 | Помилки БД PG віддавалися як 409 з англійським текстом СУБД. | Користувач бачив технічні повідомлення; перевищення довжини `varchar` (PG на відміну від SQLite його контролює) — як «409». | `ProblemDetailsMiddleware`: 23505→409 «Дублікат», 23503→409, 23502→400 (поле), 22001→400 «Занадто довге значення», 40001/40P01→409 «повторіть дію». |
| 7 | Пул з'єднань/таймаути не налаштовані. | Невизначена поведінка під навантаженням. | `Data/PgConnectionOptions.cs` додає відсутні: `Maximum Pool Size=40`, `Minimum Pool Size=2`, `Timeout=15`, `Command Timeout=60`, `Keepalive=30`, `Application Name=medlink-lab`; явно задані значення не перевизначаються. Повтор при тимчасових збоях з'єднання (`EnableRetryOnFailure(3, 5 с)`; явних транзакцій у коді немає). |
| 8 | Резервування БД `medlink_lab` **не було** (тільки системний `dpkg-db-backup`). | Втрата даних при збої. | Встановлено `medlink-lab-backup.timer` (щодня 02:40 UTC), перевірено відновлення (розд. 5). |
| 9 | Після завершення роботи `Program.cs` створював каталог `App_Data` і логував шлях SQLite навіть на PG. | Шум. | Лог показує «PostgreSQL». |

## 2. Аудит коду на SQLite-залежності

- `grep` по `Sqlite|PRAGMA|datetime(|strftime|rowid|INSERT OR|COLLATE NOCASE|AUTOINCREMENT|ExecuteSqlRaw|FromSqlRaw|SqlQuery` у `src/`: SQLite-специфічного SQL у робочому коді **немає**, окрім гілок `IsSqlite()` у `SchemaUpgrader` (`sqlite_master`, `pragma_table_info`), `Program.cs` (`PRAGMA journal_mode=WAL` лише для SQLite), `GuideExtensions` (гілка SQLite) і `EhealthOutbox` (на SQLite 1 паралельна задача, на PG — `MaxParallel`).
- Весь сирий SQL у `NumeratorService`/`SectionJournalService` — стандартний (`UPDATE … RETURNING`, `INSERT … ON CONFLICT`), працює на SQLite ≥ 3.35 і PostgreSQL.
- Типи: GUID — `varchar(64)` (рядкові GUID за правилами проєкту; у DDL для evomis — `uuid`), `DateTime` — `timestamp without time zone` зі значеннями UTC (**legacy-режим Npgsql** `Npgsql.EnableLegacyTimestampBehavior`, вмикається першим рядком `Program.cs` і в тестах `ModuleInitializer`); `decimal` — `NUMERIC(18,2)` (усі decimal у моделі — гроші та відсотки; результати досліджень не decimal); `bool` — `boolean`; JSON-колонок у моделі немає (текст).
- Колонка-рядок з `MaxLength` на PG є `varchar(n)` і **перевіряється** (на SQLite ні) — див. п.6 таблиці вище. Тест `Postgres_schema_is_versioned_and_matches_model` перевіряє, що типи колонок БД збігаються з моделлю.
- Паралелізм: `UPDATE … RETURNING` (нумератори, лічильники штрихкодів) атомарні; унікальні індекси `lab_order(order_number)`, `lab_order_sample(barcode)` — остання страховка; outbox ЕСОЗ захоплює рядок атомарним `UPDATE … WHERE id=@id AND status IN (…)`.
- Не охоплено: `EhealthFactory` (тести ЕСОЗ) досі працює лише на SQLite (окрема фабрика без `LIS_TEST_PG`); тест паралельного захоплення outbox на PG — окрема задача (потрібна окрема БД на фабрику, щоб не скидати схему одна одній).

## 3. Версіонування схеми (PostgreSQL)

**Вибір: версіоновані ідемпотентні SQL-скрипти + журнал, а не EF Migrations.** Обґрунтування:
- У проєкті немає EF-міграцій; модель має сотні сутностей і підтримується кількома агентами паралельно — згенеровані `Migration`-класи постійно конфліктували б (снапшот моделі);
- `SchemaUpgrader` + `EnsureCreated` вже покривають найчастіший випадок — **додавання** таблиць/колонок/індексів з моделі (безпечно, без втрати даних);
- решта (індекси-вирази, trigram, розширення, зміна типів, перейменування, виправлення даних) — це якраз те, що EF Migrations генерує погано і що треба переглядати очима;
- ті самі `*.sql` можна виконати вручну/в DBA-процесі evomis.

Механізм (`Data/PgSchemaMigrator.cs`):
- скрипти `db/postgres/upgrades/NNNN_назва.sql` вбудовані в збірку; при старті на PostgreSQL застосовуються за зростанням номера;
- журнал `lab_schema_version(version, name, checksum, applied_at, duration_ms)`; **один екземпляр за раз** (`pg_advisory_lock`); кожен скрипт і запис у журнал — в одній транзакції (збій = повний відкат, старт API зупиняється з чітким повідомленням); скрипт з першим рядком `-- no-transaction` виконується поза транзакцією (для `CREATE INDEX CONCURRENTLY`);
- застосований скрипт **не редагується**: контрольна сума змінилась → попередження в журналі API; зміна = новий файл;
- перевірка в CI/тестах: номери послідовні, версії в БД = версіям у збірці, розбіжностей типів немає, повторний запуск нічого не робить.

Поточні скрипти: `0001_extensions` (pg_trgm, не падає без прав), `0002_search_and_list_indexes` (індекси, розд. 6).

### Ризики змін схеми на production і правила

| Зміна | Ризик | Правило |
|---|---|---|
| Нова колонка NOT NULL | Старі рядки — потрібне значення | `SchemaUpgrader` додає з `DEFAULT` за типом (безпечно); для змістовного значення — окремий скрипт `UPDATE`. |
| Нова колонка / таблиця / індекс моделі | Низький; `CREATE INDEX` блокує запис на час побудови | Автоматично. Великі таблиці (>1 млн рядків) — індекс окремим скриптом `-- no-transaction` + `CONCURRENTLY`. |
| Унікальний індекс на наявних даних | Дублі → помилка | Помилка створення не зупиняє старт: потрапляє в журнал як `ПОМИЛКА створення індексу…`; виправити дані скриптом. |
| Зміна типу / довжини колонки | Переписування таблиці, блокування, втрата даних | Тільки скриптом з перевіркою даних; `DetectDriftAsync` показує розбіжність у журналі при старті. Автоматично не змінюється ніколи. |
| Перейменування / видалення колонки | Падіння старої версії API під час викату | Двоетапно: (1) додати нову + читати обидві, (2) через реліз видалити стару. |
| Два екземпляри API при старті | Гонка `EnsureCreated`/міграцій | Міграції захищені advisory lock; перший запуск порожньої БД — один екземпляр. |

`db/postgres/medlink_lis_schema.sql` (DDL для перенесення в evomis) відповідає моделі — тест `PostgresDdlTests.Committed_schema_matches_model` проходить; EF-модель цією роботою **не змінювалась**, тому файл не перегенеровувався.

## 4. Тести на обох провайдерах

- `bash tools/deploy/pg-test-on-server.sh --both` — спочатку SQLite, потім PostgreSQL (`medlink_lab_test` на стенді); код виходу ≠ 0, якщо впав будь-який; повні журнали на сервері (`/tmp/lab-test-sqlite.log`, `/tmp/lab-test-pg.log`), на екран — підсумок і перші повідомлення впалих.
- CI: job `backend-postgres` у `.github/workflows/ci.yml` (сервіс `postgres:16`, `LIS_TEST_PG`), поруч із SQLite-job `backend`.
- `appsettings.Testing.json` (Provider=Sqlite): тестові фабрики без явного провайдера (ЕСОЗ) лишаються на SQLite попри те, що `appsettings.json` тепер за замовчуванням Postgres.
- Нові тести `tests/MedLink.LIS.Tests/Api/PostgresReadinessTests.cs` (7): пошук без регістру + екранування `% _`, українське сортування, паралельні лічильники/нумератори/штрихкоди, версіонування схеми, параметри підключення.
- **Результат** — див. розд. 8.

## 5. Резервування й відновлення

Налаштовано на стенді (лише ресурси лабораторії): `/usr/local/sbin/medlink-lab-pg-backup`, `/usr/local/sbin/medlink-lab-pg-restore`, `medlink-lab-backup.service` + `.timer` (щодня 02:40 UTC ± 5 хв, `Persistent=true`), каталог `/var/backups/medlink-lab` (0700, root). Встановлення/оновлення: `bash tools/deploy/pg-backup-install.sh`.

- Що копіюється: `pg_dump -Fc` БД `medlink_lab` (схеми `public` та `guide`), стиснення; перевірка `pg_restore --list` і наявності `lab_order`; розмір на стенді ≈ 1,6 МБ.
- Ротація: 14 останніх щоденних (`daily/`) + 8 тижневих (`weekly/`, копія недільного). Мітка успіху: `/var/backups/medlink-lab/last_ok`; збій = unit `failed` (`systemctl status medlink-lab-backup`, `journalctl -u medlink-lab-backup`).
- Ручний запуск: `systemctl start medlink-lab-backup.service`.
- **Перевірка відновлення (безпечно, живу БД не чіпає):** `medlink-lab-pg-restore --verify [файл.dump]` — відновлює у тимчасову БД, порівнює кількості рядків із живою, видаляє тимчасову. Виконано 2026-11-09: однакові `lab_order=29, mis_patient_card=6, lab_test_result=76, lab_audit_log=374, guide.gd_scenario=296`.
- **Відновлення на місце:** `medlink-lab-pg-restore --replace-live /var/backups/medlink-lab/daily/<файл>.dump` — зупиняє `medlink-lab`, робить страхову копію `before-restore-*.dump`, перестворює БД з тим самим власником, відновлює, запускає службу, друкує кількості (запитує підтвердження `yes`; `ASSUME_YES=1` для автоматизації). Не запускалось на живій БД (лише `--verify`).
- Ручне відновлення без скрипта: `runuser -u postgres -- pg_restore --no-owner --role=lab_stand --clean --if-exists -d medlink_lab < файл.dump` (при зупиненій службі).
- Роль `lab_stand` у дамп не входить (дамп без ролей): при відновленні на новий сервер створіть роль і БД (`CREATE ROLE lab_stand LOGIN PASSWORD …; CREATE DATABASE medlink_lab OWNER lab_stand;`) до `pg_restore`.
- **Не покрито:** копії лежать на тому самому диску сервера — потрібне винесення за межі хоста (rsync/S3) на production; шифрування дампів; моніторинг свіжості `last_ok`.

## 6. Продуктивність (PostgreSQL 17, тестова БД `medlink_lab_test`, 100 000 замовлень, 30 000 пацієнтів, 300 000 показників, 282 000 результатів, 300 000 записів аудиту)

Дані згенеровано `tools/deploy/pg-perf-seed.sql` (лише в `medlink_lab_test`), запити — `tools/deploy/pg-perf-queries.sql` (форма як у SQL, що генерує EF; `EXPLAIN ANALYZE`). Час виконання на боці СУБД, мс; «до» — лише індекси моделі EF, «після» — із `0002_search_and_list_indexes`.

| Запит | до | після | Зміна |
|---|---:|---:|---|
| Q1 список замовлень (CITO першими, дата ↓, стор. 50) | 77,5 | 4,2 | partial-індекс `(is_urgent_cito DESC, order_datetime DESC) WHERE record_state<>4` |
| Q1c лічильник списку | 27,6 | 20,9 | index-only scan |
| Q2 черга (активні статуси) | 38,0 | 4,2 | той самий індекс |
| Q3 пошук за фрагментом номера замовлення | 58,7 | 0,4 | trigram GIN |
| Q4 пошук пацієнта: ПІБ/телефон/ІПН (OR по двох таблицях) | 65,2 | 85,6 | **погіршення**: планувальник читає за індексом абетки; пошук з `OR` по `mis_patient_card` + `cmn_person` не може використати trigram — див. рекомендації |
| Q4b пошук пацієнта лише за ПІБ (8 % збігів) | 30,3 | 30,5 | без змін |
| Q5 проба за штрихкодом | 0,85 | 0,04 | — (унікальний індекс) |
| Q6 показники + результати замовлення | 0,75 | 0,10 | — |
| Q7 / Q7b журнал аудиту сутності / останні 100 | 0,06 / 0,12 | 0,04 / 0,12 | — |
| Q8 робочий список «очікують» (join + сортування) | 140,2 | 56,4 | `(status, order_id)` на `lab_order_test` |
| Q9 список пацієнтів за абеткою | 28,3 | 0,06 | індекс `(caption COLLATE "uk-x-icu") WHERE record_state<>4` |
| Q10 замовлення пацієнта | 0,66 | 0,11 | — |
| Q11 замовлення за 30 днів (аналітика) | 14,4 | 2,2 | — |

Розмір `lab_order` разом з індексами на 100 000 рядків — 122 МБ; `mis_patient_card` — 29 МБ.

HTTP (живий стенд, 29 замовлень, `curl` з сервера): список замовлень 28 КБ — 7–8 мс; worklist (100) — 19 мс; пошук пацієнтів — 85 мс (перший виклик, JIT); `/health` — 22 мс. Нижче — відповіді API на 100 000 замовлень.

**Відповіді API** на тих самих 100 000 замовлень (`tools/deploy/pg-perf-api.sh`: тимчасовий екземпляр збірки на 127.0.0.1:5291 проти `medlink_lab_test`, 5 викликів, мс; помилок у журналі API — 0):

| Маршрут | мін | середнє |
|---|---:|---:|
| `GET /orders?pageSize=50` (список) | 27 | 73 |
| `GET /orders?status=NEW,RECEIVED,IN_PROGRESS` (черга, ≈6 000 активних) | 20 | 38 |
| `GET /orders?search=<фрагмент номера>` | 958 | 1009 |
| `GET /orders?search=<ПІБ>` | 970 | 999 |
| `GET /patients?search=<ПІБ>` (30 000 пацієнтів) | 145 | 158 |
| `GET /patients?pageSize=50` (за абеткою) | 20 | 24 |
| `GET /worklist?pageSize=500` (300 000 показників) | 469 | 540 |
| `GET /health` | 7 | 8 |

**Головна знахідка продуктивності — пошук замовлень (≈1 с на 100 000).** `OrderService.ListAsync(search)` будує один `WHERE` з `OR` по `lab_order.order_number`, `mis_patient_card.caption`, `cmn_person.phone/ipn` та `EXISTS` по пробах: через `OR` по кількох таблицях жоден індекс (у т.ч. trigram) не використовується, виконується повне сканування з'єднання (584 мс лише на стороні СУБД). Те саме запитання через об'єднання ідентифікаторів виконується за **4–10 мс** (на тих самих даних і індексах):

```sql
SELECT o.id FROM lab_order o WHERE o.record_state <> 4 AND o.id IN (
  SELECT id FROM lab_order WHERE order_number ILIKE :p
  UNION SELECT o2.id FROM lab_order o2 WHERE o2.patient_id IN (
        SELECT m.id FROM mis_patient_card m WHERE m.caption ILIKE :p
        UNION SELECT m.id FROM mis_patient_card m JOIN cmn_person c ON c.id = m.person_id WHERE c.phone ILIKE :p OR c.ipn ILIKE :p)
  UNION SELECT order_id FROM lab_order_sample WHERE barcode = :s)
ORDER BY o.is_urgent_cito DESC, o.order_datetime DESC LIMIT 50;
```
У LINQ: окремо вибрати `patientIds` (картка за `Caption.Contains(s)`; персона за `Phone/Ipn.Contains(s)`), `orderIds` за `OrderNumber.Contains(s)` і за штрихкодом, потім `Where(o => orderIds.Contains(o.Id) || patientIds.Contains(o.PatientId))`. **Це бізнес-логіка сервісів, не змінювалась** (передано власникам `OrderService`/`PatientService`/`WorkReportsService`: той самий шаблон `OR` у `WorkReportsService`, `EhealthSignService`).

Пул і навантаження: `max_connections` кластера = 100 (спільний з іншими службами хоста), `shared_buffers` 128 МБ, `work_mem` 4 МБ (типові; кластер не змінювався — він спільний). Ліміт пулу API 40 з'єднань. Для production рекомендовано `shared_buffers` ≥ 25 % ОЗП, `effective_cache_size` 50–75 %, `work_mem` 16–32 МБ (Q8 на 100 000 замовлень сортує частково на диску при 4 МБ), `log_min_duration_statement=500`.

Старт API на PG: на порожній БД (схема + сіди + демо) ≈ 40 с (одиничні `SaveChanges`); на наявній БД зі 100 000 замовлень — ≈ 19 с (сіди, перерахунок готовності каталогу, міграції). `Lab:SeedDemoData=false` для production.

## 7. Перенесення даних SQLite → PostgreSQL

На стенді `/srv/medlink-lab/data/medlink_lis.db` (2,9 МБ) і `medlink_guide.db` містять **лише демонстраційні дані** (сід + демо; 33 замовлення, 2531 рядків у 106 таблицях; Guide — 161 рядок): жодних реальних даних. PG-база `medlink_lab` створена та засіяна самостійно (ті самі детерміновані id демо-сідера).
Для будь-яких інших наявних SQLite-баз (пілот, чиясь копія) підготовлено `tools/db/sqlite-to-pg.py`:

```bash
# на сервері від root; dry-run за замовчуванням
python3 sqlite-to-pg.py --sqlite /шлях/medlink_lis.db --pg-db medlink_lab            # підсумок (таблиці, рядки)
python3 sqlite-to-pg.py --sqlite /шлях/medlink_lis.db --pg-db medlink_lab --apply     # перенести
python3 sqlite-to-pg.py --sqlite /шлях/medlink_guide.db --pg-db medlink_lab --schema guide --apply
```
Ідемпотентно (`INSERT … ON CONFLICT DO NOTHING`, одна транзакція, `session_replication_role=replica`), приводить типи (boolean 0/1, timestamp, bytea), для нових обов'язкових колонок PG, яких немає в старій SQLite, підставляє нейтральні значення, після `--apply` порівнює кількості рядків по кожній таблиці (код 3 при розбіжності). Перевірено 2026-11-09 на копії (відновлена з дампа БД): 106/106 таблиць, 2531 рядок, повторний запуск без змін, Guide 10/10 таблиць. **Спершу — резервна копія цільової БД** (`systemctl start medlink-lab-backup`).

## 8. Підсумок прогону і що лишилось

**Остаточний прогін на стенді 2026-11-09** (`bash tools/deploy/pg-test-on-server.sh --both`, збірка з усіма змінами цієї роботи): **SQLite 333/333, PostgreSQL 333/333** (0 впалих). Усього 333 тести (7 нових у `PostgresReadinessTests`).
У проміжних прогонах до виправлень були реальні падіння, всі усунуто: (а) після зміни типового провайдера на Postgres фабрика ЕСОЗ без явного провайдера впала — додано `appsettings.Testing.json`; (б) `PostgresDdlTests` на сервері не знаходив `MedLink.LIS.sln` — скрипт тепер включає `MedLink.LIS.sln` і `Directory.Build.props` в архів; (в) `Npgsql ... Kind=UTC to timestamp without time zone` — перемикач legacy-дат вмикається до першого використання Npgsql (у `Program.cs` і `ModuleInitializer` тестів); (г) `ILIKE … ESCAPE ''` (EF без явного escape вимикає екранування) — використано перевантаження з `ESCAPE '\'`, додано тест на літеральні `_` і `%`.
Реальних PG-специфічних падінь бізнес-логіки (типи, регістр, транзакції) у 333 тестах не виявлено; пошук/сортування кирилиці та нумератори перевірено новими тестами.

**Лишилось / ризики:**
1. **Пошук замовлень і пацієнтів з `OR` по кількох таблицях (≈1 с / 158 мс на 100 000 / 30 000)**: переписати через об'єднання id (розд. 6, 584 мс → 4 мс) у `OrderService`, `PatientService`, `WorkReportsService`, `EhealthSignService` (бізнес-логіка інших агентів, не чіпалась); або додати нормалізовану колонку `search_text` з trigram-індексом.
2. Створення індексів із `0002` на великій таблиці блокує запис (1–2 с на 100 000 рядків); для >1 млн рядків — `CONCURRENTLY` окремим скриптом.
3. Резервні копії не винесено за межі хоста; немає моніторингу `last_ok`.
4. Тести ЕСОЗ (`EhealthFactory`) — тільки SQLite.
5. Два екземпляри API на порожній БД одночасно — не підтримуються (`EnsureCreated`).
6. Колонки `varchar(n)` суворіші, ніж SQLite: нові поля вводу без перевірки довжини дадуть 400 «Занадто довге значення» (раніше мовчки зберігалось).
7. Стенд ще працює на попередній збірці: індекси `0002`, ILIKE/колація й нові нумератори діють після деплою нової збірки (деплой не виконувався).
