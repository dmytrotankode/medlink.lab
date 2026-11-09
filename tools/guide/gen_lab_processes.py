# Генератор бандла інструкцій ЛІС: python tools/guide/gen_lab_processes.py -> src/MedLink.Guide/Seed/lab_processes.json
# Селектори — data-testid зі src/MedLink.LIS.Web (див. docs/GUIDE_MODULE.md). Редагуйте тут і перегенеруйте
# (сід ідемпотентний за кодами; нова версія публікується лише коли вміст змінився).
import json
import pathlib

FOLDERS = [
    {"code": "start", "caption": "Початок роботи", "icon": "🚀", "sortOrder": 10},
    {"code": "preanalytics", "caption": "Преаналітика", "icon": "🩸", "sortOrder": 30},
    {"code": "analytics", "caption": "Аналітика та результати", "icon": "🔬", "sortOrder": 40},
    {"code": "validation", "caption": "Верифікація та критичні значення", "icon": "✅", "sortOrder": 50},
    {"code": "mis", "caption": "Інтеграція з МІС", "icon": "🔗", "sortOrder": 60},
    {"code": "quality", "caption": "Контроль якості", "icon": "📈", "sortOrder": 70},
]


def S(*items):
    return [{"type": t, "value": v, "weight": w} for t, v, w in items]


def A(code, caption, strategies, route="*"):
    return {"code": code, "caption": caption, "routePattern": route, "strategies": strategies}


def tid(code, caption, testid, prefix=False):
    sel = ('[data-testid^="%s"]' if prefix else '[data-testid="%s"]') % testid
    return A(code, caption, S(("testid", sel, 100)))


def menu(code, caption, path, text):
    return A(code, caption, S(("css", 'a[href$="%s"]' % path, 90), ("text", "div|" + text, 60)))


def step(title, body, route, anchor=None, type="info", expect="next", value="", mode="show",
         highlight="element", parent=0, placement="auto", optional=False):
    s = {"stepType": type, "title": title, "body": body, "route": route, "highlight": highlight,
         "parentLevels": parent, "placement": placement, "expect": expect, "inputValue": value,
         "inputMode": mode, "optional": optional}
    if anchor:
        s["anchor"] = anchor
    return s


def scenario(code, folder, caption, desc, route, minutes, order, steps, variables=None, tags=None):
    return {"code": code, "folderCode": folder, "caption": caption, "description": desc,
            "startRoute": route, "requiredRoles": [], "tags": tags or [], "estMinutes": minutes,
            "sortOrder": order, "publish": True, "variables": variables or [], "steps": steps}


def var(name, caption, value):
    return {"name": name, "caption": caption, "kind": "fixed", "defaultValue": value}


SC = []
D = "/laboratory/dashboard"

# 1. Навігація
SC.append(scenario("lab.nav.menu", "start", "Навігація: бічне меню ЛІС",
    "Як знайти потрібний модуль: групи процесів, довідників, інтеграцій.", D, 1, 5, [
        step("Меню модулів лабораторії", "Усі функції ЛІС згруповані в бічному меню: процеси лабораторії, довідники, інтеграції та адміністрування. Пункти з цифрами показують кількість завдань.", D, tid("lab.menu.drawer", "Бічне меню ЛІС", "labMenuDrawer"), placement="right"),
        step("Відкриємо реєстрацію направлень", "Натисніть пункт «Реєстрація направлень» — це черга замовлень лабораторії.", D, menu("lab.menu.orders", "Меню: Реєстрація направлень", "/laboratory/orders", "Реєстрація направлень"), type="click", expect="click", placement="right"),
        step("Ви в черзі замовлень", "Тут реєструються нові направлення, шукаються замовлення за № або штрихкодом, фільтруються за статусом, датою та відділенням. Повернутися на дашборд можна кліком по логотипу.", "/laboratory/orders", tid("lab.nav.orders-page", "Черга замовлень", "ordersPage")),
    ], tags=["меню", "навігація"]))

# 2. Забір біоматеріалу
PH = "/laboratory/phlebotomy"
SC.append(scenario("lab.preanalytics.collect", "preanalytics", "Забір біоматеріалу за чек-листом",
    "Пункт забору: сканування штрихкоду пробірки, чек-лист безпеки, підтвердження забору, друк етикетки.", PH, 3, 10, [
        step("Пункт забору біоматеріалу", "Тут медсестра відкриває пробірку за штрихкодом і фіксує забір. Робіть це біля пацієнта.", PH, tid("lab.phlebotomy.page", "Сторінка пункту забору", "phlebotomyPage"), highlight="none"),
        step("Скануйте штрихкод", "Наведіть сканер на етикетку пробірки або введіть штрихкод вручну та натисніть Enter.", PH, tid("lab.phlebotomy.scan", "Поле сканування", "scanInput"), type="input", value="{{barcode}}"),
        step("Картка пробірки", "Перевірте пацієнта, тип пробірки, статус «Очікує забору» та план пробірок замовлення.", PH, tid("lab.phlebotomy.sample-card", "Картка пробірки", "sampleCard")),
        step("Забір за чек-листом", "Натисніть «Забір за чек-листом» — відкриється перелік обов'язкових перевірок.", PH, tid("lab.phlebotomy.collect-btn", "Кнопка «Забір за чек-листом»", "collectBtn"), type="click", expect="click"),
        step("Чек-лист безпеки", "Відкрито перелік перевірок. Без усіх відміток підтвердити забір неможливо.", PH, tid("lab.phlebotomy.collect-dialog", "Діалог забору", "collectDialog"), placement="left", optional=True),
        step("Ідентифікація пацієнта", "Назвіть пацієнта та звірте ПІБ і дату народження з направленням — позначте пункт.", PH, tid("lab.phlebotomy.chk-id", "Чек: ідентифікація", "chk-id"), placement="left", optional=True),
        step("Натще", "Уточніть, чи дотримано умову «натще», якщо її вимагає аналіз.", PH, tid("lab.phlebotomy.chk-fasting", "Чек: натще", "chk-fasting"), placement="left", optional=True),
        step("Відповідність направленню", "Переконайтеся, що пробірка відповідає призначеним дослідженням.", PH, tid("lab.phlebotomy.chk-order", "Чек: направлення", "chk-order"), placement="left", optional=True),
        step("Перемішування", "Після забору обережно перемішайте пробірку з реагентом, дотримуючись порядку пробірок.", PH, tid("lab.phlebotomy.chk-mixing", "Чек: перемішування", "chk-mixing"), placement="left", optional=True),
        step("Підтвердіть забір", "Коли всі пункти позначені, натисніть «Підтвердити забір». Статус пробірки зміниться на «Зібрано», час і виконавець збережуться в журналі.", PH, tid("lab.phlebotomy.collect-confirm", "Підтвердити забір", "collectConfirm"), type="click", expect="click", optional=True, placement="left"),
        step("Етикетка та список проб", "Надрукуйте етикетку кнопкою «Друк етикетки», а нижче у таблиці бачите всі проби поточного замовлення.", PH, tid("lab.phlebotomy.samples-table", "Таблиця проб", "samplesTable"), optional=True),
    ], variables=[var("barcode", "Штрихкод для прикладу", "20260001")], tags=["забір", "проби", "штрихкод"]))

# 3. Робочий стіл
WS = "/laboratory/workstation"
SC.append(scenario("lab.analytics.results", "analytics", "Введення результатів на робочому столі лаборанта",
    "Робочий лист: пошук, вибір рядка, введення результату, коментар, автоверифікація.", WS, 4, 10, [
        step("Робочий стіл лаборанта", "Головний екран лаборанта: робочий лист із показниками, які чекають на результат. Дані від аналізаторів надходять автоматично.", WS, tid("lab.ws.page", "Робочий стіл", "workstationPage"), highlight="none"),
        step("Пошук у робочому листі", "Шукайте за штрихкодом, пацієнтом, тестом або № замовлення. Фільтр «Підрозділ» звужує список до вашого відділення.", WS, tid("lab.ws.search", "Пошук", "wsSearch"), type="input", value="{{search}}"),
        step("Таблиця робочого листа", "CITO-проби підсвічено, критичні значення виділено червоним. Клік по рядку відкриває деталі, подвійний клік — швидке введення результату.", WS, tid("lab.ws.table", "Робочий лист", "worklistTable"), placement="top"),
        step("Виберіть рядок", "Клікніть будь-який рядок робочого листа, щоб побачити деталі показника.", WS, tid("lab.ws.row", "Рядок робочого листа", "wl-row-", prefix=True), type="click", expect="click", optional=True),
        step("Ввести або змінити результат", "Натисніть «Ввести / змінити» у панелі деталей.", WS, tid("lab.ws.detail-edit", "Ввести / змінити", "detailEdit"), type="click", expect="click", optional=True),
        step("Редактор результату", "Введіть число, виберіть значення зі списку або текст — залежно від типу показника. Система одразу порівняє з нормою та виставить прапорець (↑↓, критичний).", WS, tid("lab.ws.result-editor", "Редактор результату", "resultEditor"), optional=True, placement="left"),
        step("Коментар оператора", "За потреби додайте коментар — він потрапить у журнал і на бланк.", WS, tid("lab.ws.result-comment", "Коментар оператора", "resultComment"), optional=True, placement="left"),
        step("Збережіть результат", "Натисніть «Зберегти». Результат отримає статус RESULTED і піде на верифікацію.", WS, tid("lab.ws.result-save", "Зберегти", "resultSave"), type="click", expect="click", optional=True),
        step("Автоверифікація", "Кнопка «Автоверифікація» автоматично підтверджує результати в межах норми за правилами лабораторії (delta-check, ВКЯ, діапазони).", WS, tid("lab.ws.autoverify", "Автоверифікація", "wsAutoverify")),
    ], variables=[var("search", "Що шукати", "Мельник")], tags=["результати", "робочий лист"]))

# 4. Верифікація
VL = "/laboratory/validation"
SC.append(scenario("lab.validation.verify", "validation", "Верифікація результатів",
    "Підтвердження результатів лікарем-лаборантом: порівняння з попередніми, пакетна верифікація.", VL, 3, 10, [
        step("Валідація та критичні значення", "Тут лікар-лаборант підтверджує результати перед випуском бланка. Три вкладки: на верифікацію, критичні без повідомлення, журнал повідомлень.", VL, tid("lab.validation.page", "Сторінка валідації", "validationPage"), highlight="none"),
        step("Вкладка «На верифікацію»", "Результати, що очікують підтвердження. Червоним виділено критичні.", VL, tid("lab.validation.tab-review", "Вкладка «На верифікацію»", "tab-review")),
        step("Верифікація одного результату", "Кнопка з іконкою відкриває порівняння з попередніми результатами пацієнта (delta-check) — перевірте динаміку перед підтвердженням.", VL, tid("lab.validation.verify-row", "Верифікувати рядок", "verify-", prefix=True), optional=True),
        step("Діалог верифікації", "Відкривається порівняння з попередніми результатами пацієнта (delta-check).", VL, tid("lab.validation.dialog", "Діалог верифікації", "verifyDialog"), optional=True, placement="left"),
        step("Коментар", "За потреби залиште коментар лікаря-лаборанта.", VL, tid("lab.validation.comment", "Коментар", "verifyComment"), optional=True, placement="left"),
        step("Підтвердіть", "Натисніть «Верифікувати» — результат стане доступним для випуску бланка.", VL, tid("lab.validation.confirm", "Підтвердити верифікацію", "verifyConfirm"), type="click", expect="click", optional=True, placement="left"),
        step("Пакетна верифікація", "Відмітьте кілька рядків чекбоксами та натисніть «Верифікувати обрані» — так швидше за великих обсягів. Критичні значення потребують окремого рішення.", VL, tid("lab.validation.verify-selected", "Верифікувати обрані", "verifySelected")),
    ], tags=["верифікація", "delta-check"]))

# 5. Критичні значення
PV = "/laboratory/validation?tab=panic"
SC.append(scenario("lab.validation.panic", "validation", "Реєстрація дзвінка про критичний результат",
    "Критичне значення: обов'язкове повідомлення лікаря та підтвердження read-back.", PV, 3, 20, [
        step("Критичні без повідомлення", "Критичні значення, про які лікаря ще не повідомлено. Поки повідомлення не зареєстровано, бланк не випускається.", PV, tid("lab.validation.tab-panic", "Вкладка «Критичні без повідомлення»", "tab-panic")),
        step("Зареєструйте дзвінок", "Телефонуйте лікарю та натисніть «Дзвінок» у відповідному рядку.", PV, tid("lab.validation.call-btn", "Кнопка «Дзвінок»", "call-", prefix=True), type="click", expect="click", optional=True),
        step("ПІБ лікаря", "Вкажіть, кому саме повідомлено критичний результат.", PV, tid("lab.panic.doctor", "ПІБ лікаря", "panicDoctor"), type="input", value="{{doctor}}", optional=True, placement="left"),
        step("Телефон", "Телефон, за яким зв'язалися.", PV, tid("lab.panic.phone", "Телефон", "panicPhone"), type="input", value="{{phone}}", optional=True, placement="left"),
        step("Read-back", "Позначте, що лікар повторив значення голосом (read-back) — це вимога безпеки.", PV, tid("lab.panic.readback", "Read-back", "panicReadback"), type="click", expect="click", optional=True, placement="left"),
        step("Збережіть", "Натисніть «Зареєструвати дзвінок». Запис потрапить у журнал дзвінків з часом і виконавцем.", PV, tid("lab.panic.save", "Зареєструвати дзвінок", "panicSave"), type="click", expect="click", optional=True, placement="left"),
    ], variables=[var("doctor", "Лікар", "Іваненко Петро Олексійович"), var("phone", "Телефон", "+380501234567")], tags=["критичні значення", "критичні"]))

# 6. МІС
RF = "/laboratory/refs"
SC.append(scenario("lab.mis.distribute", "mis", "Розподіл вхідного направлення з МІС",
    "Прийом направлення з МІС, вибір аналізаторів, постановка в черги.", RF, 3, 10, [
        step("Вхідні направлення", "Направлення з МІС MedLink потрапляють у цю чергу автоматично. Лабораторія розподіляє показники між аналізаторами.", RF, tid("lab.mis.page", "Вхідні направлення", "incomingReferralsPage"), highlight="none"),
        step("Виберіть направлення", "Клікніть направлення зі списку ліворуч.", RF, tid("lab.mis.inbox-item", "Направлення у списку", "inbox-", prefix=True), type="click", expect="click", optional=True),
        step("Панель розподілу", "Для кожного показника вибрано аналізатор за налаштуваннями «Послуги ЕСОЗ × аналізатори». Перевірте або змініть вибір.", RF, tid("lab.mis.distribution", "Панель розподілу", "distributionPanel"), optional=True, placement="left"),
        step("Підтвердіть розподіл", "Натисніть «Підтвердити й поставити в черги» — показники з'являться в робочих листах аналізаторів.", RF, tid("lab.mis.confirm", "Підтвердити розподіл", "confirmDistribution"), type="click", expect="click", optional=True),
    ], tags=["МІС", "направлення"]))

# 7. ВКЯ
QC = "/laboratory/quality-control"
SC.append(scenario("lab.quality.point", "quality", "Внесення точки контролю якості (ВКЯ)",
    "Внесення вимірювання контрольного матеріалу, оцінка за правилами Вестгарда, lockout.", QC, 3, 10, [
        step("Контроль якості", "Щодня перед роботою вносьте вимірювання контрольних матеріалів. Порушення правил Вестгарда блокують випуск результатів показника (lockout).", QC, tid("lab.qc.page", "Сторінка ВКЯ", "qcPage"), highlight="none"),
        step("Контрольний матеріал", "Виберіть матеріал / лот. Новий матеріал додається кнопкою «Матеріал» вгорі.", QC, tid("lab.qc.material", "Матеріал / лот", "qcMaterialSelect")),
        step("Показник", "Виберіть показник, для якого вносите контроль.", QC, tid("lab.qc.test", "Показник", "qcTestSelect")),
        step("Внести точку", "Натисніть «Внести точку контролю».", QC, tid("lab.qc.add-point", "Внести точку контролю", "qcAddPoint"), type="click", expect="click", optional=True),
        step("Виміряне значення", "Введіть результат, отриманий на аналізаторі для контрольного матеріалу.", QC, tid("lab.qc.value", "Виміряне значення", "qcPointValue"), type="input", value="{{value}}", optional=True, placement="left"),
        step("Збережіть та оцініть", "Натисніть «Зберегти та оцінити»: система порахує відхилення, застосує правила Вестгарда та покаже, чи потрібен lockout.", QC, tid("lab.qc.save", "Зберегти та оцінити", "qcPointSave"), type="click", expect="click", optional=True, placement="left"),
        step("Lockout", "Якщо правило порушено — показник заблоковано. Розблокувати можна тут із зазначенням причини та коригувальної дії.", QC, tid("lab.qc.tab-lockouts", "Вкладка Lockout", "tab-lockouts"), optional=True),
    ], variables=[var("value", "Виміряне значення", "5.2")], tags=["ВКЯ", "Вестгард"]))

# 8. Нове направлення
OR = "/laboratory/orders"
SC.append(scenario("lab.orders.create", "start", "Створення нового направлення",
    "Реєстрація замовлення: пацієнт, профілі та тести, CITO, створення.", OR, 4, 20, [
        step("Черга замовлень", "Тут реєструються й відстежуються всі направлення лабораторії.", OR, tid("lab.orders.page", "Сторінка замовлень", "ordersPage"), highlight="none"),
        step("Нове направлення", "Натисніть «Нове направлення» — відкриється форма реєстрації.", OR, tid("lab.orders.create", "Кнопка «Нове направлення»", "ordersCreate"), type="click", expect="click"),
        step("Форма замовлення", "Заповніть дані пацієнта, лікаря та оберіть дослідження.", OR, tid("lab.orders.dialog", "Форма замовлення", "createOrderDialog"), placement="left", optional=True),
        step("Профілі досліджень", "Оберіть готові профілі (набори тестів).", OR, tid("lab.orders.profiles", "Профілі", "profileSelect"), placement="left", optional=True),
        step("Окремі тести", "Або додайте окремі тести.", OR, tid("lab.orders.tests", "Тести", "testSelect"), placement="left", optional=True),
        step("Терміново (CITO)", "Увімкніть, якщо замовлення термінове — воно потрапить на початок черги.", OR, tid("lab.orders.cito", "CITO", "citoToggle"), placement="left", optional=True),
        step("Створіть замовлення", "Натисніть «Створити замовлення». Система присвоїть номер і штрихкоди пробірок.", OR, tid("lab.orders.submit", "Створити замовлення", "createOrderSubmit"), type="click", expect="click", optional=True, placement="left"),
    ], tags=["направлення", "замовлення", "CITO"]))

# 9. Пошук за штрихкодом
SC.append(scenario("lab.nav.barcode", "start", "Пошук за штрихкодом у шапці",
    "Швидко відкрити замовлення за штрихкодом пробірки.", D, 1, 15, [
        step("Пошук у шапці", "Введіть штрихкод пробірки або № замовлення й натисніть Enter — відкриється картка замовлення з будь-якого екрана.", D, tid("lab.header.barcode", "Пошук за штрихкодом", "headerBarcode"), placement="bottom"),
        step("Меню", "Кнопка згортає або розгортає бічне меню.", D, tid("lab.header.menu", "Кнопка меню", "menuToggle"), placement="bottom", optional=True),
    ], tags=["штрихкод", "пошук"]))

bundle = {"format": "medlink-guide-bundle", "version": 1, "app": {"code": "lab", "caption": "MedLink LIS"},
          "folders": FOLDERS, "scenarios": SC}
out = pathlib.Path(__file__).resolve().parents[2] / "src" / "MedLink.Guide" / "Seed" / "lab_processes.json"
out.write_text(json.dumps(bundle, ensure_ascii=False, indent=2), encoding="utf-8")
print("written", out, len(SC), "scenarios,", sum(len(s["steps"]) for s in SC), "steps")
