using DocumEntum.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DocumEntum.Services;

public class CustomCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CustomCookieAuthenticationEvents(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        // Подписываемся на событие валидации cookie-принципала
        OnValidatePrincipal = async context =>
        {
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

            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);

            if (user == null) // пользователь удалён из БД
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
            }
        };
    }
}