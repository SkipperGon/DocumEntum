using DocumEntum.Services;

namespace DocumEntum.Middleware
{
    public class SuperAdminSetupMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SuperAdminStatusService _status;

        public SuperAdminSetupMiddleware(RequestDelegate next, SuperAdminStatusService status)
        {
            _next = next;
            _status = status;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/Account/SetupSuperAdmin") ||
                path.StartsWith("/_framework") ||
                path.StartsWith("/_content") ||
                path.StartsWith("/css") ||
                path.StartsWith("/js") ||
                path.StartsWith("/lib") ||
                path.StartsWith("/Account/Login") ||
                path.StartsWith("/Account/Logout") ||
                path.StartsWith("/Account/ExternalLogin") ||
                path == "/")
            {
                await _next(context);
                return;
            }

            // Если суперадмина нет при запуске перенаправляем на страницу создания
            if (!_status.HasSuperAdmin)
            {
                context.Response.Redirect("/Account/SetupSuperAdmin");
                return;
            }

            await _next(context);
        }
    }
}