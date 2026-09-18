using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Topomatic.ApplicationPlatform;
using Topomatic.ApplicationPlatform.Plugins;

namespace RoburPseudoCommands
{
    internal enum EmergencyExecutionResult
    {
        Completed,
        Cancelled,
        NotAvailable,
        RegistryFailure,
        Failed
    }

    internal static class EmergencyCommandRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, PluginFunction> Snapshot = new Dictionary<string, PluginFunction>(StringComparer.OrdinalIgnoreCase);
        private static Timer _captureTimer;
        private static int _lastObservedCount = -1;
        private static int _stableSamples;
        private static int _sampleCount;
        private static bool _captureComplete;

        public static int Count { get { lock (SyncRoot) return Snapshot.Count; } }
        public static bool IsCaptureComplete { get { lock (SyncRoot) return _captureComplete; } }

        public static void ScheduleCapture()
        {
            CaptureSample();
            if (_captureTimer != null) return;
            _captureTimer = new Timer { Interval = 250 };
            _captureTimer.Tick += delegate { try { CaptureSample(); } catch (Exception ex) { Logger.Error("emergency command snapshot sample failed", ex); } };
            _captureTimer.Start();
            Logger.Info("emergency command snapshot scheduled");
        }

        public static string[] GetCommandNames()
        {
            lock (SyncRoot) return Snapshot.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static EmergencyExecutionResult TryExecute(string command, object[] args, out string error)
        {
            command = (command ?? string.Empty).Trim();
            PluginFunction function;
            lock (SyncRoot) Snapshot.TryGetValue(command, out function);
            if (function == null)
            {
                error = "Команда '" + command + "' отсутствует в безопасном снимке.";
                Logger.Info("emergency execute rejected command='" + command + "' reason=not-in-snapshot snapshotCount=" + Count);
                return EmergencyExecutionResult.NotAvailable;
            }

            try
            {
                var invocationArguments = ProtectedCommandArguments.Build(command, args);
                Logger.Info("emergency execute command='" + command + "' commandArgs=" +
                    (args == null ? 0 : args.Length) + " invocationArgs=" + invocationArguments.Length);
                function.Execute(invocationArguments);
                error = string.Empty;
                return EmergencyExecutionResult.Completed;
            }
            catch (Exception ex)
            {
                error = GetInnermostMessage(ex);
                if (IsCancellation(ex))
                {
                    Logger.Info("emergency direct handler cancelled command='" + command + "' reason='" + error + "'");
                    return EmergencyExecutionResult.Cancelled;
                }

                Logger.Error("emergency direct handler failed command='" + command + "'", ex);
                return IsRegistryFailure(ex)
                    ? EmergencyExecutionResult.RegistryFailure
                    : EmergencyExecutionResult.Failed;
            }
        }

        public static bool IsCancellation(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (current is OperationCanceledException) return true;
            return false;
        }
        public static bool IsRegistryFailure(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (current is KeyNotFoundException || current is TargetParameterCountException) return true;
            return false;
        }

        private static void CaptureSample()
        {
            Dictionary<string, PluginFunction> live;
            string source;
            if (!TryReadLargestRegistry(out live, out source) || live.Count == 0) return;

            var added = 0;
            lock (SyncRoot)
            {
                foreach (var item in live)
                {
                    if (Snapshot.ContainsKey(item.Key)) continue;
                    Snapshot.Add(item.Key, item.Value);
                    added++;
                }

                _sampleCount++;
                if (Snapshot.Count == _lastObservedCount) _stableSamples++; else _stableSamples = 0;
                _lastObservedCount = Snapshot.Count;
                if ((_sampleCount >= 12 && _stableSamples >= 4) || _sampleCount >= 40)
                {
                    _captureComplete = true;
                    if (_captureTimer != null)
                    {
                        _captureTimer.Stop();
                        _captureTimer.Dispose();
                        _captureTimer = null;
                    }
                }
            }

            if (added > 0 || IsCaptureComplete)
                Logger.Info("emergency command snapshot sample source='" + source + "' liveCount=" + live.Count + " added=" + added + " snapshotCount=" + Count + " complete=" + IsCaptureComplete);
        }

        private static bool TryReadLargestRegistry(out Dictionary<string, PluginFunction> registry, out string source)
        {
            registry = new Dictionary<string, PluginFunction>(StringComparer.OrdinalIgnoreCase);
            source = string.Empty;
            object manager;
            try { manager = ApplicationHost.Current.Plugins; } catch { return false; }
            if (manager == null) return false;

            var type = manager.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in type.GetFields(flags))
            {
                object value;
                try { value = field.GetValue(manager); } catch { continue; }
                ConsiderDictionary(value, "field:" + field.Name, ref registry, ref source);
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                object value;
                try { value = property.GetValue(manager, null); } catch { continue; }
                ConsiderDictionary(value, "property:" + property.Name, ref registry, ref source);
            }

            return registry.Count > 0;
        }

        private static void ConsiderDictionary(object candidate, string candidateSource, ref Dictionary<string, PluginFunction> best, ref string source)
        {
            var dictionary = candidate as IDictionary;
            if (dictionary == null || dictionary.Count <= best.Count) return;

            var found = new Dictionary<string, PluginFunction>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key as string;
                    var function = entry.Value as PluginFunction ?? TryUnwrapFunction(entry.Value);
                    if (!string.IsNullOrWhiteSpace(key) && function != null && !found.ContainsKey(key))
                        found.Add(key.Trim(), function);
                }
            }
            catch { return; }

            if (found.Count <= best.Count) return;
            best = found;
            source = candidateSource;
        }

        private static PluginFunction TryUnwrapFunction(object value)
        {
            if (value == null) return null;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in value.GetType().GetFields(flags))
            {
                if (!typeof(PluginFunction).IsAssignableFrom(field.FieldType)) continue;
                try { return field.GetValue(value) as PluginFunction; } catch { return null; }
            }

            foreach (var property in value.GetType().GetProperties(flags))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0 ||
                    !typeof(PluginFunction).IsAssignableFrom(property.PropertyType)) continue;
                try { return property.GetValue(value, null) as PluginFunction; } catch { return null; }
            }

            return null;
        }

        private static string GetInnermostMessage(Exception exception)
        {
            var current = exception;
            while (current.InnerException != null) current = current.InnerException;
            return current.Message;
        }
    }
}
