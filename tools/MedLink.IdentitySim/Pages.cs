// Сторінки імітатора українською: вхід (користувач МІС + пароль стенда), вибір профілю, помилки, вихід. Усі значення екрануються.
using System.Net;
using System.Text;

namespace MedLink.IdentitySim;

public static class Pages
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Layout(string title, string body) => $$"""
        <!doctype html><html lang="uk"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{{E(title)}}</title>
        <style>
          body{font-family:system-ui,Segoe UI,Arial,sans-serif;background:#eef2f6;margin:0;color:#1d2733}
          .card{max-width:460px;margin:7vh auto;background:#fff;border-radius:10px;box-shadow:0 2px 14px rgba(0,0,0,.12);padding:28px}
          h1{font-size:20px;margin:0 0 4px} .sub{color:#667;font-size:13px;margin-bottom:18px}
          label{display:block;font-size:13px;margin:14px 0 4px;color:#445}
          input,select{width:100%;box-sizing:border-box;padding:9px;border:1px solid #b9c3cf;border-radius:6px;font-size:14px}
          button{margin-top:20px;width:100%;padding:11px;border:0;border-radius:6px;background:#1976d2;color:#fff;font-size:15px;cursor:pointer}
          .err{background:#fdecea;color:#a12622;padding:9px;border-radius:6px;margin-top:12px;font-size:13px}
          .note{margin-top:18px;font-size:12px;color:#889}
          .prof{display:block;width:100%;text-align:left;margin-top:8px;background:#f3f6fa;color:#1d2733;border:1px solid #c9d3df}
        </style></head><body><div class="card">{{body}}</div></body></html>
        """;

    public static string Login(string action, string ticket, IReadOnlyList<SimUser> users, string? error)
    {
        var sb = new StringBuilder();
        sb.Append("<h1>Вхід до МІС (стенд)</h1><div class=\"sub\">Імітатор сервісу автентифікації</div>");
        if (error != null) sb.Append($"<div class=\"err\">{E(error)}</div>");
        sb.Append($"<form method=\"post\" action=\"{E(action)}\"><input type=\"hidden\" name=\"ticket\" value=\"{E(ticket)}\">");
        sb.Append("<label for=\"f\">Пошук користувача</label><input id=\"f\" type=\"text\" placeholder=\"Прізвище…\" autocomplete=\"off\">");
        sb.Append("<label for=\"u\">Користувач МІС</label><select id=\"u\" name=\"sub\" size=\"10\" required>");
        foreach (var u in users) sb.Append($"<option value=\"{E(u.Sub)}\">{E(u.Name)}</option>");
        sb.Append("</select><label for=\"p\">Пароль стенда</label><input id=\"p\" type=\"password\" name=\"password\" autocomplete=\"off\" required>");
        sb.Append("<button type=\"submit\">Увійти</button></form>");
        sb.Append("<div class=\"note\">Тестовий стенд: вхід без облікових даних користувача, за спільним паролем стенда.</div>");
        sb.Append("<script>var f=document.getElementById('f'),u=document.getElementById('u');f.addEventListener('input',function(){var q=f.value.toLowerCase();for(var i=0;i<u.options.length;i++){u.options[i].hidden=q&&u.options[i].text.toLowerCase().indexOf(q)<0;}});</script>");
        return Layout("Вхід", sb.ToString());
    }

    public static string Profiles(string action, string ticket, SimUser user)
    {
        var sb = new StringBuilder();
        sb.Append($"<h1>Оберіть профіль</h1><div class=\"sub\">{E(user.Name)}</div>");
        sb.Append($"<form method=\"post\" action=\"{E(action)}\"><input type=\"hidden\" name=\"ticket\" value=\"{E(ticket)}\">");
        foreach (var p in user.Profiles) sb.Append($"<button class=\"prof\" type=\"submit\" name=\"profile\" value=\"{E(p.Id)}\">{E(p.Caption)}</button>");
        sb.Append("</form>");
        return Layout("Профіль", sb.ToString());
    }

    public static string Error(string message) =>
        Layout("Помилка", $"<h1>Помилка запиту</h1><div class=\"err\">{E(message)}</div>");

    public static string LoggedOut() =>
        Layout("Вихід", "<h1>Ви вийшли з системи</h1><div class=\"sub\">Сеанс завершено. Закрийте вкладку або поверніться до застосунку.</div>");
}
