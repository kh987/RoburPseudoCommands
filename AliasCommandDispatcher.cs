namespace RoburPseudoCommands
{
    public static class AliasCommandDispatcher
    {
        public static void Execute(string alias, bool forceExecute)
        {
            Module.ExecuteRegisteredAlias(alias, forceExecute);
        }
    }
}
