using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GSBC.ImpactKids.Web.Components.Pages.Analytics;

public partial class MemoryVerseAnalytics
{
    private string DashboardUrl()
    {
        return "https://kids-metabase.baptist.com.au/embed/dashboard/" + GetToken(MetabaseConfig.DashboardMappings["MemoryVerseAnalytics"]) + "#theme=night&bordered=true&titled=true";
    }

    private string GetToken(int dashboardId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(MetabaseConfig.Secret));
        SigningCredentials   credentials = new(securityKey, SecurityAlgorithms.HmacSha256Signature);
        JwtHeader            header      = new(credentials);
        Dictionary<string, int> dash = new()
        {
            { "dashboard", dashboardId }
        };

        Dictionary<string, string> pars = new();
        JwtPayload payload = new()
        {
            { "resource", dash },
            { "params", pars }
        };
        
        var     secToken    = new JwtSecurityToken(header, payload);
        var     handler     = new JwtSecurityTokenHandler();
        string? tokenString = handler.WriteToken(secToken);
        return tokenString;
    }
}