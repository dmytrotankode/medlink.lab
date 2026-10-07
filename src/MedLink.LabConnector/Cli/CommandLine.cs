// =============================================================================
// MedLink LIS Analyzer Connector — простий розбір аргументів командного рядка
// (без зовнішніх залежностей): команда + параметри --name value | --name=value | --flag.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
namespace MedLink.LabConnector.Cli;

public sealed class CommandLine
{
    private static readonly HashSet<string> KnownCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "run", "setup", "test", "preview-order", "status", "install-service", "uninstall-service", "list-profiles", "version", "help", "--help", "-h",
    };

    public string Command { get; private set; } = "run";
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Positional { get; } = new();
    /// <summary>Аргументи, що передаються хосту (напр. --urls, --environment).</summary>
    public string[] Passthrough { get; private set; } = Array.Empty<string>();

    public string? Get(string name) => Options.TryGetValue(name, out var v) ? v : null;
    public bool Has(string name) => Options.ContainsKey(name);
    public string Require(string name)
        => Get(name) ?? throw new ArgumentException($"Потрібен параметр --{name}");

    public static CommandLine Parse(string[] args)
    {
        var cli = new CommandLine();
        var passthrough = new List<string>();
        int i = 0;
        if (args.Length > 0 && !args[0].StartsWith('-') && KnownCommands.Contains(args[0]))
        {
            cli.Command = args[0].ToLowerInvariant();
            i = 1;
        }
        else if (args.Length > 0 && KnownCommands.Contains(args[0]) && args[0].StartsWith('-'))
        {
            cli.Command = args[0];
            i = 1;
        }
        for (; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--"))
            {
                var body = a[2..];
                string name, value;
                int eq = body.IndexOf('=');
                if (eq >= 0) { name = body[..eq]; value = body[(eq + 1)..]; }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) { name = body; value = args[++i]; }
                else { name = body; value = "true"; }
                cli.Options[name] = value;
                // Параметри хоста ASP.NET передаємо далі
                if (name is "urls" or "environment" or "contentRoot") { passthrough.Add($"--{name}"); passthrough.Add(value); }
            }
            else if (a.StartsWith('-') && a.Length == 2)
            {
                var name = a[1..];
                var value = i + 1 < args.Length && !args[i + 1].StartsWith('-') ? args[++i] : "true";
                cli.Options[name] = value;
            }
            else cli.Positional.Add(a);
        }
        cli.Passthrough = passthrough.ToArray();
        return cli;
    }
}
