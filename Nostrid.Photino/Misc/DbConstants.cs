using System.IO;
using System;

internal static class DbConstants
{
    public const string DatabaseFilename = "Nostr.sqlite.db";
    public const string AppName = "Nostrid";

    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName,
            DatabaseFilename);
}