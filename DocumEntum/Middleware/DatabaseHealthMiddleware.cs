using DocumEntum.Services;

namespace DocumEntum.Middleware
{
    public class DatabaseHealthMiddleware
    {
        private readonly RequestDelegate _next;

        public DatabaseHealthMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IDatabaseHealthService healthService)
        {
            // Пропускаем страницу ошибки БД, статические файлы и страницу входа (чтобы избежать редиректа)
            var path = context.Request.Path;
            if (!healthService.IsHealthy &&
                !path.StartsWithSegments("/DBError") &&
                !path.StartsWithSegments("/css") &&
                !path.StartsWithSegments("/_framework") &&
                !path.StartsWithSegments("/_content") &&
                !path.StartsWithSegments("/Account/Login") &&
                !path.StartsWithSegments("/health"))
            {
                context.Response.Redirect("/DBError");
                return;
            }

            await _next(context);
        }
    }
}
