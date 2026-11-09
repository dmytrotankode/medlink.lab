// =============================================================================
// PostgreSQL: поведінка текстових запитів, сумісна з очікуваннями користувачів ЛІС.
//  1) string.Contains/StartsWith/EndsWith(рядок) у LINQ → ILIKE (пошук без урахування регістру, зокрема кирилиці;
//     на PostgreSQL звичайний LIKE/strpos чутливий до регістру). Спецсимволи %, _ і \ у введенні екрануються.
//  2) OrderBy/ThenBy за «людськими» текстовими полями (ПІБ, назва, опис…) → COLLATE "uk-x-icu":
//     колація БД en_US ставить «ґ» перед «а», а «ї» перед «і»; українське впорядкування дає ICU.
// Перехоплювач підключається лише для Npgsql (Program.cs). Колація перевіряється при старті (PgQuerySettings).
// =============================================================================
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MedLink.LIS.Api.Data;

/// <summary>Налаштування, які визначаються після підключення до БД (наявність ICU-колації).</summary>
public sealed class PgQuerySettings
{
    /// <summary>Назва колації для сортування; null — не застосовувати (колації немає в кластері або вимкнено).</summary>
    public volatile string? Collation;
    /// <summary>Чи перетворювати Contains/StartsWith/EndsWith на ILIKE.</summary>
    public volatile bool CaseInsensitiveSearch = true;
}

public sealed class PgQueryInterceptor : IQueryExpressionInterceptor
{
    private readonly PgQuerySettings _settings;
    public PgQueryInterceptor(PgQuerySettings settings) => _settings = settings;

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        => new Rewriter(_settings).Visit(queryExpression);

    private sealed class Rewriter : ExpressionVisitor
    {
        private static readonly MethodInfo ILike = typeof(NpgsqlDbFunctionsExtensions)
            .GetMethod(nameof(NpgsqlDbFunctionsExtensions.ILike), new[] { typeof(DbFunctions), typeof(string), typeof(string), typeof(string) })!;
        private static readonly MethodInfo CollateOpen = typeof(RelationalDbFunctionsExtensions).GetMethod(nameof(RelationalDbFunctionsExtensions.Collate))!;
        private static readonly MethodInfo Replace2 = typeof(string).GetMethod(nameof(string.Replace), new[] { typeof(string), typeof(string) })!;
        private static readonly MethodInfo Concat3 = typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string), typeof(string) })!;
        private static readonly MethodInfo Concat2 = typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) })!;
        private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        private static readonly MethodInfo StartsWith = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
        private static readonly MethodInfo EndsWith = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;
        private static readonly HashSet<string> OrderMethods = new() { "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending" };
        /// <summary>Поля з людським текстом, за якими сортують у списках (коди, статуси, номери сортуються байтово).</summary>
        private static readonly Regex HumanText = new("(Name|Caption|Title|Fio|Description|Address|Position|Specialty|Comment|Note)$", RegexOptions.Compiled);

        private readonly PgQuerySettings _s;
        public Rewriter(PgQuerySettings s) => _s = s;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // --- пошук без урахування регістру ---
            if (_s.CaseInsensitiveSearch && node.Object != null && node.Arguments.Count == 1 && node.Method.DeclaringType == typeof(string)
                && (node.Method == Contains || node.Method == StartsWith || node.Method == EndsWith))
            {
                var text = Visit(node.Object)!;
                var arg = Visit(node.Arguments[0])!;
                var escaped = Escape(arg);
                var percent = Expression.Constant("%");
                Expression pattern = node.Method == Contains ? Expression.Call(Concat3, percent, escaped, percent)
                    : node.Method == StartsWith ? Expression.Call(Concat2, escaped, percent)
                    : Expression.Call(Concat2, percent, escaped);
                return Expression.Call(ILike, Expression.Constant(EF.Functions), text, pattern, Expression.Constant("\\")); // ESCAPE '\' (без явного escape EF додає ESCAPE '' — тоді екранування не діє)
            }

            // --- українське впорядкування ---
            var collation = _s.Collation;
            if (collation != null && node.Method.DeclaringType == typeof(Queryable) && OrderMethods.Contains(node.Method.Name)
                && node.Arguments.Count == 2 && node.Method.GetGenericArguments() is { Length: 2 } ga && ga[1] == typeof(string)
                && node.Arguments[1] is UnaryExpression { Operand: LambdaExpression lambda }
                && MemberName(lambda.Body) is { } member && HumanText.IsMatch(member))
            {
                var source = Visit(node.Arguments[0])!;
                var collated = Expression.Call(CollateOpen.MakeGenericMethod(typeof(string)), Expression.Constant(EF.Functions), lambda.Body, Expression.Constant(collation));
                var newLambda = Expression.Lambda(collated, lambda.Parameters);
                return Expression.Call(node.Method, source, Expression.Quote(newLambda));
            }
            return base.VisitMethodCall(node);
        }

        private static string? MemberName(Expression e) => e switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u => MemberName(u.Operand),
            _ => null
        };

        /// <summary>replace(replace(replace(x, '\', '\\'), '%', '\%'), '_', '\_') — переводиться в SQL і для параметра, і для константи.</summary>
        private static Expression Escape(Expression arg)
        {
            Expression r = arg;
            foreach (var ch in new[] { "\\", "%", "_" })
                r = Expression.Call(r, Replace2, Expression.Constant(ch), Expression.Constant("\\" + ch));
            return r;
        }
    }
}
