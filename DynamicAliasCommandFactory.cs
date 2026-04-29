using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Topomatic.ApplicationPlatform.Plugins;

namespace RoburPseudoCommands
{
    internal static class DynamicAliasCommandFactory
    {
        private static readonly object SyncRoot = new object();
        private static HashSet<string> _registeredAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ReservedAliases = new HashSet<string>(
            new[]
            {
                "pseudo_command",
                "pseudo_reload_aliases",
                "pseudo_edit_aliases",
                "pseudo_show_aliases",
                "pseudo_show_log",
                "pseudo_alias_bootstrap"
            },
            StringComparer.OrdinalIgnoreCase);

        public static Type[] CreateTypes()
        {
            try
            {
                var store = new AliasStore();
                var count = store.Reload();
                var aliases = store.Aliases.Values
                    .Where(x => IsSupportedAliasName(x.Alias))
                    .OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                SetRegisteredAliases(aliases.Select(x => x.Alias));

                if (aliases.Count == 0)
                {
                    Logger.Info("dynamic aliases: no aliases registered; aliases='" + store.ActivePath + "'");
                    return new Type[0];
                }

                var type = CreateType(aliases);
                Logger.Info(string.Format(
                    "dynamic aliases registered aliasesCount={0}; methodsCount={1}; aliases='{2}'",
                    aliases.Count,
                    aliases.Count * 2,
                    store.ActivePath));
                Logger.Info("dynamic aliases commands: " + string.Join(", ", aliases.Select(FormatAliasForLog).ToArray()));

                if (aliases.Count != count)
                {
                    Logger.Info(string.Format(
                        "dynamic aliases skipped unsupportedCount={0}; loadedCount={1}",
                        count - aliases.Count,
                        count));
                }

                return new[] { type };
            }
            catch (Exception ex)
            {
                Logger.Error("failed to create dynamic alias command types", ex);
                return new Type[0];
            }
        }

        public static int RegisterFunctions(PluginFactory factory)
        {
            if (factory == null)
                return 0;

            try
            {
                var aliases = LoadSupportedAliases();
                SetRegisteredAliases(aliases.Select(x => x.Alias));

                var registeredMethods = 0;
                foreach (var alias in aliases)
                {
                    if (RegisterAliasFunction(factory, alias.Alias, ToCommandName(alias.Alias), false))
                        registeredMethods++;

                    if (RegisterAliasFunction(factory, alias.Alias, ToCommandName(alias.Alias) + "\b", true))
                        registeredMethods++;
                }

                Logger.Info(string.Format(
                    "dynamic aliases explicitly registered aliasesCount={0}; methodsCount={1}; methodsAttempted={2}",
                    aliases.Count,
                    registeredMethods,
                    aliases.Count * 2));
                Logger.Info("dynamic aliases explicitly registered commands: " + string.Join(", ", aliases.Select(FormatAliasForLog).ToArray()));
                return aliases.Count;
            }
            catch (Exception ex)
            {
                Logger.Error("failed to explicitly register dynamic alias functions", ex);
                return 0;
            }
        }

        public static bool IsRegistered(string alias)
        {
            lock (SyncRoot)
            {
                return _registeredAliases.Contains((alias ?? string.Empty).Trim());
            }
        }

        public static string[] GetRegisteredAliases()
        {
            lock (SyncRoot)
            {
                return _registeredAliases.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }

        private static Type CreateType(IList<AliasEntry> aliases)
        {
            var assemblyName = new AssemblyName("RoburPseudoCommands.DynamicAliases." + Guid.NewGuid().ToString("N"));
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicAliases");
            var typeBuilder = moduleBuilder.DefineType(
                "RoburPseudoCommands.DynamicAliases.AliasModule_" + Guid.NewGuid().ToString("N"),
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(PluginInitializator));

            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

            var methodIndex = 0;
            foreach (var alias in aliases)
            {
                DefineAliasMethod(typeBuilder, methodIndex++, alias.Alias, ToCommandName(alias.Alias), false);
                DefineAliasMethod(typeBuilder, methodIndex++, alias.Alias, ToCommandName(alias.Alias) + "\b", true);
            }

            return typeBuilder.CreateType();
        }

        private static List<AliasEntry> LoadSupportedAliases()
        {
            var store = new AliasStore();
            store.Reload();
            return store.Aliases.Values
                .Where(x => IsSupportedAliasName(x.Alias))
                .OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool RegisterAliasFunction(PluginFactory factory, string alias, string commandName, bool forceExecute)
        {
            try
            {
                factory.RegisterFunction(commandName, new AliasPluginFunction(alias, forceExecute));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("failed to explicitly register dynamic alias function command='" + commandName + "'", ex);
                return false;
            }
        }

        private static void DefineAliasMethod(
            TypeBuilder typeBuilder,
            int methodIndex,
            string alias,
            string commandName,
            bool forceExecute)
        {
            var methodBuilder = typeBuilder.DefineMethod(
                "AliasCommand" + methodIndex.ToString("0000"),
                MethodAttributes.Public,
                typeof(void),
                Type.EmptyTypes);

            var constructor = typeof(cmdAttribute).GetConstructor(new[] { typeof(string) });
            methodBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, new object[] { commandName }));

            var execute = typeof(AliasCommandDispatcher).GetMethod(
                "Execute",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(bool) },
                null);

            var il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldstr, alias);
            il.Emit(forceExecute ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Call, execute);
            il.Emit(OpCodes.Ret);
        }

        public static bool IsSupportedAliasName(string alias)
        {
            if (string.IsNullOrEmpty(alias))
                return false;

            if (ReservedAliases.Contains(alias))
                return false;

            foreach (var ch in alias)
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                    return false;
            }

            return true;
        }

        private static void SetRegisteredAliases(IEnumerable<string> aliases)
        {
            lock (SyncRoot)
            {
                _registeredAliases = new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string FormatAliasForLog(AliasEntry alias)
        {
            return "'" + alias.Alias + "' (" + ToCodePoints(alias.Alias) + "), '" + alias.Alias + "<BS>'";
        }

        private static string ToCodePoints(string value)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                if (i > 0)
                    sb.Append(' ');

                sb.Append("U+");
                sb.Append(((int)value[i]).ToString("X4"));
            }

            return sb.ToString();
        }

        private static string ToCommandName(string alias)
        {
            return alias.ToLowerInvariant();
        }
    }
}
