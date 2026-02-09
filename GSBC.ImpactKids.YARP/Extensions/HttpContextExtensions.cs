namespace GSBC.ImpactKids.YARP.Extensions;

internal static class HttpContextExtensions
{
    extension(HttpContext context)
    {
        public string BuildRedirectUrl(string? redirectUrl)
        {
            if (string.IsNullOrEmpty(redirectUrl))
            {
                redirectUrl = "/";
            }
            if (redirectUrl.StartsWith('/'))
            {
                redirectUrl = context.Request.Scheme + "://" + context.Request.Host + context.Request.PathBase + redirectUrl;
            }
            return redirectUrl;
        }
    }
}