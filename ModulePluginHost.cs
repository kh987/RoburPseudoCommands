using System;
using System.Linq;
using Topomatic.ApplicationPlatform.Plugins;

namespace RoburPseudoCommands
{
    public class ModulePluginHost : PluginHostInitializator
    {
        public override void Initialize(PluginFactory factory)
        {
            base.Initialize(factory);
            DynamicAliasCommandFactory.RegisterFunctions(factory);
        }

        protected override Type[] GetTypes()
        {
            try
            {
                Logger.Info("ModulePluginHost.GetTypes started");
                var dynamicTypes = DynamicAliasCommandFactory.CreateTypes();
                var types = new[] { typeof(Module) }
                    .Concat(dynamicTypes)
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
