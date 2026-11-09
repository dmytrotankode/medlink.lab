-- Основні запити ЛІС (форма відповідає SQL, який генерує EF) з EXPLAIN ANALYZE. Тільки читання.
--   sudo -u postgres psql -d medlink_lab_test -f pg-perf-queries.sql | grep -E "^==|Execution Time|Seq Scan|Index|Bitmap|Sort"
\set ON_ERROR_STOP on
\echo == Q1 список замовлень (за замовчуванням: CITO, дата ↓), сторінка 50
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT o.id, o.order_number, o.status, p.caption FROM lab_order o LEFT JOIN mis_patient_card p ON p.id = o.patient_id WHERE o.record_state <> 4 ORDER BY o.is_urgent_cito DESC, o.order_datetime DESC LIMIT 50;
\echo == Q1c лічильник списку замовлень
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT count(*) FROM lab_order o WHERE o.record_state <> 4;
\echo == Q2 черга: активні статуси
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT o.id, o.order_number, p.caption FROM lab_order o LEFT JOIN mis_patient_card p ON p.id = o.patient_id WHERE o.record_state <> 4 AND o.status IN ('NEW','RECEIVED','IN_PROGRESS','COLLECTED','PARTIALLY_COMPLETED') ORDER BY o.is_urgent_cito DESC, o.order_datetime DESC LIMIT 50;
\echo == Q3 пошук за фрагментом номера замовлення
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT o.id FROM lab_order o WHERE o.record_state <> 4 AND o.order_number ILIKE '%0012345%' ESCAPE '\' ORDER BY o.order_datetime DESC LIMIT 50;
\echo == Q4 пошук пацієнта за ПІБ/телефоном/ІПН (ILIKE, OR по двох таблицях)
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT m.id, m.caption FROM mis_patient_card m JOIN cmn_person c ON c.id = m.person_id WHERE m.record_state <> 4 AND ((m.caption ILIKE '%ґудзь-1%' ESCAPE '\') OR (c.phone ILIKE '%ґудзь-1%' ESCAPE '\') OR (c.ipn ILIKE '%ґудзь-1%' ESCAPE '\')) ORDER BY m.caption COLLATE "uk-x-icu" LIMIT 50;
\echo == Q4b пошук пацієнта лише за ПІБ
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT m.id, m.caption FROM mis_patient_card m WHERE m.record_state <> 4 AND m.caption ILIKE '%кравчен%' ESCAPE '\' ORDER BY m.caption COLLATE "uk-x-icu" LIMIT 50;
\echo == Q5 проба за штрихкодом
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT s.id, s.order_id FROM lab_order_sample s WHERE s.barcode = '900054321';
\echo == Q6 результати проби (показники замовлення + результат)
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT t.id, t.test_code, r.id FROM lab_order_test t LEFT JOIN lab_test_result r ON r.order_test_id = t.id WHERE t.order_id = md5('o54321')::uuid::text;
\echo == Q7 журнал аудиту сутності
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT a.id FROM lab_audit_log a WHERE a.entity = 'LabOrder' AND a.entity_id = md5('o54321')::uuid::text ORDER BY a.at DESC;
\echo == Q7b останні 100 записів журналу аудиту
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT a.id FROM lab_audit_log a ORDER BY a.at DESC LIMIT 100;
\echo == Q8 робочий список: очікують виконання
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT t.id, o.order_number FROM lab_order_test t JOIN lab_order o ON o.id = t.order_id WHERE t.status = 'PENDING' ORDER BY o.is_urgent_cito DESC, o.order_datetime LIMIT 200;
\echo == Q9 список пацієнтів, сортування за алфавітом, сторінка 50
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT m.id, m.caption FROM mis_patient_card m WHERE m.record_state <> 4 ORDER BY m.caption COLLATE "uk-x-icu" LIMIT 50;
\echo == Q10 замовлення пацієнта
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT o.id FROM lab_order o WHERE o.patient_id = md5('c777')::uuid::text ORDER BY o.order_datetime DESC;
\echo == Q11 видані за період (аналітика/звіт)
EXPLAIN (ANALYZE, COSTS OFF, TIMING OFF) SELECT date_trunc('day', o.order_datetime), count(*) FROM lab_order o WHERE o.order_datetime >= now() at time zone 'utc' - interval '30 days' GROUP BY 1;
