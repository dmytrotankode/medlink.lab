# План підключення автентифікації (OIDC MedLink)

Статус: **режим `Auth:Mode=Oidc` реалізовано** (JWT Bearer від Identity MedLink, співробітник і роль з МІС, SPA на oidc-client, код + PKCE) — деталі й налаштування в `docs/MEDLINK_INTEGRATION_PLAN.md` розділ 7. Перевірено на підписаному тестовому токені; вхід через справжній Identity не перевірявся (потрібен клієнт і redirect URI). Нижче — початковий план і вимоги замовника.

## 1. Що є зараз
- `CurrentEmployeeMiddleware` (`src/MedLink.LIS.Api/Infrastructure/CurrentEmployee.cs`) визначає співробітника:
  - `Auth:Mode = None` (типово): заголовок `X-MedLink-Employee-Id` **ігнорується**, діє `Lab:DefaultEmployeeId` (у демо — адміністратор);
  - `Auth:Mode = Header`: співробітник береться із заголовка, якщо його немає — `Lab:DefaultEmployeeId`. Безпечно лише за довіреним зворотним проксі МІС, який сам виставляє заголовок і вирізає чужий.
- Ролі перевіряє `IRolePolicy` / `LisStateMachine` за `ICurrentEmployee.LabRole`; сам `ICurrentEmployee` заповнює лише middleware.
- `appsettings.Development.json` вмикає `Header` (робота ролей у стенді розробника); у тестах `Auth:Mode=Header` задано у фабриках.
- Поза Development `/health` повертає `configWarnings` з кодом `AUTH_NONE` або `AUTH_HEADER`, у журналі при старті — попередження `!!!`.

## 2. Що потрібно від замовника
1. **Issuer** (authority) OIDC MedLink і адреса `/.well-known/openid-configuration`.
2. **Client id** (і client secret, якщо клієнт конфіденційний; секрет — лише у змінних середовища `Auth__Oidc__ClientSecret`).
3. **Redirect URI**, зареєстровані в провайдері (напр. `https://<публічна адреса ЛІС>/signin-oidc`, `/signout-callback-oidc`) та `post_logout_redirect_uri`.
4. **Джерело ролей**: який claim несе ролі (`roles`, `groups`, власний) і таблиця відповідності значень до ролей ЛІС (`LAB_ADMIN`, `LAB_DOCTOR`, `LAB_TECHNICIAN`, `PHLEBOTOMIST`, `REGISTRAR`, `LOGISTICS_COURIER`), або підтвердження, що ролі беруться з `org_employee.LabRole` за ідентифікатором співробітника.
5. **Claim зі стабільним ідентифікатором співробітника** (`sub` або `employee_id`) і правило його зіставлення з `org_employee.Id`.
6. Для автономного розгортання (без МІС) — рішення, чи потрібен локальний вхід (логін/пароль) чи лише OIDC.
7. Політики сесії: час життя, вихід, обмеження за IP/мережею клініки, вимога 2FA на боці провайдера.

## 3. Де в коді точка заміни
- **`CurrentEmployeeMiddleware.InvokeAsync`** — єдине місце, де визначається `requested` (ідентифікатор співробітника). Для OIDC: додати `Auth:Mode = Oidc`; замість заголовка брати `context.User.FindFirst(<claim>)`; решта (завантаження `OrgEmployee`, `current.Set`, `db.CurrentUserId`) лишається без змін.
- `Program.cs`: перед `UseMiddleware<CurrentEmployeeMiddleware>()` додати `AddAuthentication().AddJwtBearer(...)` (для API, викликів SPA з токеном) або `AddOpenIdConnect(...)` + cookie (якщо SPA обслуговує сам ЛІС), `UseAuthentication()` та `UseAuthorization()`.
- `AuthModes.Resolve` (`Infrastructure/StartupConfigCheck.cs`) — додати значення `Oidc`; невідомі значення й далі падають у безпечний `None`.
- Фронтенд: `src/services/http.js` — замість заголовка `X-MedLink-Employee-Id` додавати `Authorization: Bearer`, обробка 401 (перехід на вхід); `store/context` — поточна роль із `/api/v1/lab/...` (контекст співробітника), а не з перемикача ролей.
- Публічні маршрути без входу лишаються: `/health`, `/verify/{token}`, `/api/v1/lab/portal/*` (портал пацієнта має власну схему доступу, див. п. 5), Swagger — лише у не-виробничих середовищах.
- Конектор аналізаторів (`ConnectorApiKeyFilter`, `/api/v1/lab/connector/*`) — окремий ключ API, OIDC його не стосується.

## 4. План міграції
1. Узгодити дані з розділу 2; завести клієнта OIDC в тестовому контурі МІС.
2. Додати режим `Oidc` за прапорцем, `Auth:Mode = Header` лишити для відкату. Тести: токен-заглушка (власний `AuthenticationHandler` у `WebApplicationFactory`), 401 без токена, 403 для чужої ролі, зіставлення співробітника.
3. Перевірити на тестовому контурі: вхід, вихід, закінчення сесії, ролі кожного типу, аудит (`CreatedBy` = ідентифікатор співробітника).
4. Увімкнути на проміжному середовищі при `Auth:Mode = Oidc`; `Lab:DefaultEmployeeId` прибрати (порожнє значення → 401 замість «адміністратора за замовчуванням»).
5. Промислове увімкнення; за потреби відкат на `Header` лише за проксі.

## 5. Що ламається з увімкненням
- **Усі клієнти, що викликають API без токена**: SPA (потрібен вхід), скрипти, Swagger UI «Try it out», автотести (потрібні токени або тестова схема).
- **Портал пацієнта** (`/portal/*`, `GET /portal/{patientId}/...`): зараз без входу. Потрібна окрема схема (одноразове посилання/код із сповіщення, вхід через Дія/BankID/телефон), інакше його або залишають анонімним (небезпечно: ідентифікатор пацієнта в URL), або закривають співробітницьким входом.
- **Заголовок `X-MedLink-Employee-Id`**: перестає визначати користувача; інтеграції МІС, що його виставляють, мають передавати токен.
- **Guide** (`AllowAnonymousEditing = true`): редагування інструкцій треба прив'язати до ролей (`Guide:EditorRoles`).
- **Фонові задачі** (воркери сповіщень, МІС, ЕСОЗ) працюють без користувача — не зачіпаються; їх аудит іде від системного GUID.
- **Рольова політика в UI** (`roleMixin`) бере роль із контексту співробітника — після переходу на токен контекст повертається з сервера.
- Допустимий компроміс до завершення: `Auth:Mode = Header` лише за проксі, що перезаписує заголовок; прямий доступ до порту ЛІС закритий мережево.

## Дані тестового MedLink (зняті зі сторінки входу, 09.11.2026)

- Сервер ідентифікації: `https://id-test.medlink.healthcare` (OIDC, код авторизації + PKCE S256).
- Клієнт МІС: `MisFront`; scope: `openid MisFrontScope IdentityApiScope MisApiScope offline_access`; повернення: `https://mis-test.medlink.healthcare/link/#/callback#`.
- Після входу користувач обирає профіль (посаду в організації, напр. «Адміністратор», «Спеціаліст …»); профіль підтверджує `POST /link/api/api/Account/authenticate`, далі `GET …/Account/info`.
- У `ehealthInfo` МІС є поля: `organizationId`, `ehealthEmployeeId`, `roleName`, `typeCode`, `specialityCode`, `ehealthPositionCode`, `ehealthDivisionId`, `employeeStatus`, `currentUserId`, `canUseTerraLab`, `terraLabId`. У токені профіль містить лише `sid, sub, auth_time, idp, amr` — ролі й організація беруться з `Account/info`, а не з токена.
- Для модуля «Лабораторія» потрібні: окремий клієнт OIDC (або дозвіл використати `MisFront`), redirect URI під `/lab/`, джерело ролей (вирішити: `Account/info` через проксі МІС чи claims в окремому scope), відповідність `roleName`/`typeCode` ролям лабораторії.
