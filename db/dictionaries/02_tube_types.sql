-- MedLink LIS: Dictionaries - Tube & Container Types
-- Generated for PostgreSQL (MedLink evomis)

INSERT INTO lab_tube_types (id, code, name, color_code, volume_ml, is_active) VALUES
  (1, 'Плазма', 'Натрий-цитрат', '32768', '3', true),
  (2, 'К2 ЕДТА', 'К2 ЕДТА', '128', '1,5', true),
  (3, 'К3 ЕДТА', 'К3 ЕДТА', '16711935', '2,6', true),
  (4, 'Загальний аналіз крові', 'К3 ЕДТА/К2 ЕДТА', '15780518', '2,6', true),
  (5, 'Плазма', 'Li-гепарин', '65535', '7', true),
  (6, 'Кров на стерильність', 'Стерильне подвійне середовище', '12632256', '5', true),
  (7, 'Serum', 'Гранули', '255', '2,6', true),
  (8, 'Аплікатор', 'Аплікатор пластиковий стрижень', '15793151', NULL, true),
  (9, 'Ємкість для забору кала', 'Ємкість для забору кала-стерильна', '-16777190', '30', true),
  (10, 'Blutgas', 'Гази крові', '8421504', '2,3', true),
  (11, 'Предметне скло', 'Предметне скло', '16777215', NULL, true),
  (12, 'Ємкість для забору сечі', 'Ємкість для забору сечі-стерільна', '65280', '60', true)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, code = EXCLUDED.code, color_code = EXCLUDED.color_code, volume_ml = EXCLUDED.volume_ml;
