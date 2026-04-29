using System;
using System.Windows.Forms;
using Topomatic.ApplicationPlatform;

namespace RoburPseudoCommands
{
    internal static class AliasCommandBootstrapper
    {
        private static readonly object SyncRoot = new object();
        private static Timer _timer;
        private static bool _scheduled;
        private static bool _executed;

        public static void Schedule()
        {
            lock (SyncRoot)
            {
                if (_scheduled)
                    return;

                _scheduled = true;
            }

            try
            {
                _timer = new Timer { Interval = 250 };
                _timer.Tick += TimerTick;
                _timer.Start();
                Logger.Info("alias command bootstrap scheduled");
            }
            catch (Exception ex)
            {
                Logger.Error("failed to schedule alias command bootstrap", ex);
            }
        }

        private static void TimerTick(object sender, EventArgs e)
        {
            try
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= TimerTick;
                    _timer.Dispose();
                    _timer = null;
                }

                Run();
            }
            catch (Exception ex)
            {
                Logger.Error("alias command bootstrap timer failed", ex);
            }
        }

        private static void Run()
        {
            lock (SyncRoot)
            {
                if (_executed)
                    return;

                _executed = true;
            }

            try
            {
                ApplicationHost.Current.Plugins.Execute("pseudo_alias_bootstrap", new object[0]);
                Logger.Info("alias command bootstrap requested");
            }
            catch (Exception ex)
            {
                Logger.Error("failed to request alias command bootstrap", ex);
            }
        }
    }
}
