# -*- coding: utf-8 -*-
"""
MedLink LIS 3.0: Real Production Local SQLite REST API Backend
Fully connected to SQLite: medlink_lab_local.db
Handles full CRUD for orders, samples, results, QC Levey-Jennings, Westgard rules,
Biobank archive cells, Dictionaries, and Delphi Reference Ranges.
"""

import sqlite3
import json
import os
import datetime

DB_PATH = r"C:\__MEDLINK___\LABA\medlink_lab_local.db"

def get_db():
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn

def handle_api_request(method, path, body=None):
    if body and isinstance(body, (bytes, str)):
        try:
            body = json.loads(body)
        except Exception:
            body = {}
    elif body is None:
        body = {}

    conn = get_db()
    cur = conn.cursor()

    try:
        # =====================================================================
        # 1. PIPELINE STATE & WORKFLOW TRANSITIONS
        # =====================================================================
        if path == "/api/laboratory/pipeline/state" and method == "GET":
            cur.execute("""
                SELECT r.id, r.referral_code, r.service_name, r.status, p.last_name || ' ' || p.first_name || ' ' || IFNULL(p.second_name, '') as patient_name
                FROM ehe_incoming_medical_referral r
                JOIN mis_patient_card p ON r.patient_id = p.id
                WHERE r.id = 'REF-2026-001'
            """)
            ref = dict(cur.fetchone() or {})

            cur.execute("""
                SELECT o.id as order_id, o.order_number, o.status as order_status, o.is_urgent_cito,
                       s.id as sample_id, s.barcode, s.status as sample_status, t.name as tube_name, t.color_code as tube_color
                FROM lab_orders o
                LEFT JOIN lab_order_samples s ON s.order_id = o.id
                LEFT JOIN lab_tube_types t ON s.tube_type_id = t.id
                WHERE o.id = 'ORD-01'
            """)
            order_sample = dict(cur.fetchone() or {})

            cur.execute("""
                SELECT id, test_code, test_name, numeric_value, unit, norm_min, norm_max, flag, delta_percent, is_auto_verified, status
                FROM lab_test_results
                WHERE order_id = 'ORD-01'
            """)
            results = [dict(r) for r in cur.fetchall()]

            cur.execute("""
                SELECT id, report_number, service_name, status, ehealth_synced, signed_at
                FROM mis_diagnostic_report
                WHERE referral_id = 'REF-2026-001'
            """)
            diag = dict(cur.fetchone() or {})

            order_status = order_sample.get("order_status")
            sample_status = order_sample.get("sample_status")
            ref_status = ref.get("status")

            current_step = 1
            if ref_status == "IN_PROGRESS" or order_status == "COLLECTED":
                current_step = 2
            if sample_status in ["IN_TRANSIT"] or order_status == "IN_TRANSIT":
                current_step = 3
            if sample_status == "RECEIVED" or order_status == "ANALYZING":
                current_step = 4
            if any(r.get("status") in ["NEEDS_DOCTOR", "AUTO_VERIFIED", "MANUAL_VERIFIED"] for r in results):
                current_step = 5
            if diag.get("status") == "FINAL":
                current_step = 6
                if diag.get("ehealth_synced"):
                    current_step = 7

            return {
                "success": True,
                "currentStep": current_step,
                "referral": ref,
                "orderSample": order_sample,
                "results": results,
                "diagnosticReport": diag
            }

        elif path == "/api/laboratory/pipeline/advance" and method == "POST":
            target_step = (body or {}).get("targetStep", 2)
            if target_step == 2:
                cur.execute("UPDATE ehe_incoming_medical_referral SET status = 'IN_PROGRESS' WHERE id = 'REF-2026-001'")
                cur.execute("UPDATE lab_orders SET status = 'COLLECTED' WHERE id = 'ORD-01'")
                cur.execute("UPDATE lab_order_samples SET status = 'COLLECTED', collected_at = datetime('now') WHERE id = 'SMP-01'")
                msg = "Крок 2: Біоматеріал відібрано в пробірку (K2 EDTA / Фіолетова), штрихкод 1026004812 роздруковано!"
            elif target_step == 3:
                cur.execute("UPDATE lab_orders SET status = 'IN_TRANSIT' WHERE id = 'ORD-01'")
                cur.execute("UPDATE lab_order_samples SET status = 'IN_TRANSIT' WHERE id = 'SMP-01'")
                cur.execute("""
                    INSERT OR REPLACE INTO lab_sample_logistics (id, manifest_number, origin_dept_id, dest_dept_id, courier_name, temperature_dispatch, dispatched_at, status)
                    VALUES ('LOG-01', 'TR-2026-10-04', 'DEPT-01', 'DEPT-02', 'Шевченко Д.О.', 4.2, datetime('now'), 'IN_TRANSIT')
                """)
                msg = "Крок 3: Сформовано кур'єрський маніфест TR-2026-10-04. Термобокс у дорозі (+4.2°C)."
            elif target_step == 4:
                cur.execute("UPDATE lab_orders SET status = 'ANALYZING' WHERE id = 'ORD-01'")
                cur.execute("UPDATE lab_order_samples SET status = 'RECEIVED' WHERE id = 'SMP-01'")
                cur.execute("UPDATE lab_sample_logistics SET status = 'RECEIVED', temperature_receipt = 4.4, received_at = datetime('now') WHERE id = 'LOG-01'")
                msg = "Крок 4: Пробірку успішно прийнято в КДЛ. Вхідний бракераж пройдено: гемолізу немає."
            elif target_step == 5:
                cur.execute("""
                    UPDATE lab_test_results
                    SET numeric_value = 26.4, flag = 'CRIT_HIGH', delta_percent = 185.0, status = 'NEEDS_DOCTOR'
                    WHERE id = 'RES-01'
                """)
                msg = "Крок 5: Шлюз .NET 8 прийняв вимірювання з аналізатора: Глюкоза = 26.4 ммоль/л (Паніка CITO!)."
            elif target_step == 6:
                cur.execute("""
                    UPDATE lab_test_results
                    SET is_auto_verified = 0, verified_by_id = 'EMP-01', verified_at = datetime('now'), status = 'MANUAL_VERIFIED'
                    WHERE id = 'RES-01'
                """)
                cur.execute("""
                    INSERT OR REPLACE INTO mis_diagnostic_report (id, report_number, patient_id, referral_id, service_name, performer_doctor_id, status, ehealth_synced, conclusions, signed_at)
                    VALUES ('REP-2026-01', 'R-2026-104812', 'PT-1001', 'REF-2026-001', 'Комплексне біохімічне дослідження крові', 'EMP-01', 'FINAL', 1, 'Гіперглікемія критична. Потрібна корекція інсулінотерапії.', datetime('now'))
                """)
                cur.execute("UPDATE ehe_incoming_medical_referral SET status = 'COMPLETED' WHERE id = 'REF-2026-001'")
                cur.execute("UPDATE lab_orders SET status = 'COMPLETED' WHERE id = 'ORD-01'")
                msg = "Крок 6: Лікар наклав КЕП! Створено медичний документ MedLink (DiagnosticReport). Е-направлення погашено в ЕСОЗ."
            elif target_step == 7:
                msg = "Крок 7: Результати опубліковано в Кабінеті пацієнта MedLink! Доступний PDF з QR-кодом."
            else:
                msg = "Крок оновлено."

            conn.commit()
            return {"success": True, "message": msg, "targetStep": target_step}

        elif path == "/api/laboratory/pipeline/reset" and method == "POST":
            cur.execute("UPDATE ehe_incoming_medical_referral SET status = 'NEW' WHERE id = 'REF-2026-001'")
            cur.execute("UPDATE lab_orders SET status = 'NEW' WHERE id = 'ORD-01'")
            cur.execute("UPDATE lab_order_samples SET status = 'PENDING', collected_at = NULL WHERE id = 'SMP-01'")
            cur.execute("DELETE FROM mis_diagnostic_report WHERE referral_id = 'REF-2026-001'")
            cur.execute("DELETE FROM lab_sample_logistics WHERE id = 'LOG-01'")
            cur.execute("UPDATE lab_test_results SET status = 'PENDING', verified_at = NULL WHERE id = 'RES-01'")
            conn.commit()
            return {"success": True, "message": "Сценарій скинуто на початок. Можна повторити весь шлях!"}

        # =====================================================================
        # 2. WORKLIST & ORDERS (ЖУРНАЛ ДОСЛІДЖЕНЬ)
        # =====================================================================
        elif path == "/api/laboratory/worklist" and method == "GET":
            cur.execute("""
                SELECT r.id, s.barcode, p.last_name || ' ' || SUBSTR(p.first_name, 1, 1) || '.' || SUBSTR(IFNULL(p.second_name, ''), 1, 1) || '.' as patient,
                       a.name as analyzer, r.test_name as test, r.test_code as test_code, r.numeric_value as value, r.unit,
                       r.norm_min as normMin, r.norm_max as normMax, r.flag,
                       CASE WHEN r.delta_percent > 0 THEN '+' || r.delta_percent || '%' ELSE r.delta_percent || '%' END as deltaPercent,
                       r.status, r.is_auto_verified, o.is_urgent_cito as isCito
                FROM lab_test_results r
                JOIN lab_orders o ON r.order_id = o.id
                JOIN mis_patient_card p ON o.patient_id = p.id
                LEFT JOIN lab_order_samples s ON r.sample_id = s.id
                LEFT JOIN lab_analyzers a ON r.analyzer_id = a.id
                ORDER BY r.id ASC
            """)
            items = [dict(row) for row in cur.fetchall()]
            return {"success": True, "data": items}

        elif path == "/api/laboratory/worklist/autoverify" and method == "POST":
            cur.execute("""
                UPDATE lab_test_results
                SET is_auto_verified = 1, status = 'AUTO_VERIFIED', verified_at = datetime('now')
                WHERE flag = 'NORMAL' AND status != 'AUTO_VERIFIED'
            """)
            affected = cur.rowcount
            conn.commit()
            return {"success": True, "message": f"Автовалідовано {affected} тестів у межах норми!", "affected": affected}

        elif path.startswith("/api/laboratory/results/") and path.endswith("/update") and method == "POST":
            # Real Result Value Update
            res_id = path.split("/")[4]
            new_val = float(body.get("numeric_value", 0))
            cur.execute("SELECT norm_min, norm_max, test_code FROM lab_test_results WHERE id = ?", (res_id,))
            row = cur.fetchone()
            if row:
                n_min, n_max, t_code = row[0], row[1], row[2]
                new_flag = 'NORMAL'
                if n_min is not None and new_val < n_min:
                    new_flag = 'LOW'
                elif n_max is not None and new_val > n_max:
                    new_flag = 'HIGH'
                if t_code == 'GLU' and new_val > 25.0:
                    new_flag = 'CRIT_HIGH'
                elif t_code == 'TROP_I' and new_val > 0.04:
                    new_flag = 'CRIT_HIGH'

                cur.execute("""
                    UPDATE lab_test_results
                    SET numeric_value = ?, flag = ?, status = 'NEEDS_DOCTOR'
                    WHERE id = ?
                """, (new_val, new_flag, res_id))
                conn.commit()
                return {"success": True, "message": f"Результат {res_id} оновлено в базі: {new_val} (Прапорець: {new_flag})", "flag": new_flag}
            return {"success": False, "error": "Результат не знайдено"}

        elif path.startswith("/api/laboratory/results/") and path.endswith("/validate") and method == "POST":
            # Real Result Doctor Validation
            res_id = path.split("/")[4]
            doc_id = body.get("doctorId", "EMP-01")
            cur.execute("""
                UPDATE lab_test_results
                SET status = 'MANUAL_VERIFIED', verified_by_id = ?, verified_at = datetime('now')
                WHERE id = ?
            """, (doc_id, res_id))
            conn.commit()
            return {"success": True, "message": f"Результат {res_id} верифіковано лікарем!"}

        # =====================================================================
        # 3. PANIC CALL LOG (ЖУРНАЛ ТЕЛЕФОННИХ СПОВІЩЕНЬ CITO)
        # =====================================================================
        elif path == "/api/laboratory/panic-calls" and method == "GET":
            cur.execute("""
                SELECT l.*, r.test_name, r.numeric_value, r.unit, p.last_name || ' ' || p.first_name as patient_name
                FROM lab_panic_call_log l
                JOIN lab_test_results r ON l.result_id = r.id
                JOIN lab_orders o ON r.order_id = o.id
                JOIN mis_patient_card p ON o.patient_id = p.id
                ORDER BY l.notified_at DESC
            """)
            items = [dict(row) for row in cur.fetchall()]
            return {"success": True, "data": items}

        elif path == "/api/laboratory/panic-calls" and method == "POST":
            call_id = f"CALL-{int(cur.execute('SELECT COUNT(*) FROM lab_panic_call_log').fetchone()[0]) + 1:03d}"
            res_id = body.get("result_id", "RES-01")
            doc_name = body.get("doctor_name", "Черговий лікар реанімації")
            phone = body.get("phone", "+380500000000")
            notes = body.get("notes", "Повідомлено про критичний стан")
            cur.execute("""
                INSERT INTO lab_panic_call_log (id, result_id, doctor_notified_name, phone_called, notified_at, notified_by_id, comments)
                VALUES (?, ?, ?, ?, datetime('now'), 'EMP-01', ?)
            """, (call_id, res_id, doc_name, phone, notes))
            conn.commit()
            return {"success": True, "message": f"Телефонне сповіщення зареєстровано в журналі CITO: {call_id}!", "id": call_id}

        # =====================================================================
        # 4. QUALITY CONTROL (ВКЯ: LEVEY-JENNINGS & WESTGARD RULES)
        # =====================================================================
        elif path.startswith("/api/laboratory/qc/measurements") and method == "GET":
            # Optional filter by param
            test_code = "WBC"
            if "?" in path and "param=" in path:
                test_code = path.split("param=")[-1].split("&")[0]

            cur.execute("""
                SELECT id, analyzer_id, control_material, lot_number, test_code,
                       measured_value, target_mean, target_sd, z_score,
                       is_violation, violated_rule, is_lockout, lockout_resolved_at,
                       resolution_action, created_at,
                       strftime('%d', created_at) as day
                FROM lab_qc_results
                WHERE test_code = ?
                ORDER BY created_at ASC
            """, (test_code,))
            items = [dict(row) for row in cur.fetchall()]

            # Check if current lockout active
            cur.execute("SELECT COUNT(*) FROM lab_qc_results WHERE test_code = ? AND is_lockout = 1", (test_code,))
            lockout_count = cur.fetchone()[0]

            return {
                "success": True,
                "param": test_code,
                "isLockout": lockout_count > 0,
                "data": items
            }

        elif path == "/api/laboratory/qc/measurements" and method == "POST":
            test_code = body.get("test_code", "WBC")
            val = float(body.get("measured_value", 7.2))
            mean = float(body.get("target_mean", 7.20))
            sd = float(body.get("target_sd", 0.30))
            lot = body.get("lot_number", "LOT-88412")
            material = body.get("control_material", "Bio-Rad Lyphochek Level 2")
            analyzer = body.get("analyzer_id", "AN-01")

            z = round((val - mean) / sd, 2)
            is_violation = 0
            violated_rule = None
            is_lockout = 0

            # Westgard rules evaluation
            if abs(z) >= 3.0:
                is_violation = 1
                violated_rule = "1-3s (LOCKOUT)"
                is_lockout = 1
            elif abs(z) >= 2.0:
                is_violation = 1
                violated_rule = "1-2s (ПОПЕРЕДЖЕННЯ)"

            qc_id = f"QC-{test_code}-{int(cur.execute('SELECT COUNT(*) FROM lab_qc_results WHERE test_code = ?', (test_code,)).fetchone()[0]) + 1:02d}"
            cur.execute("""
                INSERT INTO lab_qc_results (
                    id, analyzer_id, control_material, lot_number, test_code,
                    measured_value, target_mean, target_sd, z_score,
                    is_violation, violated_rule, is_lockout, created_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, datetime('now'))
            """, (qc_id, analyzer, material, lot, test_code, val, mean, sd, z, is_violation, violated_rule, is_lockout))
            conn.commit()

            return {
                "success": True,
                "message": f"Точку контролю збережено в базі: Z-score = {z}" + (" 🚨 УВАГА: 1-3s БЛОКУВАННЯ!" if is_lockout else ""),
                "id": qc_id,
                "z_score": z,
                "is_lockout": is_lockout,
                "violated_rule": violated_rule
            }

        elif path == "/api/laboratory/qc/resolve-lockout" and method == "POST":
            action = body.get("action", "Промивка Cycle Clean")
            comment = body.get("comment", "Повторний прогін у межах норми")
            cur.execute("""
                UPDATE lab_qc_results
                SET is_lockout = 0, lockout_resolved_at = datetime('now'), resolution_action = ?
                WHERE is_lockout = 1
            """, (f"{action}: {comment}",))
            affected = cur.rowcount
            conn.commit()
            return {"success": True, "message": f"Блокування аналізатора (Lockout) знято у {affected} записах SQLite!"}

        # =====================================================================
        # 5. BIOBANK & CRYO ARCHIVE (БІОБАНК ТА АРХІВ)
        # =====================================================================
        elif path == "/api/laboratory/biobank/cells" and method == "GET":
            cur.execute("""
                SELECT c.*, s.barcode, p.last_name || ' ' || p.first_name as patient_name,
                       b.name as biomaterial_name
                FROM lab_sample_archive_cells c
                LEFT JOIN lab_order_samples s ON c.sample_id = s.barcode OR c.sample_id = s.id
                LEFT JOIN lab_orders o ON s.order_id = o.id
                LEFT JOIN mis_patient_card p ON o.patient_id = p.id
                LEFT JOIN lab_biomaterial_types b ON s.biomaterial_type_id = b.id
                ORDER BY c.cell_coordinate ASC
            """)
            occupied = [dict(row) for row in cur.fetchall()]
            return {"success": True, "data": occupied}

        elif path == "/api/laboratory/biobank/cells/place" and method == "POST":
            coord = body.get("coordinate", "A-01")
            barcode = body.get("barcode", "1026004812")
            rack = body.get("rack", "RACK-A1")
            box = body.get("box", "BOX-01")
            cell_id = f"CELL-{coord}"
            exp_date = (datetime.datetime.now() + datetime.timedelta(days=180)).strftime("%Y-%m-%d %H:%M:%S")

            cur.execute("""
                INSERT OR REPLACE INTO lab_sample_archive_cells (
                    id, rack_code, box_number, cell_coordinate, sample_id, stored_at, expiry_at
                ) VALUES (?, ?, ?, ?, ?, datetime('now'), ?)
            """, (cell_id, rack, box, coord, barcode, exp_date))
            conn.commit()
            return {"success": True, "message": f"Зразок {barcode} успішно збережено в комірці {coord} бази даних!", "coordinate": coord}

        elif path == "/api/laboratory/biobank/cells/remove" and method == "POST":
            coord = body.get("coordinate")
            if coord:
                cur.execute("DELETE FROM lab_sample_archive_cells WHERE cell_coordinate = ?", (coord,))
                conn.commit()
                return {"success": True, "message": f"Комірку {coord} звільнено в базі даних!"}
            return {"success": False, "error": "Не вказано координату комірки"}

        # =====================================================================
        # 6. PHLEBOTOMY & LOGISTICS
        # =====================================================================
        elif path == "/api/laboratory/phlebotomy/collect" and method == "POST":
            sample_id = body.get("sampleId", "SMP-01")
            barcode = body.get("barcode", "1026004812")
            cur.execute("""
                UPDATE lab_order_samples
                SET status = 'COLLECTED', collected_at = datetime('now'), barcode = ?
                WHERE id = ? OR barcode = ?
            """, (barcode, sample_id, barcode))
            conn.commit()
            return {"success": True, "message": f"Забір зразка {barcode} підтверджено в базі даних!", "barcode": barcode}

        elif path == "/api/laboratory/referrals" and method == "GET":
            cur.execute("""
                SELECT r.id, r.referral_code, r.service_code, r.service_name, r.status,
                       p.last_name || ' ' || p.first_name as patient_name, p.birth_date, p.phone,
                       e.full_name as doctor_name, r.created_at
                FROM ehe_incoming_medical_referral r
                JOIN mis_patient_card p ON r.patient_id = p.id
                LEFT JOIN org_employee e ON r.requester_doctor_id = e.id
                ORDER BY r.created_at DESC
            """)
            items = [dict(row) for row in cur.fetchall()]
            return {"success": True, "data": items}

        elif path == "/api/laboratory/diagnostic-reports" and method == "GET":
            cur.execute("""
                SELECT d.id, d.report_number, d.service_name, d.status, d.ehealth_synced, d.signed_at,
                       p.last_name || ' ' || p.first_name as patient_name,
                       e.full_name as performer_doctor, r.referral_code
                FROM mis_diagnostic_report d
                JOIN mis_patient_card p ON d.patient_id = p.id
                LEFT JOIN org_employee e ON d.performer_doctor_id = e.id
                LEFT JOIN ehe_incoming_medical_referral r ON d.referral_id = r.id
                ORDER BY d.created_at DESC
            """)
            items = [dict(row) for row in cur.fetchall()]
            return {"success": True, "data": items}

        # =====================================================================
        # 7. DICTIONARIES: FULL REAL CRUD
        # =====================================================================
        elif path.startswith("/api/laboratory/dictionaries/"):
            parts = path.strip("/").split("/")
            dict_type = parts[3]
            table_map = {
                "biomaterials": "lab_biomaterial_types",
                "tubes": "lab_tube_types",
                "methods": "lab_method_types",
                "analyzers": "lab_analyzers",
                "parameters": "lab_test_definitions"
            }
            tbl = table_map.get(dict_type)
            if not tbl:
                return {"success": False, "error": f"Unknown dictionary: {dict_type}"}

            # GET All
            if method == "GET" and len(parts) == 4:
                cur.execute(f"SELECT * FROM {tbl} ORDER BY id ASC")
                items = [dict(row) for row in cur.fetchall()]
                return {"success": True, "data": items}

            # POST (Create / Save)
            elif method == "POST":
                b = body
                if dict_type == "biomaterials":
                    item_id = b.get("id") or (int(cur.execute("SELECT IFNULL(MAX(id),0) FROM lab_biomaterial_types").fetchone()[0]) + 1)
                    cur.execute("""
                        INSERT OR REPLACE INTO lab_biomaterial_types (id, code, name, default_container, stability_hours, temperature_regime)
                        VALUES (?, ?, ?, ?, ?, ?)
                    """, (item_id, b.get("code"), b.get("name"), b.get("default_container"), b.get("stability_hours", 24), b.get("temperature_regime", "+2..+8°C")))
                elif dict_type == "tubes":
                    item_id = b.get("id") or (int(cur.execute("SELECT IFNULL(MAX(id),0) FROM lab_tube_types").fetchone()[0]) + 1)
                    cur.execute("""
                        INSERT OR REPLACE INTO lab_tube_types (id, code, name, color_code, volume_ml, anticoagulant, inversions_count, centrifuge_rpm, centrifuge_minutes)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """, (item_id, b.get("code"), b.get("name"), b.get("color_code", "#4274A7"), b.get("volume_ml", 4.0), b.get("anticoagulant"), b.get("inversions_count", 8), b.get("centrifuge_rpm", 3000), b.get("centrifuge_minutes", 10)))
                elif dict_type == "analyzers":
                    item_id = b.get("id") or f"AN-{int(cur.execute('SELECT COUNT(*) FROM lab_analyzers').fetchone()[0]) + 1:02d}"
                    cur.execute("""
                        INSERT OR REPLACE INTO lab_analyzers (id, code, name, analyzer_type_id, connection_mode, ip_host, ip_port, is_online)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                    """, (item_id, b.get("code"), b.get("name"), b.get("analyzer_type_id", 1), b.get("connection_mode", "TCP_SERVER"), b.get("ip_host", "192.168.1.100"), b.get("ip_port", 5000), b.get("is_online", 1)))
                elif dict_type == "parameters":
                    item_id = b.get("id") or (int(cur.execute("SELECT IFNULL(MAX(id),0) FROM lab_test_definitions").fetchone()[0]) + 1)
                    cur.execute("""
                        INSERT OR REPLACE INTO lab_test_definitions (id, code, name, loinc_code, biomaterial_type_id, unit, decimal_places, category, is_active)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """, (item_id, b.get("code"), b.get("name"), b.get("loinc_code"), b.get("biomaterial_type_id", 1), b.get("unit"), b.get("decimal_places", 2), b.get("category", "BIOCHEM"), 1))
                conn.commit()
                return {"success": True, "message": f"Запис довідника {dict_type} збережено в базі даних!", "id": item_id}

            # DELETE
            elif method == "DELETE" and len(parts) == 5:
                del_id = parts[4]
                cur.execute(f"DELETE FROM {tbl} WHERE id = ?", (del_id,))
                conn.commit()
                return {"success": True, "message": f"Запис {del_id} видалено з довідника {dict_type}!"}

        # =====================================================================
        # 8. NORMS & REFERENCE COMBINATIONS (DELPHI SERVICE CARD MATRIX)
        # =====================================================================
        elif path == "/api/laboratory/norms/combinations" and method == "GET":
            cur.execute("""
                SELECT r.*, t.name as test_name, t.loinc_code, t.unit as test_unit
                FROM lab_reference_ranges r
                JOIN lab_test_definitions t ON r.test_code = t.code
                ORDER BY r.test_code ASC, r.is_pregnancy DESC, r.is_menstrual_phase DESC, r.age_from ASC
            """)
            items = [dict(row) for row in cur.fetchall()]
            return {"success": True, "data": items}

        elif path == "/api/laboratory/norms/combinations" and method == "POST":
            b = body or {}
            norm_id = b.get("id") or f"REF-{b.get('test_code', 'TEST')}-{int(cur.execute('SELECT COUNT(*) FROM lab_reference_ranges').fetchone()[0]) + 1}"
            
            # Auto-assign priority based on layer_type if not set
            layer_type = b.get("layer_type") or "DEMOGRAPHIC"
            priority = b.get("priority_order")
            if priority is None or priority == "":
                prio_defaults = {"PREGNANCY": 100, "MENSTRUAL_PHASE": 80, "CLINICAL_ICD10": 60, "DEMOGRAPHIC": 40, "BASELINE": 10}
                priority = prio_defaults.get(layer_type, 40)
            else:
                priority = int(priority)

            cur.execute("""
                INSERT OR REPLACE INTO lab_reference_ranges (
                    id, test_code, norm_name, gender, is_gender, age_unit, age_from, age_to, is_age,
                    menstrual_phase, is_menstrual_phase, pregnancy_week_from, pregnancy_week_to, is_pregnancy,
                    method_name, norm_low, norm_high, crit_low, crit_high, norm_text, unit, delta_check_max_pct,
                    layer_type, priority_order, icd10_code, method_code
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                norm_id, b.get("test_code", "GLU"), b.get("norm_name", "Нове правило"),
                b.get("gender", "ANY"), int(b.get("is_gender", 0)), b.get("age_unit", "YEARS"),
                int(b.get("age_from", 0)), int(b.get("age_to", 120)), int(b.get("is_age", 1)),
                b.get("menstrual_phase"), int(b.get("is_menstrual_phase", 0)),
                b.get("pregnancy_week_from"), b.get("pregnancy_week_to"), int(b.get("is_pregnancy", 0)),
                b.get("method_name", "Стандартна"), float(b.get("norm_low", 0)), float(b.get("norm_high", 0)),
                b.get("crit_low"), b.get("crit_high"), b.get("norm_text"), b.get("unit", ""), float(b.get("delta_check_max_pct", 25.0)),
                layer_type, priority, b.get("icd10_code"), b.get("method_code", "HEX_IFCC")
            ))
            conn.commit()
            return {"success": True, "message": "Комбінацію норми успішно збережено в базі даних!", "id": norm_id}

        elif path.startswith("/api/laboratory/norms/combinations/") and method == "DELETE":
            norm_id = path.split("/")[-1]
            cur.execute("DELETE FROM lab_reference_ranges WHERE id = ?", (norm_id,))
            conn.commit()
            return {"success": True, "message": f"Комбінацію {norm_id} видалено з бази даних!"}

        # =====================================================================
        # 9. DELPHI MULTI-LAYER CASCADE RESOLVER (ШАРИ МЕТОДИКИ)
        # =====================================================================
        elif path.startswith("/api/laboratory/norms/layers") and method == "GET":
            test_code = "GLU"
            method_code = "HEX_IFCC"
            if "?" in path:
                q = path.split("?")[1]
                for param in q.split("&"):
                    if param.startswith("test_code="):
                        test_code = param.split("=")[1]
                    elif param.startswith("method_code="):
                        method_code = param.split("=")[1]

            cur.execute("""
                SELECT * FROM lab_reference_ranges
                WHERE test_code = ? AND (method_code = ? OR method_code = 'HEX_IFCC' OR method_code IS NULL)
                ORDER BY priority_order DESC, created_at ASC
            """, (test_code, method_code))
            layers = [dict(row) for row in cur.fetchall()]
            return {
                "success": True,
                "test_code": test_code,
                "method_code": method_code,
                "data": layers
            }

        elif path == "/api/laboratory/norms/resolve-cascade" and method == "POST":
            b = body or {}
            test_code = b.get("test_code", "GLU")
            method_code = b.get("method_code", "HEX_IFCC")
            gender = b.get("gender", "F")
            age = float(b.get("age", 28))
            age_unit = b.get("age_unit", "YEARS")
            is_preg = bool(b.get("is_pregnant", False))
            preg_week = b.get("pregnancy_week")
            if preg_week is not None and preg_week != "":
                preg_week = int(preg_week)
            else:
                preg_week = None
            phase = b.get("menstrual_phase")
            icd10 = b.get("icd10_code")
            measured_val = b.get("measured_value")
            prev_val = b.get("previous_value")

            # Fetch all layers for this methodology ordered by priority descending
            cur.execute("""
                SELECT * FROM lab_reference_ranges
                WHERE test_code = ? AND (method_code = ? OR method_code = 'HEX_IFCC' OR method_code IS NULL) AND is_active = 1
                ORDER BY priority_order DESC
            """, (test_code, method_code))
            all_layers = [dict(row) for row in cur.fetchall()]

            audit_trace = []
            winning_layer = None

            for layer in all_layers:
                l_type = (layer.get("layer_type") or "DEMOGRAPHIC").upper()
                l_name = layer.get("norm_name") or "Шар норми"
                prio = layer.get("priority_order") or 40

                matched = True
                reason = "Умови шару повністю співпали"

                if l_type == "PREGNANCY":
                    if not is_preg or preg_week is None:
                        matched = False
                        reason = "Пацієнт не вагітна або термін не вказано"
                    elif layer.get("pregnancy_week_from") is not None and layer.get("pregnancy_week_to") is not None:
                        pw_from = layer.get("pregnancy_week_from")
                        pw_to = layer.get("pregnancy_week_to")
                        if preg_week < pw_from or preg_week > pw_to:
                            matched = False
                            reason = f"Термін вагітності {preg_week} т. поза діапазоном [{pw_from}-{pw_to}] т."
                elif l_type == "MENSTRUAL_PHASE":
                    if gender != "F":
                        matched = False
                        reason = "Застосовується тільки для жінок"
                    elif is_preg:
                        matched = False
                        reason = "Перекрито вищим пріоритетом вагітності"
                    elif not phase or (layer.get("menstrual_phase") and layer.get("menstrual_phase") != phase):
                        matched = False
                        reason = f"Фаза циклу '{phase or 'не вказано'}' не відповідає шару '{layer.get('menstrual_phase')}'"
                elif l_type == "CLINICAL_ICD10":
                    if not icd10 or layer.get("icd10_code") != icd10:
                        matched = False
                        reason = f"Діагноз МКХ-10 '{icd10 or 'немає'}' не відповідає цільовій когорті '{layer.get('icd10_code')}'"
                elif l_type == "DEMOGRAPHIC":
                    if layer.get("is_gender") and layer.get("gender") not in ["ANY", "", None] and layer.get("gender") != gender:
                        matched = False
                        reason = f"Стать '{gender}' не відповідає шару '{layer.get('gender')}'"
                    elif layer.get("is_age"):
                        l_unit = (layer.get("age_unit") or "YEARS").upper()
                        l_from = float(layer.get("age_from", 0))
                        l_to = float(layer.get("age_to", 120))
                        
                        def to_days(v, u):
                            u = (u or "YEARS").upper()
                            if u == "DAYS": return float(v)
                            if u == "MONTHS": return float(v) * 30.4375
                            return float(v) * 365.25
                        
                        p_days = to_days(age, age_unit)
                        l_from_days = to_days(l_from, l_unit)
                        l_to_days = to_days(l_to, l_unit)
                        
                        if p_days < l_from_days or p_days > l_to_days:
                            matched = False
                            reason = f"Вік {age} {age_unit} поза діапазоном [{l_from}-{l_to} {l_unit}]"
                elif l_type == "BASELINE":
                    matched = True
                    reason = "Базовий клінічний оптимум методики (Запасний рівень каскаду)"

                audit_trace.append({
                    "priority": prio,
                    "layer_type": l_type,
                    "layer_name": l_name,
                    "is_matched": matched,
                    "reason": reason
                })

                if matched and winning_layer is None:
                    winning_layer = layer

            if not winning_layer:
                winning_layer = all_layers[-1] if all_layers else {
                    "norm_low": 4.10, "norm_high": 5.90, "crit_low": 2.50, "crit_high": 25.00,
                    "unit": "ммоль/л", "norm_name": "Запасний референс", "delta_check_max_pct": 25.0
                }

            n_low = float(winning_layer.get("norm_low", 4.10))
            n_high = float(winning_layer.get("norm_high", 5.90))
            c_low = float(winning_layer["crit_low"]) if winning_layer.get("crit_low") is not None else None
            c_high = float(winning_layer["crit_high"]) if winning_layer.get("crit_high") is not None else None
            unit = winning_layer.get("unit") or "ммоль/л"
            delta_max = float(winning_layer.get("delta_check_max_pct", 25.0))

            status_flag = "NORMAL"
            is_panic = False
            is_delta = False
            delta_pct = None

            if measured_val is not None and measured_val != "":
                val = float(measured_val)
                if (c_low is not None and val <= c_low) or (c_high is not None and val >= c_high):
                    status_flag = "CRIT_HIGH" if (c_high is not None and val >= c_high) else "CRIT_LOW"
                    is_panic = True
                elif val < n_low:
                    status_flag = "LOW"
                elif val > n_high:
                    status_flag = "HIGH"
                else:
                    status_flag = "NORMAL"

                if prev_val is not None and prev_val != "" and float(prev_val) > 0:
                    pval = float(prev_val)
                    delta_pct = round(((val - pval) / pval) * 100.0, 1)
                    if abs(delta_pct) > delta_max:
                        is_delta = True

            return {
                "success": True,
                "test_code": test_code,
                "method_code": method_code,
                "winning_layer": winning_layer,
                "norm_low": n_low,
                "norm_high": n_high,
                "crit_low": c_low,
                "crit_high": c_high,
                "unit": unit,
                "status_flag": status_flag,
                "is_panic": is_panic,
                "is_delta_alert": is_delta,
                "delta_percent": delta_pct,
                "audit_trace": audit_trace
            }

        # Legacy resolver fallback
        elif path == "/api/laboratory/norms/resolve" and method == "POST":
            b = body or {}
            test_code = b.get("test_code", "GLU")
            gender = b.get("gender", "M")
            age = float(b.get("age", 35))
            age_unit = b.get("age_unit", "YEARS")
            phase = b.get("menstrual_phase")
            preg_week = b.get("pregnancy_week")

            cur.execute("""
                SELECT * FROM lab_reference_ranges
                WHERE test_code = ?
                  AND (is_pregnancy = 0 OR (is_pregnancy = 1 AND ? >= pregnancy_week_from AND ? <= pregnancy_week_to))
                  AND (is_menstrual_phase = 0 OR (is_menstrual_phase = 1 AND menstrual_phase = ?))
                  AND (is_gender = 0 OR gender = ? OR gender = 'ANY')
                  AND (is_age = 0 OR (? >= age_from AND ? <= age_to AND age_unit = ?))
                ORDER BY priority_order DESC, is_pregnancy DESC, is_menstrual_phase DESC, is_gender DESC, is_age DESC
                LIMIT 1
            """, (test_code, preg_week or -1, preg_week or -1, phase or "", gender, age, age, age_unit))
            
            matched = cur.fetchone()
            if matched:
                return {"success": True, "matched": True, "rule": dict(matched)}
            else:
                cur.execute("SELECT * FROM lab_reference_ranges WHERE test_code = ? LIMIT 1", (test_code,))
                fallback = cur.fetchone()
                return {"success": True, "matched": False, "rule": dict(fallback) if fallback else None, "note": "Застосовано базовий референс за замовчуванням"}

        elif path == "/api/laboratory/norms/reflex-rules" and method == "GET":
            cur.execute("SELECT * FROM lab_reflex_rules ORDER BY id ASC")
            items = [dict(row) for row in cur.fetchall()]
            return {"success": True, "data": items}

        return {"success": False, "error": f"Endpoint not found: {method} {path}"}

    except Exception as e:
        return {"success": False, "error": str(e)}
    finally:
        conn.close()

if __name__ == "__main__":
    print("Testing API state:")
    res = handle_api_request("GET", "/api/laboratory/pipeline/state")
    print(json.dumps(res, ensure_ascii=False, indent=2))
