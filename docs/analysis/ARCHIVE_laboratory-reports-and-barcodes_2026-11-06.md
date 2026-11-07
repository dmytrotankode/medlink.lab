# Архив `laboratory-reports-and-barcodes.zip`: что внутри (06.11.2026)

Файл: `C:\__MEDLINK___\LABA\laboratory-reports-and-barcodes.zip` (7,8 МБ, 683 записи, распаковано 15,5 МБ).
Распакованная копия только для чтения: `C:\Users\tanko\AppData\Local\Temp\claude\b80e777d68234b88\e11d1376-ec50-475b-bda7-9fe872bcc04f\scratchpad\laba-zip\laboratory-reports-and-barcodes\` (временная папка другого чата; надёжнее распаковать архив заново).
Проверка: только чтение кода. Ничего не собиралось и не запускалось, тесты не прогонялись.

## Вердикт
Это не фрагмент и не заглушки, а рабочий исходный код ЛИС MedLink LIS 4.0 — **другая линия того же проекта** (по всем признакам ветка `tenancy` из GitLab MedLink). С нашим репозиторием не совпадает: часть есть только в архиве, часть только у нас. Название «reports-and-barcodes» не отражает состав: в корне весь ЛИС (`MedLink.LIS.sln`, `CLAUDE.md`, `README`). Папки `.git` нет, все файлы датированы 05.11.2026 (дата упаковки).

## Структура
- `src/MedLink.LIS.Api` — .NET 8, EF Core на PostgreSQL: 115 .cs, ≈ 85 тыс. строк, 5 миграций EF (последняя 0005 от 03.11).
- `src/MedLink.LIS.Core` — 101 .cs, ≈ 9,7 тыс. строк: клинические правила, штрихкоды, протоколы.
- `src/MedLink.LabConnector` — 29 .cs, ≈ 4,1 тыс. строк.
- `src/MedLink.LIS.Web` — Quasar 1.15 + Vue 2, 87 .vue.
- `tests/MedLink.LIS.Tests` — 37 .cs, ≈ 6,7 тыс. строк, ≈ 275 методов Fact/Theory, `protocol_samples` (5 HL7).
- Прочее: `db` (схема PG + 18 seed-json), `lis-db` (SQL ≈ 830 КБ), docker / nginx / supervisor, `.gitlab-ci.yml`, `mis-module-docs` (ТЗ v4 в md/html, `API_CONTRACT`, `swagger.json`, 38 скриншотов; имена части файлов повреждены кодировкой).

## Что есть по коду
- Штрихкоды: EAN-8, Code128, Code39, EAN13, QR (SVG), ZPL, планировщик пробирок; `LabelPdfService` (QuestPDF, этикетки 30×20, 50×25, лист A4); эндпоинты `orders/{id}/barcodes.pdf`, `labels`, `labels.pdf`.
- Отчёты: `orders/{id}/report` и `report.pdf`, отчёт микробиологии, `portal/.../report.pdf`, печать счёта.
- Остальное: заказы, пробы, рабочие списки, QC, микробиология, биобанк, send-out, биллинг, аналитика, логистика, реагенты.
- Коннектор приборов: ASTM, HL7, 65 моделей.
- Вход через OIDC + Redis, мультитенантность (`Tenancy/`, `TenantSessionInterceptor`).
- Заглушек нет: три комментария TODO в перенесённых файлах Core.

## Соответствие ТЗ
- В архиве: `mis-module-docs/ТЗ_ЛІС_MedLink_v4.md`, `API_CONTRACT.md`, `README.md`. Реестра задач (`РЕЄСТР_ЗАДАЧ`) нет — он есть у нас. Документа `old-docs/LIS_план_старта_для_разработчика_1.docx` в архиве нет.
- Покрытие оценено по структуре кода и README, не по каждому пункту FR-*. Основные модули ТЗ покрыты.
- Нет модулей R1.1: патоморфология (ТЗ 22), интеграция с МИС, маршрутизация и распределение, подпись / отправка в ЕСОЗ, «Робочі звіти» (ТЗ 21/23).

## Связь с нашим репозиторием
- Тот же проект: те же namespace `MedLink.LIS.*`, тот же sln, тот же `CLAUDE.md`.
- Только у нас (ветка `claude/lab-module-analysis-implementation-fp9ebd`, последний коммит 02.11) — 16 файлов: `PathologyController`, `MisIntegrationController`, `EhealthSignController`, `WorkReportsController`, `RoutingService`, `MisCatalogService`, `SchemaUpgrader` и др.
- Только в архиве — ≈ 45 файлов: слой из МИС (`AccountController`, Redis-аутентификация, `Tenancy/*`, 5 миграций EF, `PermissionCatalog`, `Code39Svg`, `Ean13Svg`, `LabelPdfService`, `UserFullProfileDto`).
- Общие файлы: 146 идентичны, 103 различаются.
- Совпадает с описанием ветки `tenancy` в `docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md`. Объединять — через diff с веткой `tenancy`, не копированием архива поверх.

## Риски
- Возможные секреты (значения не смотрели, могут быть просто имена полей): `dev/identity-server/appsettings.json`, `docker-compose.yml`, `lis-db/00_roles.sql`, `lis-db/01_schema_data.sql`, `src/MedLink.LIS.Api/appsettings*.json`, `src/MedLink.LabConnector/appsettings.json`, `Cli/Commands.cs`, `Configuration/ConfigStore.cs`, `ConnectorOptions.cs`, `Server/LisApiClient.cs`, `mis-module-docs/swagger.json`.
- Бинарников .dll / .exe нет; 38 png/jpg.
- Сторонние названия в коде и комментариях (Simplex — норма и штрихкод EAN-8, UniCam / Delphi в legacy-комментариях, evomis, identity-server MedLink).
- ТЗ v4 в архиве — та же версия, что `docs/ТЗ_ЛІС_MedLink_v4.md` у нас; опираться только на неё без реестра задач нельзя.

## Состояние нашего репозитория на 06.11 (для того, кто продолжит)
В `C:\__MEDLINK___\LABA` идёт незавершённое слияние: конфликты в `docs/РЕЄСТР_ЗАДАЧ.md` и `docs/ТЗ_ЛІС_MedLink_v4.md`, добавлен `docs/РЕВІЗІЯ_ГІЛКИ_TENANCY.md`, изменён `wwwroot/index.html`. Этот файл в git не добавлен.
