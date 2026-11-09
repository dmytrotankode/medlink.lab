-- 0002: індекси під реальні запити ЛІС (виміряно на 100 000 замовлень / 30 000 пацієнтів у тестовій БД, див. docs/POSTGRES_READINESS.md).
-- Усі індекси — IF NOT EXISTS; trigram-індекси (пошук ILIKE '%фрагмент%') і індекс українського сортування створюються лише за наявності
-- розширення pg_trgm / ICU-колації uk-x-icu у кластері. Дані не змінюються.

-- Список замовлень і черга: порядок «CITO першими, нові зверху» без сортування всієї таблиці (partial: видалені не індексуються)
CREATE INDEX IF NOT EXISTS ix_lab_order_cito_datetime ON lab_order (is_urgent_cito DESC, order_datetime DESC) WHERE record_state <> 4;

-- Робочі списки за статусом показників (PENDING тощо) з переходом до замовлення
CREATE INDEX IF NOT EXISTS ix_lab_order_test_status_order ON lab_order_test (status, order_id);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
        -- пошук за фрагментом номера замовлення, ПІБ, телефону, ІПН (LIKE/ILIKE '%…%')
        CREATE INDEX IF NOT EXISTS ix_lab_order_number_trgm ON lab_order USING gin (order_number gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_mis_patient_card_caption_trgm ON mis_patient_card USING gin (caption gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_cmn_person_phone_trgm ON cmn_person USING gin (phone gin_trgm_ops);
        CREATE INDEX IF NOT EXISTS ix_cmn_person_ipn_trgm ON cmn_person USING gin (ipn gin_trgm_ops);
    ELSE
        RAISE NOTICE 'pg_trgm відсутній: trigram-індекси пропущено';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_collation WHERE collname = 'uk-x-icu') THEN
        -- список пацієнтів за абеткою (ORDER BY caption COLLATE "uk-x-icu") читається з індексу
        CREATE INDEX IF NOT EXISTS ix_mis_patient_card_caption_uk ON mis_patient_card (caption COLLATE "uk-x-icu") WHERE record_state <> 4;
    ELSE
        RAISE NOTICE 'колація uk-x-icu відсутня: індекс абетки пропущено';
    END IF;
END $$;
