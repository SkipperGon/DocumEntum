namespace DocumEntum.Services
{
    public interface IDatabaseHealthService
    {
        bool IsHealthy { get; }
        string? LastErrorMessage { get; }
        void MarkHealthy();
        void MarkUnhealthy(string error);
    }
}
