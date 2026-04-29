using Topomatic.ApplicationPlatform.Plugins;

namespace RoburPseudoCommands
{
    internal sealed class AliasPluginFunction : PluginFunction
    {
        private readonly string _alias;
        private readonly bool _forceExecute;

        public AliasPluginFunction(string alias, bool forceExecute)
        {
            _alias = alias;
            _forceExecute = forceExecute;
        }

        public override object Execute(object[] args)
        {
            AliasCommandDispatcher.Execute(_alias, _forceExecute);
            return null;
        }
    }
}
