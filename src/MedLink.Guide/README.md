# MedLink.Guide — бібліотека інтерактивних покрокових інструкцій

Окрема .NET-бібліотека (net8.0) + схема PostgreSQL `guide` (таблиці `gd_*`, DDL: `db/postgres/guide/001_guide_schema.sql`). Не залежить від `LisDbContext` і від конкретної СУБД: провайдер EF передає хост. API: `/api/v1/guide/*`.

## Підключення до хоста (Program.cs)

```csharp
using MedLink.Guide;

// SQLite (пілот ЛІС)
builder.Services.AddGuide(builder.Configuration, o =>
{
    o.ConfigureDb = db => db.UseSqlite($"Data Source={guideDbPath}");   // окремий файл або той самий
    o.AllowAnonymousEditing = true;                                      // ЛІС без автентифікації
});

// PostgreSQL (evomis / ЛІС на Postgres) — той самий connection string, схема "guide"
builder.Services.AddGuide(builder.Configuration, o =>
{
    o.ConfigureDb = db => db.UseNpgsql(connectionString);
    o.EditorRoles = new() { "admin", "guide_editor" };
    o.DefaultTenantId = http => /* organization_id з claims або Lab:OrganizationId */ null;
});

var app = builder.Build();
await app.Services.EnsureGuideDatabaseAsync();   // створює таблиці, якщо їх немає
await app.Services.SeedGuideBundlesAsync();      // (необов'язково) вбудовані Seed/*.json, ідемпотентно
app.MapGuide();                                  // /api/v1/guide/*
```

Пакет `Npgsql.EntityFrameworkCore.PostgreSQL` / `Microsoft.EntityFrameworkCore.Sqlite` підключає **хост**, бібліотека їх не містить. Для PostgreSQL `EnsureGuideDatabaseAsync` виконує вбудований `001_guide_schema.sql` (jsonb, uuid); у production замість цього можна застосувати файл міграцією вручну.

## Заголовки запиту

| Заголовок | Призначення |
|---|---|
| `X-MedLink-Employee-Id` | uuid співробітника (аудит-колонки, прогрес). Не GUID → нульовий GUID |
| `X-MedLink-Roles` | ролі через кому (фільтр каталогу за `required_roles`, право редактора) |
| `X-MedLink-Tenant-Id` | `organization_id` (видимість `tenant_id`: NULL або збігається) |

Інше визначення користувача — власна реалізація `IGuideUserResolver` (реєструється до `AddGuide`).

## Конфігурація `Guide:*` (appsettings.json)

```json
"Guide": {
  "RoutePrefix": "/api/v1/guide",
  "Schema": "guide",
  "AllowAnonymousEditing": false,
  "EditorRoles": [ "admin", "guide_editor" ],
  "MaxMediaBytes": 10485760,
  "EmployeeHeader": "X-MedLink-Employee-Id",
  "RolesHeader": "X-MedLink-Roles",
  "TenantHeader": "X-MedLink-Tenant-Id"
}
```

Делегати (`ConfigureDb`, `IsEditor`, `DefaultTenantId`, `ResolveRoles`) задаються лише в коді.

## API (скорочено)

Плеєр (відкрито): `GET apps/{app}/catalog?role=&route=`, `GET scenarios/{id}/play`, `GET media/{id}`, `POST progress`, `GET progress/me`, `POST anchor-reports`.
Конструктор (редактор): `POST apps`, `GET apps/{app}/tree`, папки (`POST/PUT/DELETE folders`, `POST folders/{id}/move`), сценарії (`POST/GET/PUT/DELETE scenarios`, `duplicate`, `PUT …/steps`, `PUT …/variables`, `publish`, `unpublish`, `export`), `POST apps/{app}/import`, набори (`GET apps/{app}/sets`, `POST/PUT/DELETE sets`, `PUT sets/{id}/items`), якорі (`GET apps/{app}/anchors`, `PUT anchors/{id}`, `GET apps/{app}/anchors/health`, `POST anchor-reports/{id}/accept`), `POST media`.

Особливості:
- Редагується **чернетка** (`gd_step`); `publish` робить знімок у `gd_scenario_version`, плеєр читає лише знімок.
- `play` підставляє **поточні** дані якорів із `gd_anchor`, тож прийняте виправлення якоря (`accept`) діє без перепублікації.
- Видалення — soft (`record_state = 4`); непорожній розділ видаляється лише з `?cascade=true`.
- Імпорт ідемпотентний за `code` (розділів, інструкцій, якорів, наборів); публікація при імпорті — лише якщо вміст змінився.

## Набори інструкцій (презентації)

Набір (`gd_set`: `code`, `caption`, `description`, `color` `#rrggbb`, `icon`, `sort_order`, `app_id`, `tenant_id`, soft-delete) — впорядкований список елементів `gd_set_item`: **або** інструкція (`scenario_id`), **або** розділ (`folder_id`). Розділ у наборі розкривається в усі свої опубліковані інструкції разом з підрозділами. Теги інструкцій набір не формують — лише явні елементи.

- Каталог `GET apps/{app}/catalog` повертає `sets: [{id, code, caption, description, color, icon, sortOrder, estMinutes, scenarioCount, scenarios:[{id, code, caption, estMinutes, folder}]}]` (лише видимі користувачу: опубліковані, за роллю та tenant; набір без таких інструкцій приховано). Кожна папка й інструкція каталогу має `setCodes` та `sets:[{code, caption, color}]` — за ними панель підсвічує їх кольором набору (розділ у наборі позначає всі підрозділи й інструкції).
- Редактор: `GET apps/{app}/sets`, `POST sets` (`{appCode, caption, code?, color?, description?, icon?, sortOrder?, items?:[{scenarioId}|{folderId}]}`), `PUT sets/{id}`, `DELETE sets/{id}` (soft), `PUT sets/{id}/items` (повна заміна складу; порядок масиву = порядок показу).
- Бандл імпорту — необов'язкова секція `sets`, ідемпотентна за `code`; елементи задаються кодами, невідомі коди пропускаються:
  ```json
  "sets": [{ "code": "start", "caption": "Перший день", "description": "…", "color": "#2e7d32", "icon": "🎞", "sortOrder": 1,
             "items": [ { "folderCode": "lab.orders" }, { "scenarioCode": "lab.qc.basic" } ] }]
  ```
  Повторний імпорт нічого не дублює: набір оновлюється за `code`, склад переписується лише якщо змінився.
- SQLite: `EnsureGuideDatabaseAsync` дооснащує вже наявну БД таблицями `gd_set`/`gd_set_item` (`CREATE … IF NOT EXISTS`); PostgreSQL — `001_guide_schema.sql` (ідемпотентний, вшитий у збірку).

## Тести

`dotnet test tests/MedLink.Guide.Tests` (xUnit, TestServer + SQLite in-memory).
