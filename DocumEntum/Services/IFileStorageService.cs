namespace DocumEntum.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(Stream fileStream, string originalFileName);
        Task<Stream?> GetFileStreamAsync(string storedFileNameGuid);
        Task DeleteFileAsync(string storedFileNameGuid);
    }
}
