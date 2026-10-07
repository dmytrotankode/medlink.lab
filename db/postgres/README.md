# Схема ЛІС для PostgreSQL (MedLink / evomis)

| Файл | Що це |
|---|---|
| `medlink_lis_schema.sql` | **Чинна схема.** Генерується з EF-моделі ЛІС командою `dotnet run --project src/MedLink.LIS.Api -- --export-pg-ddl <шлях>`. Створює таблиці [ЛІС] (`lab_*`) і зовнішні ключі на таблиці evomis. Таблиці [MedLink] (`cmn_*`, `org_*`, `mis_*`, `ehe_*`) не створює: вони вже існують в evomis, тому в скрипті лише перелічено колонки, які використовує ЛІС. Колонки [MedLink+] додаються через `ALTER TABLE … ADD COLUMN IF NOT EXISTS`. |
| `../migrations/R1.1/*.sql` | Інкрементні міграції після v4.0 (кожна задача R1.1 — окремий скрипт). |
| `legacy_v3/` | Архів DDL етапу v3. Не відповідає поточній моделі, використовувати не можна. |

Типи відповідають evomis:
- ключі та посилання — `uuid`;
- дати — `timestamp without time zone`;
- службові колонки — `created_by/modified_by uuid`, `record_state integer` (2 — активний, 4 — видалений).

Порядок розгортання в evomis:
1. `medlink_lis_schema.sql`;
2. сід довідників ЛІС (біоматеріали, тара, показники, норми) — експорт із SQLite ЛІС;
3. зіставлення переліків `cmn_enum_record` за `enum_type + code` (див. ТЗ, розд. 20.2.4).
