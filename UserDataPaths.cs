using System;
using System.IO;

namespace RoburPseudoCommands
{
    internal static class UserDataPaths
    {
        public static string GetPluginDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                throw new InvalidOperationException(
                    "Папка ApplicationData недоступна. Запись данных рядом с DLL плагина запрещена.");
            }

            return Path.Combine(appData, "Topomatic", "RoburPseudoCommands");
        }
    }
}
