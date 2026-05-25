using DocumEntum.Components;
using DocumEntum.Components.Account;
using DocumEntum.Data;
using DocumEntum.Middleware;
using DocumEntum.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal;

namespace DocumEntum
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<SuperAdminStatusService>();
            builder.Services.AddScoped<IFileStorageService, FileStorageService>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IDocumentTypeService, DocumentTypeService>();
            builder.Services.AddScoped<IWorkflowAdminService, WorkflowAdminService>();
            builder.Services.AddScoped<IWorkflowService, WorkflowService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IOrganizationService, OrganizationService>();

            // Регистрируем сервис здоровья БД (singleton)
            builder.Services.AddSingleton<IDatabaseHealthService, DatabaseHealthService>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
                .AddIdentityCookies();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 0,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                }));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddScoped<CustomCookieAuthenticationEvents>();
            builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
            {
                options.EventsType = typeof(CustomCookieAuthenticationEvents);
            });

            var app = builder.Build();

            app.UseMiddleware<DatabaseErrorHandlingMiddleware>();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            { }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // Middleware проверки здоровья БД
            app.UseMiddleware<DatabaseHealthMiddleware>();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();

            // ----- Инициализация БД с обработкой ошибок -----
            using (var scope = app.Services.CreateScope())
            {
                var healthService = scope.ServiceProvider.GetRequiredService<IDatabaseHealthService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    string[] roles = { "Admin", "SuperAdmin", "Employee" };
                    foreach (var role in roles)
                        if (!await roleManager.RoleExistsAsync(role))
                            await roleManager.CreateAsync(new IdentityRole(role));

                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var superAdminStatus = scope.ServiceProvider.GetRequiredService<SuperAdminStatusService>();
                    var superAdmins = await userManager.GetUsersInRoleAsync("SuperAdmin");
                    superAdminStatus.HasSuperAdmin = superAdmins.Any();

                    if (superAdminStatus.HasSuperAdmin)
                    {

                        lock (Console.Out)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("\n------------------------------------------------");
                            Console.WriteLine("[INFO] Главный администратор найден в БД при запуске");
                            Console.WriteLine("------------------------------------------------\n");
                        }
                    }
                    else
                    {
                        lock (Console.Out)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n------------------------------------------------");
                            Console.WriteLine("[WARN] Главный администратор не найден в БД при запуске");
                            Console.WriteLine("При первом входе потребуется создание по /Account/SetupSuperAdmin");
                            Console.WriteLine("------------------------------------------------\n");
                            Console.ResetColor();
                        }
                    }
                    // Всё успешно
                    healthService.MarkHealthy();
                    logger.LogInformation("Database initialized successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Failed to initialize database. Application will run in degraded mode.");
                    healthService.MarkUnhealthy(ex.Message);
                    lock (Console.Out)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n------------------------------------------------");
                        Console.WriteLine("[ERROR] Сервер PostgreSQL/PostgresPro недоступен");
                        Console.WriteLine("Рекомендуется устранить проблему и перезапустить приложение");
                        Console.WriteLine("------------------------------------------------\n");
                        Console.ResetColor();
                    }
                }
            }

            
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.MapGet("/api/documents/{id:int}/download", async (int id, IDocumentService documentService) =>
            {
                var result = await documentService.GetDocumentFileAsync(id);
                if (result == null) return Results.NotFound();
                var (stream, contentType, fileName, _) = result.Value;
                return Results.File(stream, contentType, fileName);
            }).RequireAuthorization();

            app.MapGet("/api/documents/versions/{id:int}/download", async (int id, IDocumentService documentService) =>
            {
                var result = await documentService.GetDocumentVersionFileAsync(id);
                if (result == null) return Results.NotFound();
                var (stream, contentType, fileName, _) = result.Value;
                return Results.File(stream, contentType, fileName);
            }).RequireAuthorization();

            // Health-check endpoint для внешнего мониторинга
            app.MapGet("/health", (IDatabaseHealthService health) =>
                health.IsHealthy ? Results.Ok("Healthy") : Results.StatusCode(503));

            app.MapAdditionalIdentityEndpoints();

            app.Run();
        }
    }
}