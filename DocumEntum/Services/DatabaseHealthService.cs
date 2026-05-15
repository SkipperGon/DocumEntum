using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DocumEntum.Services
{
    public class DatabaseHealthService : IDatabaseHealthService
    {
        public bool IsHealthy { get; private set; } = true;
        public string? LastErrorMessage { get; private set; }

        public void MarkHealthy()
        {
            IsHealthy = true;
            LastErrorMessage = null;
        }
        public void MarkUnhealthy(string error)
        {
            IsHealthy = false;
            LastErrorMessage = error;
        }
    }
}
