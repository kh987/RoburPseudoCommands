using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    // Opt-in: polar projection gate and its outer nonempty-mask gate only.
    // No SDK/config/point writes; object-nearest gates retain original flags.
    internal static partial class NativePolarPatch
    {
        internal const string Owner = "RoburPseudoCommands.NativePolarGate.v1";
        private static volatile bool _enabled;
        private static MethodInfo _target;
        private static MethodInfo _projection;
        internal static bool Enabled { get { return _enabled; } }

        internal sealed class Profile
        {
            internal readonly string Version, FileHash, IlHash, ProjectionHash;
            internal readonly int TargetToken, ProjectionToken;
            internal Profile(string version, string fileHash, string ilHash, int target, int projection)
            {
                Version = version; FileHash = fileHash; IlHash = ilHash;
                TargetToken = target; ProjectionToken = projection;
                ProjectionHash = version == "16.0.55.83"
                    ? "DEE9813D910867F5A5F596E19BD787AC51694575A923F05C949C41E4A132CD7B"
                    : "5F4B2028C96167D3702626E21945CE8F8969BC3F1891794080E845AA26946015";
            }
        }

        internal static readonly Profile[] Profiles =
        {
            new Profile("16.0.55.83",
                "ABC06829F7093ACAC4E3FE97EAB5F9D556CF25621B543EB3C1EED62DAA1C157F",
                "3E35B50D8C5415436599E01ABEB8DF5B4D9ED0FB685960FB23064F1A55033FE2",
                0x06000AC9, 0x06000AD3),
            new Profile("16.0.62.12",
                "FE8394DF903DD99F71AF27C97DAA08E8E46BBF7EFF7F3B4DA5CE14D1EEFFAE86",
                "9CD0906E03B6034FF81A9A99EACD0CB9D6B041838D4F90F037212D00D7AC8F8C",
                0x06000B2D, 0x06000B37)
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Enable()
        {
            if (_enabled) return;
            var assembly = typeof(CadView).Assembly;
            var profile = Profiles.SingleOrDefault(p => p.Version == assembly.GetName().Version.ToString());
            if (profile == null) throw new NotSupportedException("Непроверенная версия Cad.View: " + assembly.GetName().Version);
            using (var stream = File.OpenRead(assembly.Location))
                if (Hash(stream) != profile.FileHash)
                    throw new NotSupportedException("SHA-256 Cad.View не совпадает с проверенной DLL.");

            var target = assembly.ManifestModule.ResolveMethod(profile.TargetToken) as MethodInfo;
            var projection = assembly.ManifestModule.ResolveMethod(profile.ProjectionToken) as MethodInfo;
            if (target == null || projection == null || target.IsStatic || target.ReturnType != typeof(void) ||
                projection.DeclaringType != target.DeclaringType || projection.ReturnType != typeof(bool))
                throw new NotSupportedException("Сигнатура штатного метода не совпала.");
            var types = target.GetParameters().Select(p => p.ParameterType).ToArray();
            if (!types.SequenceEqual(new[] { typeof(ObjectSnapEventArgs), typeof(Vector3D).MakeByRefType(),
                typeof(ObjectSnapFlags).MakeByRefType() }))
                throw new NotSupportedException("Параметры штатного метода не совпали.");
            using (var stream = new MemoryStream(target.GetMethodBody().GetILAsByteArray()))
                if (Hash(stream) != profile.IlHash)
                    throw new NotSupportedException("SHA-256 исходного IL не совпал.");
            using (var stream = new MemoryStream(projection.GetMethodBody().GetILAsByteArray()))
                if (Hash(stream) != profile.ProjectionHash)
                    throw new NotSupportedException("SHA-256 IL проекции не совпал.");

            var info = Harmony.GetPatchInfo(target);
            if (info != null && info.Owners.Any(id => id != Owner))
                throw new InvalidOperationException("Этот метод уже изменён другим runtime-патчем.");
            var projectionInfo = Harmony.GetPatchInfo(projection);
            if (projectionInfo != null && projectionInfo.Owners.Any(id => id != Owner))
                throw new InvalidOperationException("Проекция уже изменена другим runtime-патчем.");
            _target = target;
            _projection = projection;
            try
            {
                var harmony = new Harmony(Owner);
                InstallRotation(harmony, profile);
                // Install candidate isolation BEFORE opening the shared projection gate.
                if (projectionInfo == null || !projectionInfo.Transpilers.Any(p => p.owner == Owner))
                    harmony.Patch(projection, transpiler: new HarmonyMethod(typeof(NativePolarPatch), nameof(TranspileCandidates)));
                // A failed prior unpatch may have left our disabled gate installed.
                if (info == null || !info.Transpilers.Any(p => p.owner == Owner))
                    harmony.Patch(target, prefix: new HarmonyMethod(typeof(NativePolarPatch), nameof(RefreshPolarOrientation)),
                        transpiler: new HarmonyMethod(typeof(NativePolarPatch), nameof(Transpile)));
                info = Harmony.GetPatchInfo(target);
                if (info == null || info.Transpilers.Count(p => p.owner == Owner) != 1 ||
                    info.Prefixes.Count(p => p.owner == Owner) != 1)
                    throw new InvalidOperationException("Harmony не подтвердил установку заплатки.");
                projectionInfo = Harmony.GetPatchInfo(projection);
                if (projectionInfo == null || projectionInfo.Transpilers.Count(p => p.owner == Owner) != 1)
                    throw new InvalidOperationException("Harmony не подтвердил изоляцию кандидатов.");
                _enabled = true;
                Logger.Info("native polar patch enabled sdk=" + profile.Version + " target=" +
                    profile.TargetToken.ToString("X8") + " ilSHA256=" + profile.IlHash + " gateChanges=2 candidateIsolation=polar-origin-v2");
            }
            catch
            {
                _enabled = false;
                try { new Harmony(Owner).Unpatch(target, HarmonyPatchType.All, Owner); }
                catch { /* Disabled helper preserves the original gate; restart removes the detour. */ }
                try { new Harmony(Owner).Unpatch(projection, HarmonyPatchType.Transpiler, Owner); }
                catch { /* Disabled candidate filters preserve native behavior. */ }
                try { RemoveRotation(); } catch { /* Restart removes any disabled residual detour. */ }
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Disable()
        {
            _enabled = false; // Fail-safe even when detour removal fails.
            try { if (_target != null) new Harmony(Owner).Unpatch(_target, HarmonyPatchType.All, Owner); }
            finally
            {
                try { if (_projection != null) new Harmony(Owner).Unpatch(_projection, HarmonyPatchType.Transpiler, Owner); }
                finally { RemoveRotation(); }
            }
            Logger.Info("native polar patch disabled");
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            return Rewrite(instructions, _projection);
        }

        internal static List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodInfo projection)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var source = typeof(ObjectSnapEventArgs).GetProperty("SourceSnaps").GetGetMethod();
            var flags = typeof(ObjectSnaps).GetProperty("Flags").GetGetMethod();
            var bounds = typeof(ObjectSnapEventArgs).GetProperty("Bounds").GetGetMethod();
            var osnap = typeof(DraftingSettings).GetProperty("OSnap").GetGetMethod();
            var matches = new List<int>();
            var outerMatches = new List<int>();
            for (var i = 1; i + 12 < code.Count; i++)
            {
                if (code[i - 1].opcode == OpCodes.Ldarg_1 && Calls(code[i], source) && Calls(code[i + 1], flags) &&
                    IsNearest(code[i + 2]) && code[i + 3].opcode == OpCodes.And && IsNearest(code[i + 4]) &&
                    (code[i + 5].opcode == OpCodes.Bne_Un || code[i + 5].opcode == OpCodes.Bne_Un_S) &&
                    code[i + 6].opcode == OpCodes.Ldarg_0 && code[i + 7].opcode == OpCodes.Ldarg_1 &&
                    Calls(code[i + 8], bounds) && code[i + 9].opcode == OpCodes.Ldarg_1 &&
                    code[i + 10].opcode == OpCodes.Ldarg_2 && Calls(code[i + 11], projection) &&
                    (code[i + 12].opcode == OpCodes.Brfalse || code[i + 12].opcode == OpCodes.Brfalse_S) &&
                    SameDestination(code, code[i + 5], code[i + 12])) matches.Add(i);
            }
            if (matches.Count != 1) throw new InvalidOperationException("Полярная IL-ветка: ожидалось 1 совпадение, найдено " + matches.Count);
            for (var i = 1; i + 4 < code.Count; i++)
            {
                if (code[i - 1].opcode == OpCodes.Ldarg_1 && Calls(code[i], source) && Calls(code[i + 1], flags) &&
                    (code[i + 2].opcode == OpCodes.Brfalse || code[i + 2].opcode == OpCodes.Brfalse_S) &&
                    Calls(code[i + 3], osnap) &&
                    (code[i + 4].opcode == OpCodes.Brfalse || code[i + 4].opcode == OpCodes.Brfalse_S) &&
                    SameDestination(code, code[i + 2], code[i + 4])) outerMatches.Add(i);
            }
            if (outerMatches.Count != 1) throw new InvalidOperationException("Внешняя IL-проверка маски не совпала.");
            var at = matches[0];
            // Keep all instruction labels and exception boundaries at their original positions.
            foreach (var index in new[] { at, outerMatches[0] })
            {
                code[index].opcode = OpCodes.Nop;
                code[index].operand = null;
                code[index + 1].opcode = OpCodes.Call;
                code[index + 1].operand = typeof(NativePolarPatch).GetMethod(nameof(PolarGateFlags),
                    BindingFlags.Static | BindingFlags.NonPublic);
            }
            return code;
        }

        internal static ObjectSnapFlags GateFlags(ObjectSnapFlags original, bool enabled, bool polar, bool hasBase)
        {
            return enabled && polar && hasBase ? original | ObjectSnapFlags.Nearest : original;
        }

        private static ObjectSnapFlags PolarGateFlags(ObjectSnapEventArgs args)
        {
            var flags = args.SourceSnaps.Flags;
            if (!_enabled) return flags;
            return GateFlags(flags, true, DraftingSettings.PolarTracking, args.FirstLinePoint.HasValue);
        }

        private static bool Calls(CodeInstruction code, MethodInfo method)
        {
            return method != null && (code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && Equals(code.operand, method);
        }
        private static bool IsNearest(CodeInstruction code)
        {
            return code.opcode == OpCodes.Ldc_I4 && Equals(code.operand, (int)ObjectSnapFlags.Nearest);
        }
        private static bool SameDestination(List<CodeInstruction> code, CodeInstruction first, CodeInstruction second)
        {
            if (!(first.operand is Label) || !(second.operand is Label)) return false;
            var a = (Label)first.operand;
            var b = (Label)second.operand;
            return code.Any(instruction => instruction.labels.Contains(a) && instruction.labels.Contains(b));
        }
        private static string Hash(Stream stream)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
