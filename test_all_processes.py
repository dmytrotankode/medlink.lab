# -*- coding: utf-8 -*-
"""
test_all_processes.py
Comprehensive test suite for all MedLink LIS 3.0 processes:
1. Pipeline End-to-End Progression (Steps 1 to 7)
2. Worklist & Auto-Verification
3. QC Lockout Resolution
4. Delphi Reference Norms Combinations Matrix & Resolver
5. Reflex Rules Catalog
6. Clinical Dictionaries
7. Diagnostics Report Sync with eHealth/MedLink evomis
"""

import urllib.request
import json
import sys

BASE_URL = "http://127.0.0.1:8088"

def make_req(method, path, data=None):
    url = BASE_URL + path
    headers = {"Content-Type": "application/json; charset=utf-8"}
    body = json.dumps(data).encode("utf-8") if data else None
    req = urllib.request.Request(url, data=body, headers=headers, method=method)
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode("utf-8"))

def run_tests():
    passed = 0
    total = 0

    def check(name, condition, extra=""):
        nonlocal passed, total
        total += 1
        if condition:
            passed += 1
            print(f"[PASS] {name} {extra}")
        else:
            print(f"[FAIL] {name} {extra}")

    print("=== 1. ТЕСТУВАННЯ НАСКРІЗНОГО ПРОЦЕСУ (PIPELINE) ===")
    # Reset
    res = make_req("POST", "/api/laboratory/pipeline/reset")
    check("Скидання процесу в початковий стан", res.get("success") is True)

    # Initial state
    st = make_req("GET", "/api/laboratory/pipeline/state")
    check("Отримання початкового стану процесу", st.get("success") is True)
    check("Крок 1 активний (Нове е-направлення)", st.get("currentStep") == 1)

    # Step 2: Phlebotomy
    res = make_req("POST", "/api/laboratory/pipeline/advance", {"targetStep": 2})
    check("Перехід на Крок 2 (Забір біоматеріалу & Штрихкод)", res.get("success") is True)
    st = make_req("GET", "/api/laboratory/pipeline/state")
    check("Статус замовлення в базі: COLLECTED", st.get("orderSample", {}).get("sample_status") == "COLLECTED")

    # Step 3: Logistics
    res = make_req("POST", "/api/laboratory/pipeline/advance", {"targetStep": 3})
    check("Перехід на Крок 3 (Логістика & Холодовий ланцюг)", res.get("success") is True)
    st = make_req("GET", "/api/laboratory/pipeline/state")
    check("Статус замовлення в базі: IN_TRANSIT", st.get("orderSample", {}).get("sample_status") == "IN_TRANSIT")

    # Step 4: Reception at CDL
    res = make_req("POST", "/api/laboratory/pipeline/advance", {"targetStep": 4})
    check("Перехід на Крок 4 (Прийом у лабораторію & Бракераж)", res.get("success") is True)
    st = make_req("GET", "/api/laboratory/pipeline/state")
    check("Статус зразка в базі: RECEIVED", st.get("orderSample", {}).get("sample_status") == "RECEIVED")

    # Step 5: Analyzer measurement
    res = make_req("POST", "/api/laboratory/pipeline/advance", {"targetStep": 5})
    check("Перехід на Крок 5 (Аналізатор передав вимірювання)", res.get("success") is True)
    st = make_req("GET", "/api/laboratory/pipeline/state")
    res_items = st.get("results", [])
    has_crit = any(r.get("flag") == "CRIT_HIGH" for r in res_items)
    check("Виявлено критичне значення (Паніка CITO)", has_crit)

    # Step 6: Doctor verification & KEP
    res = make_req("POST", "/api/laboratory/pipeline/advance", {"targetStep": 6})
    check("Перехід на Крок 6 (Верифікація лікарем & КЕП)", res.get("success") is True)
    st = make_req("GET", "/api/laboratory/pipeline/state")
    check("Створено DiagnosticReport (FINAL) з підписом", st.get("diagnosticReport", {}).get("status") == "FINAL")
    check("Е-направлення погашено в ЕСОЗ", st.get("referral", {}).get("status") == "COMPLETED")

    # Step 7: Patient portal
    res = make_req("POST", "/api/laboratory/pipeline/advance", {"targetStep": 7})
    check("Перехід на Крок 7 (Публікація в Кабінеті пацієнта)", res.get("success") is True)

    print("\n=== 2. ТЕСТУВАННЯ РОБОЧОГО СПИСКУ ТА АВТОВЕРИФІКАЦІЇ ===")
    wl = make_req("GET", "/api/laboratory/worklist")
    check("Отримання робочого журналу досліджень", wl.get("success") is True and len(wl.get("data", [])) > 0, f"({len(wl.get('data', []))} записів)")
    av = make_req("POST", "/api/laboratory/worklist/autoverify")
    check("Виконання правила автоверифікації норм", av.get("success") is True, f"({av.get('message')})")

    print("\n=== 3. ТЕСТУВАННЯ ВНУТРІШНЬОГО КОНТРОЛЮ ЯКОСТІ (QC LOCKOUT) ===")
    lck = make_req("POST", "/api/laboratory/qc/resolve-lockout", {"action": "Калібрування та промивка", "comment": "Контроль відновлено"})
    check("Зняття блокування аналізатора (Lockout) з аудитом", lck.get("success") is True)

    print("\n=== 4. ТЕСТУВАННЯ БАГАТОВИМІРНОЇ МАТРИЦІ РЕФЕРЕНТІВ DELPHI ===")
    norms = make_req("GET", "/api/laboratory/norms/combinations")
    check("Отримання матриці комбінацій норм", norms.get("success") is True and len(norms.get("data", [])) > 0, f"({len(norms.get('data', []))} правил)")

    # Test Resolver for Adult Male Glucose
    r_male = make_req("POST", "/api/laboratory/norms/resolve", {"test_code": "GLU", "gender": "M", "age": 42})
    check("Резолвер норми: Чоловік 42 роки (Глюкоза)", r_male.get("success") is True and r_male.get("rule", {}).get("norm_low") == 4.1)

    # Test Resolver for Pregnancy 2nd Trimester
    r_preg = make_req("POST", "/api/laboratory/norms/resolve", {"test_code": "AFP", "gender": "F", "age": 28, "pregnancy_week": 18})
    check("Резолвер норми: Вагітність 18 тижнів (АФП)", r_preg.get("success") is True)

    # Test Resolver for Child
    r_child = make_req("POST", "/api/laboratory/norms/resolve", {"test_code": "GLU", "gender": "M", "age": 5})
    check("Резолвер норми: Дитина 5 років (Глюкоза)", r_child.get("success") is True and r_child.get("rule", {}).get("norm_low") == 3.3)

    # Add new combination rule
    new_rule = {
        "test_code": "HGB",
        "norm_name": "Гемоглобін новонароджені 1-3 дні",
        "gender": "ANY",
        "is_gender": 0,
        "age_unit": "DAYS",
        "age_from": 1,
        "age_to": 3,
        "is_age": 1,
        "norm_low": 145.0,
        "norm_high": 225.0,
        "crit_low": 100.0,
        "crit_high": 250.0,
        "unit": "г/л"
    }
    add_r = make_req("POST", "/api/laboratory/norms/combinations", new_rule)
    check("Збереження нової вікової комбінації в базу", add_r.get("success") is True)

    print("\n=== 5. ТЕСТУВАННЯ REFLEX-ПРАВИЛ ТА ДОВІДНИКІВ ===")
    refl = make_req("GET", "/api/laboratory/norms/reflex-rules")
    check("Отримання каталогу reflex-правил", refl.get("success") is True and len(refl.get("data", [])) > 0)

    for d in ["biomaterials", "tubes", "methods", "analyzers", "parameters"]:
        dict_res = make_req("GET", f"/api/laboratory/dictionaries/{d}")
        check(f"Довідник {d}", dict_res.get("success") is True and len(dict_res.get("data", [])) > 0, f"({len(dict_res.get('data', []))} позицій)")

    print(f"\n==========================================")
    print(f"ПІДСУМОК ТЕСТУВАННЯ: {passed}/{total} тестів успішно пройдено! ({(passed/total)*100:.1f}%)")
    print(f"==========================================")
    return passed == total

if __name__ == "__main__":
    success = run_tests()
    sys.exit(0 if success else 1)
