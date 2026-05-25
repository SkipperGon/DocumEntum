using DocumEntum.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using System.Net.Sockets;
using System.Security.Claims;

namespace DocumEntum.Services
{

    public class CustomCookieAuthenticationEvents : CookieAuthenticationEvents
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDatabaseHealthService _healthService;

        public CustomCookieAuthenticationEvents(IServiceScopeFactory scopeFactory, IDatabaseHealthService healthService)
        {
            _scopeFactory = scopeFactory;
            _healthService = healthService;

            OnValidatePrincipal = async context =>
            {
                // Проверяем, не находимся ли мы уже на странице ошибки
                if (context.Request.Path.StartsWithSegments("/DBError"))
                {
                    return;
                }

                //Если сервис уже знает, что БД лежит, прерываем валидацию, 
                // чтобы не вызывать исключения и не зацикливать цикл запросов
                if (!_healthService.IsHealthy)
                {
                    context.RejectPrincipal();
                    context.HttpContext.Response.Redirect("/DBError");
                    return;
                }

                var userPrincipal = context.Principal;
                if (userPrincipal?.Identity?.IsAuthenticated != true)
                    return;

                var userId = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                    return;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.FindByIdAsync(userId);

                    if (user == null || user.IsBlocked)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync();
                    }
                }
                catch (Exception ex) when (IsDatabaseConnectionException(ex))
                {
                    // База данных недоступна
                    _healthService.MarkUnhealthy(ex.Message);
                    context.HttpContext.Response.Redirect("/DBError");
                    context.RejectPrincipal();
                }
            };

        }
        private bool IsDatabaseConnectionException(Exception ex)
        {
            return ex is NpgsqlException ||
                   ex is SocketException ||
                   ex is IOException ||
                   (ex is InvalidOperationException ioEx && ioEx.Message.Contains("transient failure")) ||
                   ex.InnerException != null && IsDatabaseConnectionException(ex.InnerException);
        }
    }
}