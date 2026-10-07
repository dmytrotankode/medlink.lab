-- MedLink LIS: Dictionaries - Biomaterial Types
-- Generated for PostgreSQL (MedLink evomis)

INSERT INTO lab_biomaterial_types (id, code, name, config, is_active) VALUES
  (1, 'BM_01', 'Цільна кров (капілярна)', '{"curr_numb": 0}'::jsonb, true),
  (2, 'BM_02', 'Кал', '{"curr_numb": 0}'::jsonb, true),
  (3, 'BM_03', 'Сироватка, гепаринизована плазма', '{"curr_numb": 0}'::jsonb, true),
  (4, 'BM_04', 'Сироватка', '{"curr_numb": 0}'::jsonb, true),
  (5, 'BM_05', 'Плазма', '{"curr_numb": 0}'::jsonb, true),
  (6, 'BM_06', 'Плевральна рідина', '{"curr_numb": 0}'::jsonb, true),
  (7, 'BM_07', 'Сеча', '{"curr_numb": 0}'::jsonb, true),
  (8, 'BM_08', 'Частинки органів', '{"curr_numb": 0}'::jsonb, true),
  (9, 'BM_09', 'Дренажна рідина', '{"curr_numb": 0}'::jsonb, true),
  (10, 'BM_10', 'Ліквор', '{"curr_numb": 0}'::jsonb, true),
  (11, 'BM_11', 'Харкотіння', '{"curr_numb": 0}'::jsonb, true),
  (12, 'BM_12', 'Біологічна речовина', '{"curr_numb": 0}'::jsonb, true),
  (13, 'BM_13', 'Випотні рідини', '{"curr_numb": 0}'::jsonb, true),
  (14, 'BM_14', 'Грудне молоко', '{"curr_numb": 0}'::jsonb, true),
  (15, 'BM_15', 'Цільна кров венозна або капілярна', '{"curr_numb": 0}'::jsonb, true),
  (16, 'BM_16', 'Гепаринизована плазма', '{"curr_numb": 0}'::jsonb, true),
  (17, 'BM_17', 'Перикардиальна рідина', '{"curr_numb": 0}'::jsonb, true),
  (18, 'BM_18', 'Цільна кров (венозна)', '{"curr_numb": 0}'::jsonb, true)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, code = EXCLUDED.code, config = EXCLUDED.config;
