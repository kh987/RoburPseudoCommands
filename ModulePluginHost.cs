using System;
using System.Linq;
using Topomatic.ApplicationPlatform.Plugins;

namespace RoburPseudoCommands
{
    public class ModulePluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            try
            {
                Logger.Info("ModulePluginHost.GetTypes started");
                var types = new[] { typeof(Module) }
                    .Concat(DynamicAliasCommandFactory.CreateTypes())
                    .ToArray();
                Logger.Info("ModulePluginHost.GetTypes completed count=" + types.Length);
                return types;
            }
            catch (Exception ex)
            {
                Logger.Error("ModulePluginHost.GetTypes failed; returning Module only", ex);
                return new[] { typeof(Module) };
            }
        }
    }
}
