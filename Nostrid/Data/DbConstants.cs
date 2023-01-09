namespace Nostrid.Data
{
    public static class DbConstants
    {
        public const string DatabaseFilename = "Nostr.db";

        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
    }
}
