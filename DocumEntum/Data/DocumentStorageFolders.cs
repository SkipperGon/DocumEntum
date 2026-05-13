namespace DocumEntum.Data
{
    /// <summary>
    /// Подпапки локального хранилища: утверждённые документы и черновики в бизнес-процессе.
    /// </summary>
    public static class DocumentStorageFolders
    {
        public const string Documents = "Documents";
        public const string Workflows = "Workflows";

        public static bool IsUnderWorkflows(string? relativePath) =>
            !string.IsNullOrEmpty(relativePath) &&
            (relativePath.StartsWith($"{Workflows}/", StringComparison.Ordinal)
             || relativePath.Equals(Workflows, StringComparison.Ordinal));

        /// <summary>Старый каталог черновиков до переименования в Workflows.</summary>
        public static bool IsLegacyPendingPath(string? relativePath) =>
            !string.IsNullOrEmpty(relativePath) &&
            relativePath.StartsWith("pending/", StringComparison.Ordinal);

        public static bool IsStagingWorkflowPath(string? relativePath) =>
            IsUnderWorkflows(relativePath) || IsLegacyPendingPath(relativePath);
    }
}
