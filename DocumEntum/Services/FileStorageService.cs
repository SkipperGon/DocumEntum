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

        private string GetFullPath(string relativePath)
        {
            // нормализация разделителей
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(_rootPath, relativePath);
            var fullRoot = Path.GetFullPath(_rootPath);
            var fullRes = Path.GetFullPath(full);
            if (!fullRes.StartsWith(fullRoot))
                throw new UnauthorizedAccessException("Path traversal attempt");
            return fullRes;
        }

        private void EnsureSubFolder(string subFolder)
        {
            if (!string.IsNullOrEmpty(subFolder))
            {
                var folder = GetFullPath(subFolder);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string subFolder = "")
        {
            var guid = Guid.NewGuid().ToString();
            var relativePath = string.IsNullOrEmpty(subFolder) ? guid : Path.Combine(subFolder, guid);
            var fullPath = GetFullPath(relativePath);
            EnsureSubFolder(subFolder);
            using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(file);
            // Возвращаем относительный путь
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        public async Task<Stream?> GetFileStreamAsync(string relativePath)
        {
            var fullPath = GetFullPath(relativePath);
            if (!File.Exists(fullPath))
                return null;
            var memory = new MemoryStream();
            await using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                await stream.CopyToAsync(memory);
            memory.Position = 0;
            return memory;
        }

        public async Task DeleteFileAsync(string relativePath)
        {
            var fullPath = GetFullPath(relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            await Task.CompletedTask;
        }

        public async Task<string> MoveFileAsync(string sourceRelativePath, string targetSubFolder)
        {
            var sourceFull = GetFullPath(sourceRelativePath);
            if (!File.Exists(sourceFull))
                throw new FileNotFoundException($"File not found: {sourceRelativePath}");

            // Генерируем GUID для файла
            var newGuid = Guid.NewGuid().ToString();
            var targetRelative = string.IsNullOrEmpty(targetSubFolder) ? newGuid : Path.Combine(targetSubFolder, newGuid);
            var targetFull = GetFullPath(targetRelative);
            EnsureSubFolder(targetSubFolder);

            File.Move(sourceFull, targetFull);
            await Task.CompletedTask;
            return targetRelative.Replace(Path.DirectorySeparatorChar, '/');
        }

        public async Task<string> CopyFileAsync(string sourceRelativePath, string targetSubFolder)
        {
            var sourceFull = GetFullPath(sourceRelativePath);
            if (!File.Exists(sourceFull))
                throw new FileNotFoundException($"File not found: {sourceRelativePath}");

            var newGuid = Guid.NewGuid().ToString();
            var targetRelative = string.IsNullOrEmpty(targetSubFolder) ? newGuid : Path.Combine(targetSubFolder, newGuid);
            var targetFull = GetFullPath(targetRelative);
            EnsureSubFolder(targetSubFolder);

            File.Copy(sourceFull, targetFull, overwrite: false);
            await Task.CompletedTask;
            return targetRelative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}