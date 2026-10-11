# MedLink LIS 4.0 — інструкції для Claude Code

Лабораторна інформаційна система MedLink (модуль evomis). Стек: .NET 8 Web API + EF Core SQLite (`src/MedLink.LIS.Api`), спільна бібліотека (`src/MedLink.LIS.Core`), коннектор аналізаторів (`src/MedLink.LabConnector`), фронтенд Quasar v1.15.3 + Vue 2 (`src/MedLink.LIS.Web`). Без автентифікації користувачів (її надає evomis).

## Джерела істини
- `docs/API_CONTRACT.md` — контракт API, доменна модель, протокол коннектора. Зміни контракту — тільки тут.
- `docs/ТЗ_ЛІС_MedLink_v4.md` — ТЗ; бек-лог наступних робіт у розділі 19.9 та главах 18–19.
- `docs/РЕЄСТР_ЗАДАЧ.md` — задачі власника продукту та статуси.

## Команди
- Збірка: `dotnet build MedLink.LIS.sln`; тести: `dotnet test MedLink.LIS.sln` (мають проходити всі).
- Фронтенд: `cd src/MedLink.LIS.Web && npm ci && npm run build`; у wwwroot API: `./build.sh` (Windows `.\build.ps1`).
- Запуск: `./run.sh` або `.\run.cmd` → http://localhost:5055 (SPA), `/swagger`, `/health`.
- Коннектор: `dotnet run --project src/MedLink.LabConnector -- list-profiles | test | preview-order | setup`.

## Правила
- Мова коду англійська, UI/коментарі/повідомлення українською.
- Таблиці та колонки snake_case, GUID-рядки, аудит-колонки як в evomis; тільки типи, сумісні з PostgreSQL.
- Кожна нова можливість: тест у `tests/MedLink.LIS.Tests`, оновлення контракту і ТЗ (розділ з вимогами + скріншот у `docs/screenshots`).
- Не комітити `bin/ obj/ node_modules/ dist/ wwwroot/* App_Data/`.
