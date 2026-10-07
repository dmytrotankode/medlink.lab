-- =============================================================================
-- MedLink LIS 3.0: MASTER DEPLOYMENT SCRIPT
-- Executes all schema creations, views, indexes, and standard seeds
-- in a single resilient PostgreSQL transaction.
-- =============================================================================

\echo '=== 1/9 Deploying MedLink LIS Core Schema ==='
\ir 01_lis_schema_core.sql

\echo '=== 2/9 Deploying Microbiology & EUCAST Schema ==='
\ir 02_lis_schema_microbiology_eucast.sql

\echo '=== 3/9 Deploying evomis Integration Views & Constraints ==='
\ir 03_evomis_integration_views_and_fk.sql

\echo '=== 4/9 Seeding Biomaterials ==='
\ir 04_seed_biomaterials.sql

\echo '=== 5/9 Seeding Vacuum Tube Types ==='
\ir 05_seed_tube_types.sql

\echo '=== 6/9 Seeding Analytical Methods ==='
\ir 06_seed_method_types.sql

\echo '=== 7/9 Seeding Analyzer Models & Protocols ==='
\ir 07_seed_analyzer_types.sql

\echo '=== 8/9 Seeding Parameters, Profiles, LOINC & Reference Ranges ==='
\ir 08_seed_parameters_and_profiles.sql

\echo '=== 9/9 Seeding Microbiology Organisms, Antibiotics & EUCAST ==='
\ir 09_seed_microbiology_eucast.sql

\echo '=== MEDLINK LIS 3.0 DATABASE DEPLOYMENT COMPLETE! ==='
