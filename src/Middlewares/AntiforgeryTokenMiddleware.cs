using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace Kastra.Web.Middlewares
{
    public class AntiForgeryTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAntiforgery _antiforgery;

        public AntiForgeryTokenMiddleware(RequestDelegate next, IAntiforgery antiforgery)
        {
            _next = next;
            _antiforgery = antiforgery;
        }

        public async Task Invoke(HttpContext context)
        {
            // Apply antiforgery check for all POST requests
            if (HttpMethods.IsPost(context.Request.Method))
            {
                await _antiforgery.ValidateRequestAsync(context);
            }

            await _next(context);
        }
    }
}