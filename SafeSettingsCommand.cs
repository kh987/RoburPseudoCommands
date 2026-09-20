namespace RoburPseudoCommands
{
    internal static class ProtectedCommandArguments
    {
        internal static bool RequiresPackedHandler(string command)
        {
            switch ((command ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "dsettings":
                case "smdx_manager":
                case "options":
                    return true;
                default:
                    return false;
            }
        }

        internal static object[] Build(string command, object[] commandArguments)
        {
            var args = commandArguments ?? new object[0];
            return RequiresPackedHandler(command) ? new object[] { args } : args;
        }
    }
}
