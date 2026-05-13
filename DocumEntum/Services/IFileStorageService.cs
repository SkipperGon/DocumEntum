namespace DocumEntum.Services
{
    public interface IFileStorageService
    {
        // cохранить файл по относительному пути
        Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string subFolder = "");

        // Получить поток файла по относительному пути
        Task<Stream?> GetFileStreamAsync(string relativePath);

        // Удалить файл по относительному пути
        Task DeleteFileAsync(string relativePath);

        // Переместить файл по относительному пути
        Task<string> MoveFileAsync(string sourceRelativePath, string targetSubFolder);

        /// <summary>Копировать файл в подпапку (относительный путь как у SaveFileAsync).</summary>
        Task<string> CopyFileAsync(string sourceRelativePath, string targetSubFolder);
    }
}