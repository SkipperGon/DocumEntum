using DocumEntum.Services;

namespace DocumEntum.Middleware
{
    public class DatabaseHealthMiddleware
    {
        private readonly RequestDelegate _next;

        public DatabaseHealthMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IDatabaseHealthService healthService)
        {
            var path = context.Request.Path;

            // Проверяем состояние ДО обработки запроса. 
            if (!healthService.IsHealthy &&
                !path.StartsWithSegments("/DBError") &&
                !path.StartsWithSegments("/css") &&
                !path.StartsWithSegments("/_framework") &&
                !path.StartsWithSegments("/_blazor") &&
                !path.StartsWithSegments("/_content") &&
                !path.StartsWithSegments("/Account/Login") &&
                !path.StartsWithSegments("/health"))
            {
                context.Response.Redirect("/DBError");
                return;
            }

            // Обрабатываем сам запрос и перехватываем обрывы соединения
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (IsDatabaseException(ex))
            {
                // Помечаем сервис как нездоровый
                healthService.MarkUnhealthy(ex.Message);

                // Если заголовки ответа еще не отправлены клиенту, мы можем сделать редирект
                if (!context.Response.HasStarted)
                {
                    context.Response.Redirect("/DBError");
                    return;
                }

                // Если ответ уже начал формироваться, редирект не сработает
                throw;
            }
        }

        // Вспомогательный метод для поиска корня проблемы в цепочке InnerException
        private bool IsDatabaseException(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                // Проверяем типичные классы исключений потери связи с БД Npgsql/PostgreSQL
                if (current is System.Net.Sockets.SocketException ||
                    current.GetType().FullName?.Contains("Npgsql") == true ||
                    current is Microsoft.EntityFrameworkCore.DbUpdateException)
                {
                    return true;
                }
                current = current.InnerException;
            }
            return false;
        }
    }
}