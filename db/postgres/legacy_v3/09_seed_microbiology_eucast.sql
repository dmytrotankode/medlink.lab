-- =============================================================================
-- MedLink LIS 3.0: Seed Microbiology Pathogens, Antibiotics & EUCAST Breakpoints
-- =============================================================================

START TRANSACTION;

-- 1. Standard Clinical Microorganisms
INSERT INTO lab_micro_organisms (id, code, latin_name, common_name, kingdom, gram_stain, morphology, alert_critical_organism, is_active) VALUES
  (1, 'SAUR', 'Staphylococcus aureus', 'Золотистий стафілокок', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', false, true),
  (2, 'MRSA', 'Staphylococcus aureus (MRSA)', 'Метицилін-резистентний стафілокок', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', true, true),
  (3, 'ECOL', 'Escherichia coli', 'Кишкова паличка', 'BACTERIA', 'GRAM_NEGATIVE', 'BACILLI', false, true),
  (4, 'KPNE', 'Klebsiella pneumoniae', 'Клебсієла пневмонії', 'BACTERIA', 'GRAM_NEGATIVE', 'BACILLI', false, true),
  (5, 'PAER', 'Pseudomonas aeruginosa', 'Синьогнійна паличка', 'BACTERIA', 'GRAM_NEGATIVE', 'BACILLI', true, true),
  (6, 'ABAU', 'Acinetobacter baumannii', 'Ацинетобактер Баумана', 'BACTERIA', 'GRAM_NEGATIVE', 'COCCOBACILLI', true, true),
  (7, 'SPNE', 'Streptococcus pneumoniae', 'Пневмокок', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', false, true),
  (8, 'EFAL', 'Enterococcus faecalis', 'Ентерокок фекальний', 'BACTERIA', 'GRAM_POSITIVE', 'COCCI', false, true),
  (9, 'CALB', 'Candida albicans', 'Кандида альбіканс', 'FUNGI', NULL, 'YEAST', false, true)
ON CONFLICT (id) DO UPDATE SET latin_name = EXCLUDED.latin_name, alert_critical_organism = EXCLUDED.alert_critical_organism;

-- 2. Standard Antibiotics & Chemotherapeutics
INSERT INTO lab_antibiotics (id, code, name, group_name, atc_code, default_disk_content_mcg, is_active) VALUES
  (1, 'AMX', 'Амоксицилін', 'Penicillins', 'J01CA04', 25.0, true),
  (2, 'AMC', 'Амоксицилін / Клавуланат', 'Penicillins + Inhibitors', 'J01CR02', 30.0, true),
  (3, 'CTX', 'Цефотаксим', 'Cephalosporins 3rd gen', 'J01DD01', 30.0, true),
  (4, 'CRO', 'Цефтріаксон', 'Cephalosporins 3rd gen', 'J01DD04', 30.0, true),
  (5, 'CAZ', 'Цефтазидим', 'Cephalosporins 3rd gen', 'J01DD02', 10.0, true),
  (6, 'MEM', 'Меропенем', 'Carbapenems', 'J01DH02', 10.0, true),
  (7, 'IPM', 'Іміпенем', 'Carbapenems', 'J01DH51', 10.0, true),
  (8, 'CIP', 'Ципрофлоксацин', 'Fluoroquinolones', 'J01MA02', 5.0, true),
  (9, 'LEV', 'Левофлоксацин', 'Fluoroquinolones', 'J01MA12', 5.0, true),
  (10, 'GEN', 'Гентаміцин', 'Aminoglycosides', 'J01GB03', 10.0, true),
  (11, 'AMK', 'Амікацин', 'Aminoglycosides', 'J01GB06', 30.0, true),
  (12, 'VAN', 'Ванкоміцин', 'Glycopeptides', 'J01XA01', 5.0, true),
  (13, 'LZD', 'Лінезолід', 'Oxazolidinones', 'J01XX08', 10.0, true)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, group_name = EXCLUDED.group_name;

-- 3. EUCAST Breakpoints (v14.0)
-- E. coli vs Ciprofloxacin: S >= 25 mm, R < 22 mm; MIC S <= 0.25, R > 0.5
INSERT INTO lab_eucast_breakpoints (organism_id, antibiotic_id, eucast_version, mic_susceptible_le, mic_resistant_gt, zone_susceptible_ge, zone_resistant_lt)
VALUES (3, 8, 'v14.0_2024', 0.25, 0.5, 25.0, 22.0)
ON CONFLICT DO NOTHING;

-- E. coli vs Meropenem: S >= 22 mm, R < 16 mm; MIC S <= 2.0, R > 8.0
INSERT INTO lab_eucast_breakpoints (organism_id, antibiotic_id, eucast_version, mic_susceptible_le, mic_resistant_gt, zone_susceptible_ge, zone_resistant_lt)
VALUES (3, 6, 'v14.0_2024', 2.0, 8.0, 22.0, 16.0)
ON CONFLICT DO NOTHING;

-- S. aureus vs Vancomycin: MIC S <= 2.0, R > 2.0
INSERT INTO lab_eucast_breakpoints (organism_id, antibiotic_id, eucast_version, mic_susceptible_le, mic_resistant_gt, zone_susceptible_ge, zone_resistant_lt)
VALUES (1, 12, 'v14.0_2024', 2.0, 2.0, NULL, NULL)
ON CONFLICT DO NOTHING;

COMMIT;
