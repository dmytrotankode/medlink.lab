-- 0001: розширення для текстового пошуку. pg_trgm — «trusted» розширення (PostgreSQL 13+), його може створити власник БД;
-- якщо прав немає, скрипт не падає, а trigram-індекси наступних скриптів просто пропускаються (пошук працюватиме повільніше).
DO $$
BEGIN
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'pg_trgm недоступний (%): trigram-індекси не створюються', SQLERRM;
END $$;
