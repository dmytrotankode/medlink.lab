// Налаштування імітатора (секція Sim). Пароль стенда — лише зі змінної середовища Sim__Password, у журнал не потрапляє.
namespace MedLink.IdentitySim;

public sealed class SimOptions
{
    public string Issuer { get; set; } = "";
    public string Password { get; set; } = "";
    public string KeyPath { get; set; } = "data/signing-key.pem";
    public string? MedlinkConnectionString { get; set; }
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenHours { get; set; } = 8;
    public int CodeSeconds { get; set; } = 60;
    public Dictionary<string, ClientOptions> Clients { get; set; } = new();
    public List<SimUser> Users { get; set; } = new();

    /// <summary>Issuer без кінцевого слеша: усі адреси провайдера будуються від нього (на стенді містить префікс /id).</summary>
    public string IssuerBase => Issuer.TrimEnd('/');
    /// <summary>Схема+хост з Issuer: у шаблонах клієнтів {origin} замінюється на нього.</summary>
    public string Origin => new Uri(IssuerBase).GetLeftPart(UriPartial.Authority);
}

public sealed class ClientOptions
{
    public List<string> AllowedRedirectUris { get; set; } = new();
    public List<string> AllowedPostLogoutRedirectUris { get; set; } = new();
    public List<string> AllowedScopes { get; set; } = new();

    public bool RedirectAllowed(string? uri, string origin) => Matches(AllowedRedirectUris, uri, origin);
    public bool PostLogoutAllowed(string? uri, string origin) => Matches(AllowedPostLogoutRedirectUris, uri, origin);
    // Точне співпадіння (як у справжнього Identity); єдиний шаблон — {origin}
    private static bool Matches(List<string> list, string? uri, string origin) =>
        !string.IsNullOrEmpty(uri) && list.Any(t => string.Equals(t.Replace("{origin}", origin), uri, StringComparison.Ordinal));
}

public sealed class SimProfile
{
    public string Id { get; set; } = "";
    public string Caption { get; set; } = "";
    public string? TypeId { get; set; }
    public SimProfile() { }
    public SimProfile(string id, string caption, string? typeId) { Id = id; Caption = caption; TypeId = typeId; }
}

public sealed class SimUser
{
    public string Sub { get; set; } = "";
    public string Name { get; set; } = "";
    public List<SimProfile> Profiles { get; set; } = new();
}
