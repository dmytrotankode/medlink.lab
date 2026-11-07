-- =============================================================================
-- MedLink Guide — модуль інтерактивних покрокових інструкцій (плагін до будь-якої системи).
-- Окрема схема "guide" у тій самій БД PostgreSQL, що й ЛІС/evomis. Таблиці gd_*.
-- Конвенції ЛІС/evomis: snake_case, uuid, аудит-колонки created_by/created_on/modified_by/modified_on,
-- record_state (2 — активний, 4 — видалений), tenant_id = org_organization.id (NULL = спільне для всіх).
-- Ідемпотентний (CREATE ... IF NOT EXISTS). Відповідає EF-моделі src/MedLink.Guide (GuideDbContext).
-- =============================================================================
BEGIN;

CREATE SCHEMA IF NOT EXISTS guide;
SET LOCAL search_path = guide, public;

-- ---------------------------------------------------------------------------
-- Система-хост (ЛІС, evomis, ...): один модуль обслуговує кілька систем
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS gd_app (
    id uuid NOT NULL,
    code varchar(64) NOT NULL,            -- 'lab', 'evomis', ...
    caption varchar(256) NOT NULL,
    base_url varchar(512),
    default_locale varchar(8) NOT NULL DEFAULT 'uk',
    created_by uuid NOT NULL, created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL, modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_gd_app PRIMARY KEY (id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_gd_app_code ON gd_app (code);

-- ---------------------------------------------------------------------------
-- Дерево розділів (папок) інструкцій
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS gd_folder (
    id uuid NOT NULL,
    app_id uuid NOT NULL,
    parent_id uuid,
    tenant_id uuid,
    code varchar(64),
    caption varchar(256) NOT NULL,
    description text,
    icon varchar(64),
    sort_order integer NOT NULL DEFAULT 0,
    created_by uuid NOT NULL, created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL, modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_gd_folder PRIMARY KEY (id),
    CONSTRAINT fk_gd_folder_app FOREIGN KEY (app_id) REFERENCES gd_app (id),
    CONSTRAINT fk_gd_folder_parent FOREIGN KEY (parent_id) REFERENCES gd_folder (id)
);
CREATE INDEX IF NOT EXISTS ix_gd_folder_app_parent ON gd_folder (app_id, parent_id, sort_order);

-- ---------------------------------------------------------------------------
-- Якорі: переюзні «закладки» на елементи UI з багатьма стратегіями пошуку (щоб не губились)
-- strategies  : jsonb [{type,value,weight}] — data-testid / id / aria / label / text / css / path
-- fingerprint : jsonb {tag, text, classes, attrs, role, label, ancestors[], rectRatio} — для самовідновлення
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS gd_anchor (
    id uuid NOT NULL,
    app_id uuid NOT NULL,
    tenant_id uuid,
    code varchar(128),                    -- стабільний ключ (необов'язково)
    caption varchar(256) NOT NULL,
    route_pattern varchar(512),           -- де живе елемент (напр. /laboratory/orders/:id)
    strategies jsonb NOT NULL DEFAULT '[]'::jsonb,
    fingerprint jsonb NOT NULL DEFAULT '{}'::jsonb,
    parent_levels integer NOT NULL DEFAULT 0,   -- підсвічувати предка на N рівнів вище
    screenshot_id uuid,
    health_status varchar(16) NOT NULL DEFAULT 'unknown',  -- unknown | ok | healed | broken
    health_checked_on timestamp without time zone,
    last_found_how varchar(32),
    created_by uuid NOT NULL, created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL, modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_gd_anchor PRIMARY KEY (id),
    CONSTRAINT fk_gd_anchor_app FOREIGN KEY (app_id) REFERENCES gd_app (id)
);
CREATE INDEX IF NOT EXISTS ix_gd_anchor_app ON gd_anchor (app_id, record_state);
CREATE UNIQUE INDEX IF NOT EXISTS ux_gd_anchor_code ON gd_anchor (app_id, code) WHERE code IS NOT NULL AND record_state = 2;

-- ---------------------------------------------------------------------------
-- Сценарій (інструкція). Редагується чернетка (gd_step); публікація = знімок у gd_scenario_version
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS gd_scenario (
    id uuid NOT NULL,
    app_id uuid NOT NULL,
    folder_id uuid,
    tenant_id uuid,
    code varchar(128),
    caption varchar(256) NOT NULL,
    description text,
    icon varchar(64),
    status varchar(16) NOT NULL DEFAULT 'draft',    -- draft | published | archived
    start_route varchar(512),                       -- куди перейти перед початком (будь-де в системі)
    required_roles jsonb NOT NULL DEFAULT '[]'::jsonb,  -- порожньо = для всіх
    tags jsonb NOT NULL DEFAULT '[]'::jsonb,
    est_minutes integer,
    locale varchar(8) NOT NULL DEFAULT 'uk',
    sort_order integer NOT NULL DEFAULT 0,
    draft_version integer NOT NULL DEFAULT 1,
    published_version integer,
    published_on timestamp without time zone,
    published_by uuid,
    created_by uuid NOT NULL, created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL, modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_gd_scenario PRIMARY KEY (id),
    CONSTRAINT fk_gd_scenario_app FOREIGN KEY (app_id) REFERENCES gd_app (id),
    CONSTRAINT fk_gd_scenario_folder FOREIGN KEY (folder_id) REFERENCES gd_folder (id)
);
CREATE INDEX IF NOT EXISTS ix_gd_scenario_app_folder ON gd_scenario (app_id, folder_id, sort_order);
CREATE INDEX IF NOT EXISTS ix_gd_scenario_status ON gd_scenario (app_id, status, record_state);

-- Змінні/тестові дані сценарію: значення підставляються в кроки як {{ім'я}}
CREATE TABLE IF NOT EXISTS gd_scenario_variable (
    id uuid NOT NULL,
    scenario_id uuid NOT NULL,
    name varchar(64) NOT NULL,
    caption varchar(256),
    kind varchar(16) NOT NULL DEFAULT 'fixed',      -- fixed | random_number | random_text | today | now | pick
    default_value text,
    options jsonb NOT NULL DEFAULT '{}'::jsonb,      -- напр. {"prefix":"TEST-","length":6} або {"values":["A","B"]}
    sort_order integer NOT NULL DEFAULT 0,
    created_by uuid NOT NULL, created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL, modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_gd_scenario_variable PRIMARY KEY (id),
    CONSTRAINT fk_gd_variable_scenario FOREIGN KEY (scenario_id) REFERENCES gd_scenario (id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_gd_variable_name ON gd_scenario_variable (scenario_id, name) WHERE record_state = 2;

-- ---------------------------------------------------------------------------
-- Крок (чернетка)
-- step_type : info | click | input | select | check | navigate | wait | hover
-- expect    : next | click | input | change | route | appear | disappear  (що вважати виконаним кроком)
-- highlight : element | parent | rect | none
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS gd_step (
    id uuid NOT NULL,
    scenario_id uuid NOT NULL,
    anchor_id uuid,
    sort_order integer NOT NULL DEFAULT 0,
    step_type varchar(16) NOT NULL DEFAULT 'info',
    title varchar(256),
    body text,                                      -- пояснення (простий текст/markdown-lite, підтримує {{змінні}})
    route varchar(512),                             -- сторінка, де виконується крок (для автопереходу)
    highlight varchar(16) NOT NULL DEFAULT 'element',
    parent_levels integer NOT NULL DEFAULT 0,
    rect jsonb,                                     -- {x,y,w,h} у частках вікна/елемента, якщо highlight='rect'
    padding integer NOT NULL DEFAULT 6,
    placement varchar(16) NOT NULL DEFAULT 'auto',  -- auto | top | bottom | left | right | center
    input_value text,                               -- що ввести/обрати (з {{змінними}}) — тестові дані
    input_mode varchar(16) NOT NULL DEFAULT 'show', -- show (лише показати) | prefill (автозаповнити) | require (вимагати від користувача)
    expect varchar(16) NOT NULL DEFAULT 'next',
    wait_route varchar(512),
    timeout_ms integer NOT NULL DEFAULT 8000,
    optional boolean NOT NULL DEFAULT false,        -- пропустити крок, якщо якір не знайдено
    block_outside boolean NOT NULL DEFAULT true,    -- блокувати кліки поза підсвіченою зоною
    narration_media_id uuid,
    transcript text,                                -- текст надиктовки
    created_by uuid NOT NULL, created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL, modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_gd_step PRIMARY KEY (id),
    CONSTRAINT fk_gd_step_scenario FOREIGN KEY (scenario_id) REFERENCES gd_scenario (id) ON DELETE CASCADE,
    CONSTRAINT fk_gd_step_anchor FOREIGN KEY (anchor_id) REFERENCES gd_anchor (id)
);
CREATE INDEX IF NOT EXISTS ix_gd_step_scenario ON gd_step (scenario_id, sort_order);
CREATE INDEX IF NOT EXISTS ix_gd_step_anchor ON gd_step (anchor_id);

-- Опубліковані знімки (плеєр читає лише їх; зміна чернетки не ламає тих, хто проходить)
CREATE TABLE IF NOT EXISTS gd_scenario_version (
    id uuid NOT NULL,
    scenario_id uuid NOT NULL,
    version_no integer NOT NULL,
    snapshot jsonb NOT NULL,                        -- повний сценарій: кроки, якорі, змінні
    note varchar(512),
    published_by uuid NOT NULL,
    published_on timestamp without time zone NOT NULL,
    CONSTRAINT pk_gd_scenario_version PRIMARY KEY (id),
    CONSTRAINT fk_gd_version_scenario FOREIGN KEY (scenario_id) REFERENCES gd_scenario (id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_gd_version ON gd_scenario_version (scenario_id, version_no);

-- Медіа: скріншоти якорів, аудіо-надиктовка
CREATE TABLE IF NOT EXISTS gd_media (
    id uuid NOT NULL,
    app_id uuid,
    kind varchar(16) NOT NULL,                      -- image | audio | video
    mime varchar(64) NOT NULL,
    file_name varchar(256),
    size_bytes integer NOT NULL DEFAULT 0,
    content bytea NOT NULL,
    transcript text,
    duration_ms integer,
    created_by uuid NOT NULL, created_on timestamp without time zone NOT NULL,
    modified_by uuid NOT NULL, modified_on timestamp without time zone,
    record_state integer NOT NULL DEFAULT 2,
    CONSTRAINT pk_gd_media PRIMARY KEY (id)
);

-- Проходження користувачами
CREATE TABLE IF NOT EXISTS gd_progress (
    id uuid NOT NULL,
    scenario_id uuid NOT NULL,
    version_no integer NOT NULL,
    employee_id uuid NOT NULL,
    status varchar(16) NOT NULL DEFAULT 'started',  -- started | completed | abandoned
    last_step integer NOT NULL DEFAULT 0,
    started_on timestamp without time zone NOT NULL,
    completed_on timestamp without time zone,
    mode varchar(16) NOT NULL DEFAULT 'guided',     -- guided | demo
    CONSTRAINT pk_gd_progress PRIMARY KEY (id),
    CONSTRAINT fk_gd_progress_scenario FOREIGN KEY (scenario_id) REFERENCES gd_scenario (id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_gd_progress_user ON gd_progress (employee_id, scenario_id);

-- Телеметрія якорів: плеєр повідомляє «не знайдено» / «знайдено нечітким збігом (healed)»
CREATE TABLE IF NOT EXISTS gd_anchor_report (
    id uuid NOT NULL,
    anchor_id uuid NOT NULL,
    scenario_id uuid,
    step_id uuid,
    outcome varchar(16) NOT NULL,                   -- ok | healed | missing
    how varchar(32),
    confidence numeric(4,3),
    route varchar(512),
    new_strategies jsonb,                           -- пропозиція оновлення (для healed)
    new_fingerprint jsonb,
    employee_id uuid,
    reported_on timestamp without time zone NOT NULL,
    resolved boolean NOT NULL DEFAULT false,
    CONSTRAINT pk_gd_anchor_report PRIMARY KEY (id),
    CONSTRAINT fk_gd_report_anchor FOREIGN KEY (anchor_id) REFERENCES gd_anchor (id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_gd_report_anchor ON gd_anchor_report (anchor_id, resolved, reported_on DESC);

COMMIT;
