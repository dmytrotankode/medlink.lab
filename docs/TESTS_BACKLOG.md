# Беклог тестів (Додаток E, що не реалізовано автотестами)

Статус на 2026-11-09, гілка `cl/tests`. Реалізовані тести позначені `[Trait("TZ","…")]` (AppendixEApiTests), юніт-частина — `Clinical/AppendixEUnitTests.cs`.
Усі нові API-тести працюють лише через HTTP API (без SQLite-специфічного SQL), тож мають проходити й на PostgreSQL (`LIS_TEST_PG`); на PG в цій сесії не запускалися.

## Не реалізовано і чому

| ID / група | Причина | Що потрібно |
|---|---|---|
| E-S* (Playwright E2E) | Потрібні браузер і працюючий стенд | Окремий CI-job із запущеним API+UI |
| I-WS-perf-300ms, perf-worklist-500, Lighthouse, NFR-01..03 | Залежать від середовища/заліза, нестабільні в CI | Виділений перф-стенд |
| CI-matrix-postgres | Потрібен сервер PG у CI | Сервіс PG у пайплайні, `LIS_TEST_PG` |
| C-setup-register, C-manual-install | Потрібні ОС, служба Windows/systemd, CLI конектора | Тест на VM |
| Реальні зовнішні системи (ЄСОЗ, МІС-обмін, SMS/e-mail) | Немає зовнішніх систем | Контрактні тести/стаби |
| I-REG-release-notification-stub | Сповіщення переробляє інший агент | Додати після злиття notify |
| I-VAL-panic-pending, I-REG-release-409-panic | Паніка/блок видачі — зона агента calib/panic | Додати після злиття |
| I-REA-consume-alert, rerun, I-WS-reflex-created, обчислювані значення | Реагенти, повтор, обчислення — переробляються іншими агентами | Додати після злиття |
| Паралельні release/verify (рівно один 200) | `OrderService.ReleaseAsync` без атомарного захоплення; зараз тест допускає 200/409 (TODO у SecurityAndRaceApiTests) | Після переробки Release вимагати {200,409} і один MisDiagnosticReport |
| Токен видачі при паралельних release | Друга видача перезаписує VerifyToken, перший токен дає 404 на /verify | Перевірити після переробки Release |

## Вже покрито юніт-тестами раніше
U-AutoVerify, DeltaCheck, ReflexEngine, Westgard, Cascade норм, Eucast, Phenotypes, TatCalculator, парсери приладів.

## Спостереження (не виправлялося)
- `AutoVerifyAsync` ігнорує PerformerId і перевірку правдоподібності значення.
- `Lab:VerifySecret` має дефолт `medlink-lis`: у проді обов'язково задати власний секрет.
- `VerifyBatchAsync` перетворює Forbidden на «skipped» (200 із verified=0).
