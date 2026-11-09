namespace MedLink.LIS.Api.Configuration;

public static class SessionControlOptions
{
    public const string CurrentSessionClaimType = "current_session_id";
    public const string SidClaimType = "sid";
    public const string DefaultActiveTokenKeyPrefix = "IDS:ACTIVE_TOKEN:";
    public const string DefaultTokenByHashKeyPrefix = "IDS:TOKEN_BY_HASH:";
}
