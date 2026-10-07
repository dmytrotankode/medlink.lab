# MedLink LIS 4.0 — Лабораторна інформаційна система MedLink

> ТОВ «МедЛінк» (MedLink LLC) © 2026. Модуль «Лабораторія» для веб-МІС MedLink (evomis) та автономний ЛІС-продукт.

Технологічний стек повторює MedLink: **.NET 8 Web API + EF Core**, **Quasar v1.15.3 + Vue 2**, база **SQLite** (схема у стилі evomis, готова до перенесення на PostgreSQL), крос-платформний **коннектор аналізаторів** (.NET 8 Worker Service: ASTM E1394 / HL7 v2 MLLP / текстові протоколи 65 моделей приладів). Авторизація користувачів у модулі відсутня — її надає evomis.

## Склад репозиторію

| Шлях | Що це |
|---|---|
| `src/MedLink.LIS.Core` | Спільна бібліотека: клінічні правила (каскад норм Simplex, прапорці, delta-check, автоверифікація, reflex, Вестгард, EUCAST, TAT), штрихкоди (Simplex EAN-8, Code128, ZPL, QR), протоколи аналізаторів (ASTM, HL7, текстові), побудовники замовлень для приладів, каталог 64 типів аналізаторів, DTO протоколу коннектора |
| `src/MedLink.LIS.Api` | REST API ЛІС (ASP.NET Core 8, EF Core SQLite, Swagger), віддає зібраний фронтенд із `wwwroot` |
| `src/MedLink.LIS.Web` | Фронтенд Quasar CLI v1 + Vue 2 у структурі `evomis/src/App.View` |
| `src/MedLink.LabConnector` | Коннектор аналізаторів (служба Windows / systemd), CLI `setup`, локальна сторінка статусу `http://localhost:5088` |
| `installers/labconnector` | Inno Setup, PowerShell, Linux systemd/deb, Docker, скрипти публікації |
| `tests/MedLink.LIS.Tests` | xUnit: клінічні правила, протоколи, API |
| `tests/protocol_samples` | Зразки повідомлень ASTM/HL7/FHIR |
| `db/` | DDL PostgreSQL для перенесення, довідники, зразкова БД прототипу |
| `docs/` | **ТЗ v4**, реєстр задач, контракт API; `docs/analysis` — архів етапу аналізу (ТЗ v2 замовника, майстер-спека v3, прототипи, скріншоти) |
| `legacy/` | Delphi-коннектор AConnectAstm та дамп MySQL Simplex (норми, типи аналізаторів, процедури) |
| `tools/legacy_scripts` | Python-скрипти етапу аналізу (генерація прототипів/документів) |

## Швидкий старт

```bash
# 1. Бекенд (створює App_Data/medlink_lis.db і сідить довідники при першому запуску)
dotnet run --project src/MedLink.LIS.Api --urls http://0.0.0.0:5055
#    Swagger: http://localhost:5055/swagger   Health: http://localhost:5055/health

# 2. Фронтенд (dev-режим із проксі на :5055)
cd src/MedLink.LIS.Web && npm install && npm run dev
#    або зібрати в wwwroot API: ./build.sh (Windows: .\build.ps1)

# 3. Тести
dotnet test MedLink.LIS.sln

# 4. Коннектор аналізаторів (на ПК біля приладу)
#    У веб-інтерфейсі: Інтеграції → Аналізатори та коннектори → «Додати коннектор» → ключ інсталяції
dotnet run --project src/MedLink.LabConnector -- setup --server http://lis-server:5055 --install-key <КЛЮЧ>
dotnet run --project src/MedLink.LabConnector -- run
```

Детальні інструкції: `src/MedLink.LIS.Api/README.md`, `src/MedLink.LIS.Web/README.md`, `installers/labconnector/README_UA.md`.

## Документація

- `docs/ТЗ_ЛІС_MedLink_v4.md` — детальне технічне завдання v4 (вимоги, модель даних, API, коннектор, інсталяція, приймання).
- `docs/РЕЄСТР_ЗАДАЧ.md` — задачі, поставлені власником продукту, та їхній статус.
- `docs/API_CONTRACT.md` — контракт REST API, доменної моделі та протоколу коннектора.
