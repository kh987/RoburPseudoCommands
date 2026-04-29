using System;
using Topomatic.ApplicationPlatform;

namespace RoburPseudoCommands
{
    internal static class RoburCommandCache
    {
        public static bool TryClear(out string error)
        {
            error = string.Empty;

            try
            {
                ApplicationHost.Current.Plugins.Execute("clearcache", new object[0]);
                Logger.Info("Robur plugin command cache cleared with clearcache");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Error("failed to clear Robur plugin command cache with clearcache", ex);
                return false;
            }
        }
    }
}
