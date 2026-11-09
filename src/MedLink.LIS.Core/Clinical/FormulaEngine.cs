// =============================================================================
// Безпечний обчислювач формул розрахункових показників (FR-WS-005).
// Власний розбір без eval/скриптів: числа (десятковий роздільник — крапка), + - * / ^, дужки, унарний мінус, посилання на коди
// показників (літери, цифри, «_»), контекстні змінні (AGE, MALE, FEMALE) та обмежений набір функцій.
// Помилки повертаються як значення (Error), а не винятки — для перевірки синтаксису в довіднику.
// =============================================================================
using System.Globalization;

namespace MedLink.LIS.Core.Clinical;

/// <summary>Контекстні змінні, доступні у формулах поряд із кодами показників.</summary>
public static class FormulaContextVariables
{
    /// <summary>Вік повних років (дробовий), стать: MALE = 1 для чоловіка, FEMALE = 1 для жінки.</summary>
    public static readonly string[] Names = { "AGE", "MALE", "FEMALE" };
    public static bool IsContext(string name) => Names.Contains(name, StringComparer.OrdinalIgnoreCase);
}

public sealed class ParsedFormula
{
    internal FormulaNode? Root { get; init; }
    public string? Error { get; init; }
    public bool IsValid => Error == null && Root != null;
    /// <summary>Коди показників (без контекстних змінних), від яких залежить формула; верхній регістр.</summary>
    public IReadOnlyList<string> Inputs { get; init; } = Array.Empty<string>();
    /// <summary>Контекстні змінні, що використані у формулі.</summary>
    public IReadOnlyList<string> ContextVariables { get; init; } = Array.Empty<string>();
}

public sealed class FormulaEvaluation
{
    public double? Value { get; init; }
    /// <summary>Коди вхідних показників, для яких значення немає.</summary>
    public IReadOnlyList<string> Missing { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }
    public bool Ok => Value.HasValue && Error == null;
}

internal abstract record FormulaNode;
internal sealed record NumberNode(double Value) : FormulaNode;
internal sealed record VarNode(string Name) : FormulaNode;
internal sealed record UnaryNode(FormulaNode Operand) : FormulaNode;
internal sealed record BinaryNode(char Op, FormulaNode Left, FormulaNode Right) : FormulaNode;
internal sealed record CallNode(string Function, List<FormulaNode> Args) : FormulaNode;

public static class FormulaEngine
{
    public const int MaxLength = 500;
    private const int MaxDepth = 40;

    private static readonly Dictionary<string, (int Min, int Max)> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ROUND"] = (1, 2), ["ABS"] = (1, 1), ["MIN"] = (2, 8), ["MAX"] = (2, 8), ["SQRT"] = (1, 1), ["LN"] = (1, 1),
        ["LOG10"] = (1, 1), ["EXP"] = (1, 1), ["POW"] = (2, 2), ["FLOOR"] = (1, 1), ["CEIL"] = (1, 1)
    };

    public static ParsedFormula Parse(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return new ParsedFormula { Error = "Формула порожня" };
        if (formula.Length > MaxLength) return new ParsedFormula { Error = $"Формула задовга (понад {MaxLength} символів)" };
        try
        {
            var p = new Parser(formula);
            var root = p.ParseAll();
            return new ParsedFormula { Root = root, Inputs = p.Inputs.ToList(), ContextVariables = p.Context.ToList() };
        }
        catch (FormulaSyntaxException ex) { return new ParsedFormula { Error = ex.Message }; }
    }

    /// <summary>Обчислення. Змінна без значення (resolver повертає null) → у Missing, значення немає.</summary>
    public static FormulaEvaluation Evaluate(ParsedFormula formula, Func<string, double?> resolve)
    {
        if (!formula.IsValid) return new FormulaEvaluation { Error = formula.Error ?? "Формула некоректна" };
        var missing = new List<string>();
        try
        {
            var v = Eval(formula.Root!, resolve, missing);
            if (missing.Count > 0) return new FormulaEvaluation { Missing = missing.Distinct().ToList() };
            if (double.IsNaN(v) || double.IsInfinity(v)) return new FormulaEvaluation { Error = "Результат обчислення не є скінченним числом" };
            return new FormulaEvaluation { Value = v };
        }
        catch (FormulaSyntaxException ex) { return new FormulaEvaluation { Error = ex.Message }; }
    }

    /// <summary>Пошук циклу залежностей: повертає ланцюг кодів (A → B → A) або null.</summary>
    public static List<string>? FindCycle(string startCode, IEnumerable<string> startInputs, Func<string, IEnumerable<string>> inputsOf)
    {
        var path = new List<string> { startCode.ToUpperInvariant() };
        List<string>? Dfs(string code, IEnumerable<string> deps)
        {
            foreach (var d0 in deps)
            {
                var d = d0.ToUpperInvariant();
                if (d == path[0]) return new List<string>(path) { d };
                if (path.Contains(d)) continue;
                path.Add(d);
                var r = Dfs(d, inputsOf(d));
                if (r != null) return r;
                path.RemoveAt(path.Count - 1);
            }
            return null;
        }
        return Dfs(startCode, startInputs);
    }

    private static double Eval(FormulaNode n, Func<string, double?> resolve, List<string> missing)
    {
        switch (n)
        {
            case NumberNode num: return num.Value;
            case VarNode v:
            {
                var val = resolve(v.Name);
                if (!val.HasValue) { missing.Add(v.Name); return 0; }
                return val.Value;
            }
            case UnaryNode u: return -Eval(u.Operand, resolve, missing);
            case BinaryNode b:
            {
                var l = Eval(b.Left, resolve, missing);
                var r = Eval(b.Right, resolve, missing);
                switch (b.Op)
                {
                    case '+': return l + r;
                    case '-': return l - r;
                    case '*': return l * r;
                    case '/':
                        if (missing.Count == 0 && r == 0) throw new FormulaSyntaxException("Ділення на нуль");
                        return r == 0 ? 0 : l / r;
                    case '^': return Math.Pow(l, r);
                }
                throw new FormulaSyntaxException("Невідомий оператор");
            }
            case CallNode c:
            {
                var a = c.Args.Select(x => Eval(x, resolve, missing)).ToArray();
                switch (c.Function.ToUpperInvariant())
                {
                    case "ROUND": return Math.Round(a[0], a.Length > 1 ? (int)Math.Clamp(a[1], 0, 10) : 0, MidpointRounding.AwayFromZero);
                    case "ABS": return Math.Abs(a[0]);
                    case "MIN": return a.Min();
                    case "MAX": return a.Max();
                    case "SQRT": return missing.Count == 0 && a[0] < 0 ? throw new FormulaSyntaxException("Корінь з від'ємного числа") : Math.Sqrt(Math.Max(a[0], 0));
                    case "LN": return missing.Count == 0 && a[0] <= 0 ? throw new FormulaSyntaxException("Логарифм невід'ємного числа не існує для нуля та від'ємних") : Math.Log(Math.Max(a[0], double.Epsilon));
                    case "LOG10": return missing.Count == 0 && a[0] <= 0 ? throw new FormulaSyntaxException("Логарифм визначений лише для додатних чисел") : Math.Log10(Math.Max(a[0], double.Epsilon));
                    case "EXP": return Math.Exp(a[0]);
                    case "POW": return Math.Pow(a[0], a[1]);
                    case "FLOOR": return Math.Floor(a[0]);
                    case "CEIL": return Math.Ceiling(a[0]);
                }
                throw new FormulaSyntaxException($"Невідома функція {c.Function}");
            }
        }
        throw new FormulaSyntaxException("Невідомий вузол формули");
    }

    private sealed class FormulaSyntaxException : Exception { public FormulaSyntaxException(string m) : base(m) { } }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;
        private int _depth;
        public readonly HashSet<string> Inputs = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Context = new(StringComparer.OrdinalIgnoreCase);

        public Parser(string s) { _s = s; }

        public FormulaNode ParseAll()
        {
            var n = ParseExpr();
            SkipWs();
            if (_i < _s.Length) throw new FormulaSyntaxException($"Неочікуваний символ «{_s[_i]}» на позиції {_i + 1}");
            return n;
        }

        private void SkipWs() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }
        private char Peek() { SkipWs(); return _i < _s.Length ? _s[_i] : '\0'; }

        private FormulaNode ParseExpr()
        {
            if (++_depth > MaxDepth) throw new FormulaSyntaxException("Забагато вкладених дужок");
            var left = ParseTerm();
            while (Peek() is '+' or '-') { var op = _s[_i++]; left = new BinaryNode(op, left, ParseTerm()); }
            _depth--;
            return left;
        }

        private FormulaNode ParseTerm()
        {
            var left = ParseUnary();
            while (Peek() is '*' or '/' or '×' or '÷' or '·')
            {
                var op = _s[_i++];
                left = new BinaryNode(op is '*' or '×' or '·' ? '*' : '/', left, ParseUnary());
            }
            return left;
        }

        private FormulaNode ParseUnary()
        {
            if (Peek() == '-') { _i++; return new UnaryNode(ParseUnary()); }
            if (Peek() == '+') { _i++; return ParseUnary(); }
            return ParsePower();
        }

        private FormulaNode ParsePower()
        {
            var b = ParsePrimary();
            if (Peek() == '^') { _i++; return new BinaryNode('^', b, ParseUnary()); } // права асоціативність
            return b;
        }

        private FormulaNode ParsePrimary()
        {
            var c = Peek();
            if (c == '\0') throw new FormulaSyntaxException("Формула обірвана: очікується значення");
            if (c == '(')
            {
                _i++;
                var e = ParseExpr();
                if (Peek() != ')') throw new FormulaSyntaxException("Не закрито дужку «(»");
                _i++;
                return e;
            }
            if (char.IsDigit(c) || c == '.') return ParseNumber();
            if (char.IsLetter(c) || c == '_') return ParseName();
            throw new FormulaSyntaxException($"Неочікуваний символ «{c}» на позиції {_i + 1}");
        }

        private FormulaNode ParseNumber()
        {
            var start = _i;
            while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
            var text = _s[start.._i];
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) throw new FormulaSyntaxException($"Некоректне число «{_s[start.._i]}»");
            return new NumberNode(v);
        }

        private FormulaNode ParseName()
        {
            var start = _i;
            while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '_')) _i++;
            var name = _s[start.._i];
            if (Peek() == '(')
            {
                if (!Functions.TryGetValue(name, out var arity)) throw new FormulaSyntaxException($"Невідома функція «{name}»");
                _i++;
                var args = new List<FormulaNode>();
                if (Peek() != ')')
                {
                    args.Add(ParseExpr());
                    while (Peek() == ';' || Peek() == ',')
                    {
                        // десятковий роздільник у числах — лише крапка; кома або «;» розділяють аргументи
                        _i++;
                        args.Add(ParseExpr());
                    }
                }
                if (Peek() != ')') throw new FormulaSyntaxException($"Не закрито дужку у виклику {name.ToUpperInvariant()}");
                _i++;
                if (args.Count < arity.Min || args.Count > arity.Max)
                    throw new FormulaSyntaxException($"Функція {name.ToUpperInvariant()} приймає {(arity.Min == arity.Max ? arity.Min.ToString() : $"{arity.Min}–{arity.Max}")} аргумент(и), передано {args.Count}");
                return new CallNode(name.ToUpperInvariant(), args);
            }
            var up = name.ToUpperInvariant();
            if (char.IsDigit(up[0])) throw new FormulaSyntaxException($"Код показника не може починатися з цифри: «{name}»");
            if (FormulaContextVariables.IsContext(up)) Context.Add(up); else Inputs.Add(up);
            return new VarNode(up);
        }
    }
}
