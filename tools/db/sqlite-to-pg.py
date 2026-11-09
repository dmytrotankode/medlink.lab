#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Перенесення даних ЛІС зі SQLite у PostgreSQL (ідемпотентне: INSERT ... ON CONFLICT DO NOTHING) із перевіркою кількості рядків.

Запуск на сервері від root (потрібні python3 і доступ до локального postgres через peer-автентифікацію):
    python3 sqlite-to-pg.py --sqlite /srv/medlink-lab/data/medlink_lis.db --pg-db medlink_lab                 # підсумок без змін (dry-run)
    python3 sqlite-to-pg.py --sqlite /srv/medlink-lab/data/medlink_lis.db --pg-db medlink_lab --apply          # перенести
    python3 sqlite-to-pg.py --sqlite /srv/medlink-lab/data/medlink_guide.db --pg-db medlink_lab --schema guide --apply   # Guide
Для перевірки на копії: --pg-db medlink_lab_test (схема має вже існувати — її створює запуск API).
Що робить: бере таблиці, що є і в SQLite, і в PostgreSQL, переносить спільні колонки з приведенням типів (boolean 0/1, timestamp,
bytea), у одній транзакції з session_replication_role=replica (порядок таблиць за FK неважливий). Наявні в PG рядки з тим самим
ключем не перезаписуються. Після --apply друкує таблиці, де в PG рядків менше, ніж у SQLite (код виходу 3).
"""
import argparse, sqlite3, subprocess, sys

def psql(db, sql, stdin=None):
    cmd = ["runuser", "-u", "postgres", "--", "psql", "-X", "-q", "-At", "-F", "\t", "-v", "ON_ERROR_STOP=1", "-d", db]
    cmd += (["-1"] if stdin is not None else ["-c", sql])
    r = subprocess.run(cmd, input=stdin, capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        sys.exit("psql: " + r.stderr.strip())
    return r.stdout

def lit(v, typ):
    if v is None: return "NULL"
    if typ == "boolean": return "true" if v in (1, "1", True, "true", "True") else "false"
    if typ in ("integer", "bigint", "smallint"): return str(int(v))
    if typ in ("numeric", "double precision", "real"): return repr(float(v)) if typ != "numeric" else str(v)
    if typ == "bytea": return "'" + chr(92) + "x" + bytes(v).hex() + "'"
    s = str(v).replace("'", "''")
    if "\\" in s: return "E'" + s.replace("\\", "\\\\") + "'"
    return "'" + s + "'"

def default_lit(typ):
    if typ == "boolean": return "false"
    if typ in ("integer", "bigint", "smallint", "numeric", "double precision", "real"): return "0"
    if typ.startswith("timestamp"): return "now() at time zone 'utc'"
    if typ == "uuid": return "'00000000-0000-0000-0000-000000000000'"
    if typ == "bytea": return "''::bytea"
    return "''"

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sqlite", required=True); ap.add_argument("--pg-db", required=True)
    ap.add_argument("--schema", default="public"); ap.add_argument("--apply", action="store_true")
    a = ap.parse_args()
    lite = sqlite3.connect("file:%s?mode=ro" % a.sqlite, uri=True)
    lite.text_factory = lambda b: b.decode("utf-8", "replace")
    tables = [r[0] for r in lite.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '%EFMigrations%'")]
    pgcols, req = {}, {}
    for line in psql(a.pg_db, "SELECT table_name, column_name, data_type, (is_nullable = 'NO' AND column_default IS NULL) FROM information_schema.columns WHERE table_schema = '%s' ORDER BY table_name, ordinal_position" % a.schema).splitlines():
        t, c, d, required = line.split("\t"); pgcols.setdefault(t, {})[c] = d
        if required == "t": req.setdefault(t, []).append(c)
    out, plan, missing = ["SET session_replication_role = replica;"], [], []
    for t in sorted(tables):
        if t not in pgcols: missing.append(t); continue
        src_cols = [r[1] for r in lite.execute('PRAGMA table_info("%s")' % t)]
        cols = [c for c in src_cols if c in pgcols[t]]
        # нові обов'язкові колонки PG, яких немає в старій SQLite: нейтральне значення за типом
        extra = [c for c in req.get(t, []) if c not in src_cols]
        n = lite.execute('SELECT count(*) FROM "%s"' % t).fetchone()[0]
        plan.append((t, n))
        if n == 0 or not cols: continue
        coll = ", ".join('"%s"' % c for c in cols + extra)
        for row in lite.execute('SELECT %s FROM "%s"' % (", ".join('"%s"' % c for c in cols), t)):
            vals = ", ".join([lit(v, pgcols[t][c]) for v, c in zip(row, cols)] + [default_lit(pgcols[t][c]) for c in extra])
            out.append('INSERT INTO "%s"."%s" (%s) VALUES (%s) ON CONFLICT DO NOTHING;' % (a.schema, t, coll, vals))
    print("Таблиць у SQLite: %d; є в PG: %d; немає в PG: %s" % (len(tables), len(plan), ", ".join(missing) or "-"))
    print("Рядків у SQLite: %d" % sum(n for _, n in plan))
    if not a.apply:
        print("DRY-RUN: нічого не змінено. Додайте --apply."); return
    psql(a.pg_db, None, stdin="\n".join(out) + "\n")
    bad = []
    for t, n in plan:
        have = int(psql(a.pg_db, 'SELECT count(*) FROM "%s"."%s"' % (a.schema, t)).strip())
        if have < n: bad.append((t, n, have))
    print("Перенесено. Перевірено кількість рядків у %d таблицях." % len(plan))
    if bad:
        for t, n, h in bad: print("  РОЗБІЖНІСТЬ %s: SQLite %d, PG %d" % (t, n, h))
        sys.exit(3)
    print("Розбіжностей немає (у PG рядків не менше, ніж у SQLite у кожній таблиці).")

main()
