-- Синтетичне навантаження для вимірювань продуктивності. ТІЛЬКИ тестова БД (medlink_lab_test) — скрипт відмовляється працювати деінде.
-- Запуск (на сервері стенда, після прогону тестів, коли схема й демо-рядки-шаблони вже є):
--   sudo -u postgres psql -d medlink_lab_test -v n=100000 -v np=30000 -f pg-perf-seed.sql
-- Ідемпотентно за детермінованими id (повторний запуск нічого не дублює). Рядки будуються з існуючих демо-рядків як шаблонів
-- (jsonb-злиття), тому не залежить від переліку колонок. Усі згенеровані замовлення мають номер з префіксом «P».
-- ВАЖЛИВО: jsonb_populate_record викликається через LATERAL — запис «(f(x)).*» обчислює функцію окремо для КОЖНОЇ колонки (у десятки разів повільніше).
\set ON_ERROR_STOP on
\timing on
SELECT current_database() = 'medlink_lab_test' AS is_test_db \gset
\if :is_test_db
\else
  \echo 'ВІДМОВА: це не medlink_lab_test'
  \quit
\endif

CREATE TEMP TABLE t_person AS SELECT * FROM cmn_person LIMIT 1;
CREATE TEMP TABLE t_card   AS SELECT * FROM mis_patient_card LIMIT 1;
CREATE TEMP TABLE t_order  AS SELECT * FROM lab_order WHERE status = 'RELEASED' LIMIT 1;
CREATE TEMP TABLE t_sample AS SELECT * FROM lab_order_sample LIMIT 1;
CREATE TEMP TABLE t_otest  AS SELECT * FROM lab_order_test LIMIT 1;
CREATE TEMP TABLE t_result AS SELECT * FROM lab_test_result LIMIT 1;
CREATE TEMP TABLE t_audit  AS SELECT * FROM lab_audit_log LIMIT 1;
CREATE TEMP TABLE t_codes  AS SELECT code, row_number() OVER (ORDER BY code) AS rn FROM lab_test_definition;
SELECT count(*) AS test_codes FROM t_codes \gset

-- Пацієнти: персона + картка
INSERT INTO cmn_person
SELECT r.* FROM t_person t, generate_series(1, :np) g,
  LATERAL jsonb_populate_record(NULL::cmn_person, to_jsonb(t) || jsonb_build_object(
    'id', md5('p' || g)::uuid::text,
    'last_name', (ARRAY['Шевченко','Коваленко','Бондаренко','Ткаченко','Кравченко','Олійник','Шевчук','Поліщук','Ґудзь','Єрмоленко','Їжакевич','Іваненко'])[1 + g % 12] || '-' || g,
    'name', (ARRAY['Олена','Іван','Марія','Андрій','Галина','Євген','Ірина','Ярослав'])[1 + g % 8],
    'middle_name', (ARRAY['Петрівна','Іванович','Олексіївна','Миколайович'])[1 + g % 4],
    'phone', '+380' || lpad((500000000 + g * 37)::text, 9, '0'),
    'ipn', lpad((1000000000 + g * 7)::text, 10, '0'))) r
ON CONFLICT DO NOTHING;

INSERT INTO mis_patient_card
SELECT r.* FROM t_card t, generate_series(1, :np) g, cmn_person p,
  LATERAL jsonb_populate_record(NULL::mis_patient_card, to_jsonb(t) || jsonb_build_object(
    'id', md5('c' || g)::uuid::text, 'person_id', p.id,
    'caption', p.last_name || ' ' || p.name || ' ' || p.middle_name)) r
WHERE p.id = md5('p' || g)::uuid::text
ON CONFLICT DO NOTHING;

-- Замовлення: 94 % видані, решта активні (черга); кожне 20-те — CITO
INSERT INTO lab_order
SELECT r.* FROM t_order t, generate_series(1, :n) g,
  LATERAL jsonb_populate_record(NULL::lab_order, to_jsonb(t) || jsonb_build_object(
    'id', md5('o' || g)::uuid::text,
    'order_number', 'P' || lpad(g::text, 7, '0'),
    'patient_id', md5('c' || (1 + g % :np))::uuid::text,
    'order_datetime', to_char(now() at time zone 'utc' - make_interval(mins => (g * 5) % 525600), 'YYYY-MM-DD"T"HH24:MI:SS'),
    'status', CASE WHEN g % 50 = 0 THEN 'NEW' WHEN g % 50 = 1 THEN 'RECEIVED' WHEN g % 50 = 2 THEN 'IN_PROGRESS' ELSE 'RELEASED' END,
    'is_urgent_cito', (g % 20 = 0),
    'verify_token', md5('v' || g))) r
ON CONFLICT DO NOTHING;

INSERT INTO lab_order_sample
SELECT r.* FROM t_sample t, generate_series(1, :n) g,
  LATERAL jsonb_populate_record(NULL::lab_order_sample, to_jsonb(t) || jsonb_build_object(
    'id', md5('s' || g)::uuid::text, 'order_id', md5('o' || g)::uuid::text, 'barcode', '9' || lpad(g::text, 8, '0'))) r
ON CONFLICT DO NOTHING;

-- по 3 показники на замовлення (+ результат для виданих)
INSERT INTO lab_order_test
SELECT r.* FROM t_otest t, generate_series(1, :n) g, generate_series(1, 3) k, t_codes c,
  LATERAL jsonb_populate_record(NULL::lab_order_test, to_jsonb(t) || jsonb_build_object(
    'id', md5('t' || g || '-' || k)::uuid::text, 'order_id', md5('o' || g)::uuid::text, 'sample_id', md5('s' || g)::uuid::text,
    'test_code', c.code, 'status', CASE WHEN g % 50 < 3 THEN 'PENDING' ELSE 'VERIFIED' END)) r
WHERE c.rn = 1 + (g + k) % :test_codes
ON CONFLICT DO NOTHING;

INSERT INTO lab_test_result
SELECT r.* FROM t_result t, generate_series(1, :n) g, generate_series(1, 3) k,
  LATERAL jsonb_populate_record(NULL::lab_test_result, to_jsonb(t) || jsonb_build_object(
    'id', md5('r' || g || '-' || k)::uuid::text, 'order_test_id', md5('t' || g || '-' || k)::uuid::text)) r
WHERE g % 50 >= 3
ON CONFLICT DO NOTHING;

INSERT INTO lab_audit_log
SELECT r.* FROM t_audit t, generate_series(1, :n) g, generate_series(1, 3) k,
  LATERAL jsonb_populate_record(NULL::lab_audit_log, to_jsonb(t) || jsonb_build_object(
    'id', md5('a' || g || '-' || k)::uuid::text, 'entity', 'LabOrder', 'entity_id', md5('o' || g)::uuid::text,
    'at', to_char(now() at time zone 'utc' - make_interval(mins => (g * 5 + k) % 525600), 'YYYY-MM-DD"T"HH24:MI:SS'))) r
ON CONFLICT DO NOTHING;

ANALYZE;
SELECT 'lab_order' t, count(*) FROM lab_order UNION ALL SELECT 'lab_order_test', count(*) FROM lab_order_test UNION ALL SELECT 'lab_test_result', count(*) FROM lab_test_result
UNION ALL SELECT 'lab_audit_log', count(*) FROM lab_audit_log UNION ALL SELECT 'mis_patient_card', count(*) FROM mis_patient_card;
