using System.Linq;
using System.Web.Mvc;

namespace GSTReraReconciliation
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new SecurityHeadersAttribute());
        }
    }

    /// <summary>
    /// Custom action filter that adds security headers to all responses.
    /// Prevents clickjacking (X-Frame-Options), MIME-type sniffing (X-Content-Type-Options),
    /// and enforces referrer policy.
    /// </summary>
    public class SecurityHeadersAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var response = filterContext.HttpContext.Response;

            // Prevent clickjacking
            if (!response.Headers.AllKeys.Contains("X-Frame-Options"))
            {
                response.Headers.Add("X-Frame-Options", "DENY");
            }

            // Prevent MIME-type sniffing
            if (!response.Headers.AllKeys.Contains("X-Content-Type-Options"))
            {
                response.Headers.Add("X-Content-Type-Options", "nosniff");
            }

            // Referrer policy
            if (!response.Headers.AllKeys.Contains("Referrer-Policy"))
            {
                response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            }

            // Permissions policy — disable unused browser features
            if (!response.Headers.AllKeys.Contains("Permissions-Policy"))
            {
                response.Headers.Add("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            }

            base.OnResultExecuting(filterContext);
        }
    }
}
