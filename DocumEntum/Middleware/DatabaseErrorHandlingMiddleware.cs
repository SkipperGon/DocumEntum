using Npgsql;
using System.Net.Sockets;

namespace DocumEntum.Middleware
{
    public class DatabaseErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public DatabaseErrorHandlingMiddleware(RequestDelegate next, ILogger<DatabaseErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (IsDatabaseConnectionException(ex))
            {
                _logger.LogError(ex, "Database connection error, redirecting to /DBError");
                context.Response.Redirect("/DBError");
            }
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
