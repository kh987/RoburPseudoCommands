using System;
using System.Collections.Generic;

namespace RoburPseudoCommands
{
    internal static class DynamicAliasCommandFactory
    {
        private static readonly HashSet<string> ReservedAliases = new HashSet<string>(
            new[]
            {
                "pseudo_command",
                "pseudo_reload_aliases",
                "pseudo_edit_aliases",
                "pseudo_show_aliases",
                "pseudo_show_log",
                "pseudo_autoload"
            },
            StringComparer.OrdinalIgnoreCase);

        public static bool IsSupportedAliasName(string alias)
        {
            if (string.IsNullOrEmpty(alias) || ReservedAliases.Contains(alias))
                return false;

            foreach (var ch in alias)
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                    return false;
            }

            return true;
        }
    }
}
