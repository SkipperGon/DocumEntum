namespace DocumEntum.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _rootPath;

        public FileStorageService(IConfiguration config)
        {
            _rootPath = config["LocalStorage:RootPath"] ?? "Storage";
            if (!Directory.Exists(_rootPath))
                Directory.CreateDirectory(_rootPath);
        }

        /// <summary>
        /// Сохраняет файл на диск под именем GUID (без расширения).
        /// Возвращает строку GUID.
        /// </summary>
        public async Task<string> SaveFileAsync(Stream fileStream, string originalFileName)
        {
            var guid = Guid.NewGuid().ToString();   // без расширения
            var fullPath = Path.Combine(_rootPath, guid);
            using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(file);
            return guid;
        }

        /// <summary>
        /// Возвращает поток файла по GUID (расширения в пути нет).
        /// </summary>
        public async Task<Stream?> GetFileStreamAsync(string storedFileNameGuid)
        {
            var fullPath = Path.Combine(_rootPath, storedFileNameGuid);
            if (!File.Exists(fullPath))
                return null;

            var memory = new MemoryStream();
            await using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                await stream.CopyToAsync(memory);
            memory.Position = 0;
            return memory;
        }

        public async Task DeleteFileAsync(string storedFileNameGuid)
        {
            var fullPath = Path.Combine(_rootPath, storedFileNameGuid);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            await Task.CompletedTask;
        }
    }
}
