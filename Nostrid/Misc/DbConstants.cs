namespace Nostrid.Misc
{
    public static class DbConstants
    {
        public const string DatabaseFilename = "Nostr.sqlite.db";

        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
    }
}
