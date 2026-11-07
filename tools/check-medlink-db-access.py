# -*- coding: utf-8 -*-
"""Перевірка прав доступу до тестової бази MedLink (evomis-test).
Запуск на ПК під VPN:  python tools\check-medlink-db-access.py
Пароль запитується інтерактивно і ніде не зберігається."""
import getpass, sys
try:
    import psycopg2
except ImportError:
    sys.exit("Потрібен psycopg2: pip install psycopg2-binary")

host = input("Host [192.168.255.1]: ").strip() or "192.168.255.1"
db = input("Database [evomis-test]: ").strip() or "evomis-test"
user = input("User [d.tanko]: ").strip() or "d.tanko"
pwd = getpass.getpass("Password: ")

conn = psycopg2.connect(host=host, port=5432, dbname=db, user=user, password=pwd, connect_timeout=10)
cur = conn.cursor()
cur.execute("SELECT current_user, version();")
print("Підключено як:", cur.fetchone()[0])
cur.execute("""
SELECT has_schema_privilege('public','CREATE') AS can_create_tables,
       has_schema_privilege('public','USAGE')  AS can_use_schema,
       has_table_privilege('mis_patient_card','SELECT') AS read_patient,
       has_table_privilege('mis_patient_card','INSERT') AS write_patient,
       has_table_privilege('org_employee','SELECT') AS read_employee,
       has_table_privilege('org_employee','INSERT') AS write_employee,
       has_table_privilege('dct_service','SELECT') AS read_service,
       has_table_privilege('org_department','SELECT') AS read_department;
""")
cols = [d[0] for d in cur.description]
for c, v in zip(cols, cur.fetchone()):
    print(f"  {c:22s} {'так' if v else 'ні'}")
cur.execute("SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name LIKE 'lab\\_%';")
print("Таблиць lab_* уже в базі:", cur.fetchone()[0])
cur.execute("SELECT rolname, rolcreatedb, rolsuper FROM pg_roles WHERE rolname=current_user;")
print("Роль:", cur.fetchone())
conn.close()
