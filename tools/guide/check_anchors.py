#!/usr/bin/env python3
"""Автоперевірка якорів інструкцій (Guide): для кожного кроку кожного сценарію з Seed/*.json
перевіряє, що якір існує і знаходиться в коді Vue (data-testid, текстовий літерал, aria-label, id/клас).
Запуск: python tools/guide/check_anchors.py [--report docs/.../anchors_report.md] [--list]
Код виходу 1, якщо є кроки без валідного якоря. Така сама логіка — у тесті AnchorCoverageTests (C#)."""
import glob, json, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SEED = os.path.join(ROOT, 'src', 'MedLink.Guide', 'Seed')
SRC_DIRS = [os.path.join(ROOT, 'src', 'MedLink.LIS.Web', 'src'), os.path.join(ROOT, 'src', 'MedLink.Guide.Web', 'src')]
EXT = ('.vue', '.js', '.html')

norm = lambda s: re.sub(r'\s+', ' ', s or '').strip().lower()


WEB = os.path.join(ROOT, 'src', 'MedLink.LIS.Web', 'src')


def resolve_import(frm, spec):
    base = None
    if spec.startswith('.'): base = os.path.normpath(os.path.join(os.path.dirname(frm), spec))
    elif spec.startswith('@/'): base = os.path.join(WEB, spec[2:])
    elif spec.split('/')[0] in ('pages', 'components', 'layouts', 'mixins', 'utils', 'services', 'boot'): base = os.path.join(WEB, spec)
    if not base: return None
    for c in (base, base + '.vue', base + '.js', os.path.join(base, 'index.js')):
        if os.path.isfile(c) and c.endswith(EXT): return os.path.normpath(c)
    return None


def deps(files, start, memo=None):
    """Транзитивні імпорти файлу (вкладені компоненти/міксини/утиліти)."""
    seen, todo = set(), [start]
    while todo:
        f = todo.pop()
        if f in seen or f not in files: continue
        seen.add(f)
        for m in re.finditer(r"""(?:from\s+|import\s*\(\s*|import\s+|require\(\s*)['"]([^'"]+)['"]""", files[f]):
            r = resolve_import(f, m.group(1))
            if r: todo.append(r)
    return seen


def load_routes(files):
    """[(regex маршруту, файл сторінки)] з router/laboratoryRoutes.js."""
    src = open(os.path.join(WEB, 'router', 'laboratoryRoutes.js'), encoding='utf8').read()
    out = []
    for m in re.finditer(r"""lab\(\s*'([^']*)'\s*,\s*'[^']*'\s*,\s*'[^']*'\s*,\s*\(\)\s*=>\s*import\('([^']+)'\)""", src):
        path, comp = m.group(1), m.group(2)
        full = '/laboratory/' + path if path else '/laboratory'
        rx = re.compile('^' + re.sub(r':[\w]+', '[^/]+', re.escape(full).replace('\:', ':')) + '$')
        f = resolve_import(os.path.join(WEB, 'router', 'x.js'), comp) or resolve_import(os.path.join(WEB, 'x.js'), comp)
        out.append((rx, f, full))
    return out


def load_sources():
    files = {}
    for d in SRC_DIRS:
        for dp, _, fs in os.walk(d):
            for f in fs:
                if f.endswith(EXT):
                    p = os.path.join(dp, f)
                    files[os.path.normpath(p)] = open(p, encoding='utf8').read()
    return files


class Index:
    def __init__(self, files):
        self.all = '\n'.join(files.values())
        self.all_l = self.all.lower()
        self.testids, self.testid_prefixes = set(), set()
        for m in re.finditer(r'''data-(?:testid|guide-id)\s*=\s*"([^"]*)"''', self.all):
            self.testids.add(m.group(1))
        for m in re.finditer(r'''(?::|v-bind:)data-(?:testid|guide-id)\s*=\s*"([^"]*)"''', self.all):
            for lit in re.findall(r"""'([^']*)'|`([^`$]*)""", m.group(1)):
                v = lit[0] or lit[1]
                if len(v) >= 2: self.testid_prefixes.add(v)
        self.texts, self.text_res = {}, []  # текст -> множина тегів-власників ('*' — будь-який: label=, рядкові літерали)
        VOID = {'br', 'hr', 'img', 'input', 'meta', 'link'}
        for src in files.values():
            stack, pos = [], 0
            for m in re.finditer(r'<(/?)([A-Za-z][\w-]*)([^>]*?)(/?)>', src):
                seg = src[pos:m.start()]; pos = m.end()
                self._add_text(seg, stack[-1] if stack else '*')
                tag = m.group(2).lower()
                if m.group(1):
                    while stack and stack[-1] != tag: stack.pop()
                    if stack: stack.pop()
                elif not m.group(4) and tag not in VOID:
                    stack.append(tag)
            for m in re.finditer(r'''(?:label|title|caption|placeholder|aria-label|text)\s*=\s*"([^"{]*)"''', src):
                self._add_text(m.group(1), '*')
            for q in ("'", '"', '`'):
                for m in re.finditer(re.escape(q) + r"([^" + re.escape(q) + "\n$]{2,120})" + re.escape(q), src):
                    self._add_text(m.group(1), '*')

    def _add_text(self, seg, tag):
        t = norm(re.sub(r'&nbsp;', ' ', seg))
        if not t: return
        if '{{' in t:
            if len(re.sub(r'\{\{.*?\}\}', '', t).strip()) >= 2:
                self.text_res.append((tag, re.compile('^' + '.*'.join(re.escape(x.strip()) for x in re.split(r'\{\{.*?\}\}', t)) + '$')))
        else:
            self.texts.setdefault(t, set()).add(tag)

    def has_text(self, t, tag=None):
        t = norm(t)
        def ok(tags):  # компоненти Quasar (q-*) рендеряться в різні теги — для них тег не звіряємо
            return (not tag) or '*' in tags or tag in tags or any(x.startswith('q-') or '-' in x or x[0].isupper() for x in tags)
        # тег не звіряємо: плеєр шукає текст і серед будь-яких елементів, якщо тег змінився (span → div)
        return t in self.texts or any(r.match(t) for g, r in self.text_res)

    def has_testid(self, v, op=''):
        if op == '^':
            return any(x.startswith(v) for x in self.testids) or any(v.startswith(p) or p.startswith(v) for p in self.testid_prefixes) or ('"%s' % v) in self.all or ("'%s" % v) in self.all or ('`%s' % v) in self.all
        if op in ('$', '*'):
            return any((v in x) for x in self.testids) or any(v in p for p in self.testid_prefixes) or v in self.all
        return v in self.testids or any(v.startswith(p) for p in self.testid_prefixes) or ('"%s"' % v) in self.all or ("'%s'" % v) in self.all or ('`%s`' % v) in self.all

    def has_token(self, tok):
        return tok.lower() in self.all_l


def check_strategy(ix, st, fp=None):
    t, v = st.get('type'), st.get('value', '')
    if t == 'text':
        tag, _, text = v.partition('|')
        if not _: tag, text = (fp or {}).get('tag'), v  # без «тег|»: тег з відбитку або будь-який
        return ix.has_text(text, tag), 'текст «%s» (тег %s) не знайдено в шаблонах' % (text, tag)
    if t in ('testid', 'guide'):
        m = re.search(r'''\[data-[a-z-]+\s*([\^$*]?)=\s*"([^"]+)"\]''', v)
        if not m: return False, 'незрозумілий селектор ' + v
        return ix.has_testid(m.group(2), m.group(1)), 'data-testid «%s%s» не знайдено' % (m.group(1) + '=', m.group(2))
    # css / aria / attr / id: усі значення атрибутів, #id, .клас мають зустрічатись у коді
    mt = re.fullmatch(r'''\s*[\w]*\[data-[a-z-]+\s*=\s*"([^"]+)"\]\s*''', v)
    if mt: return ix.has_testid(mt.group(1)), 'data-testid «%s» не знайдено' % mt.group(1)
    toks = [m.group(1) for m in re.finditer(r'''\[[a-z-]+[\^$*~|]?=\s*"([^"]*)"\]''', v)]
    rest = re.sub(r'\[[^\]]*\]', ' ', v)
    toks += re.findall(r'#([\w-]+)', rest)
    toks += [c for c in re.findall(r'\.([\w-]+)', rest) if not c.startswith('q-')]
    toks = [x for x in toks if x]
    if not toks: return True, ''  # лише теги/класи фреймворку
    miss = [x for x in toks if not ix.has_token(x)]
    return (not miss), 'у коді немає: ' + ', '.join(miss)


def load_bundles():
    res = []
    for f in sorted(glob.glob(os.path.join(SEED, '*.json'))):
        res.append((os.path.basename(f), json.load(open(f, encoding='utf8'))))
    return res


def run():
    files = load_sources()
    routes = load_routes(files)
    layout = deps(files, os.path.join(WEB, 'layouts', 'baseLayout', 'BaseLayout.vue'))
    guide_files = {f for f in files if f.startswith(os.path.normpath(SRC_DIRS[1]))}
    cache = {}

    def index_for(route):
        key = None
        for rx, f, full in routes:
            if rx.match(route or ''): key = f; break
        if key not in cache:
            sub = (deps(files, key) | layout | guide_files) if key else set(files)
            cache[key] = (Index({f: files[f] for f in sub}), key is not None)
        return cache[key]
    bundles = load_bundles()
    eff = {}  # code -> strategies (остання повна відозначена, як при імпорті)
    for _, d in bundles:
        for s in d.get('scenarios', []):
            for st in s['steps']:
                a = st.get('anchor')
                if a and a.get('code') and a.get('strategies'): eff[a['code']] = a
    rows, warns, n_steps, n_sc = [], [], 0, 0
    gix = Index(files)
    for fn, d in bundles:
        for s in d.get('scenarios', []):
            n_sc += 1
            cur_route = (s.get('startRoute') or '').split('?')[0]
            for i, st in enumerate(s['steps'], 1):
                n_steps += 1
                if st.get('route'): cur_route = st['route'].split('?')[0]
                ix, routed = index_for(cur_route)
                a = st.get('anchor')
                hl = st.get('highlight')
                if not a:
                    if hl == 'element': rows.append((fn, s['code'], i, st.get('title'), '-', 'крок з highlight=element без якоря'))
                    continue
                code = a.get('code')
                e = eff.get(code) if code else (a if a.get('strategies') else None)
                if not e:
                    rows.append((fn, s['code'], i, st.get('title'), code, 'якір без визначення (немає strategies)')); continue
                if not e['strategies']:
                    rows.append((fn, s['code'], i, st.get('title'), code, 'порожній список стратегій')); continue
                res = [check_strategy(ix, g, e.get('fingerprint')) for g in e['strategies']]
                if any(r[0] for r in res): continue
                # крок міг перейти на іншу сторінку кліком (маршрут у сіді не змінюється) — шукаємо в усьому коді
                gres = [check_strategy(gix, g, e.get('fingerprint')) for g in e['strategies']]
                where = ' [сторінка %s]' % cur_route if routed else ' [маршрут %s не знайдено]' % cur_route
                if any(r[0] for r in gres):
                    warns.append((fn, s['code'], i, st.get('title'), code, 'знайдено лише на іншій сторінці' + where))
                else:
                    rows.append((fn, s['code'], i, st.get('title'), code, '; '.join(r[1] for r in res) + where))
    return n_sc, n_steps, rows, warns


if __name__ == '__main__':
    n_sc, n_steps, rows, warns = run()
    print('Сценаріїв: %d, кроків: %d, кроків без валідного якоря: %d (у %d сценаріях); попереджень (якір лише на іншій сторінці): %d' % (n_sc, n_steps, len(rows), len({r[1] for r in rows}), len(warns)))
    if '--list' in sys.argv or rows:
        for r in rows: print('  %s #%d [%s] %s — %s' % (r[1], r[2], r[4], (r[3] or '')[:40], r[5]))
    if '--report' in sys.argv:
        p = sys.argv[sys.argv.index('--report') + 1]
        with open(p, 'w', encoding='utf8') as f:
            f.write('# Звіт покриття якорів\n\nСценаріїв: %d; кроків: %d; без валідного якоря: %d\n\n' % (n_sc, n_steps, len(rows)))
            for r in rows: f.write('- `%s` #%d (`%s`): %s\n' % (r[1], r[2], r[4], r[5]))
    sys.exit(1 if rows else 0)
