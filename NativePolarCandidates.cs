using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    internal static partial class NativePolarPatch
    {
        private static IEnumerable<CodeInstruction> TranspileCandidates(IEnumerable<CodeInstruction> instructions)
        {
            return RewriteCandidates(instructions, _projection);
        }

        // ClearSnaps creates polar rays with index=-1, snapType=-1 and the current FirstLinePoint.
        // AppendDynamicSnap uses nonnegative indices. Never substitute angles or projected points.
        internal static bool AllowLine(ObjectSnapEventArgs args, int index, int snapType, Vector3D origin)
        {
            if (!_enabled || (args.SourceSnaps.Flags & ObjectSnapFlags.Nearest) != 0) return true;
            return DraftingSettings.PolarTracking && args.FirstLinePoint.HasValue &&
                index == -1 && snapType == -1 && origin.Equals(args.FirstLinePoint.Value);
        }

        internal static int CircleCount(int count, ObjectSnapEventArgs args)
        {
            return !_enabled || (args.SourceSnaps.Flags & ObjectSnapFlags.Nearest) != 0 ? count : 0;
        }

        internal static List<CodeInstruction> RewriteCandidates(IEnumerable<CodeInstruction> instructions, MethodInfo projection)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var module = projection.Module;
            var token = projection.MetadataToken;
            var origin = (MethodInfo)module.ResolveMethod(token + 1);
            var index = (MethodInfo)module.ResolveMethod(token + 7);
            var snapType = (MethodInfo)module.ResolveMethod(token + 9);
            var lineType = projection.GetMethodBody().LocalVariables[3].LocalType;
            if (origin.DeclaringType != lineType || index.DeclaringType != lineType || snapType.DeclaringType != lineType ||
                origin.ReturnType != typeof(Vector3D) || index.ReturnType != typeof(int) ||
                snapType.ReturnType != typeof(ObjectSnapFlags) || new[] { origin, index, snapType }.Any(m => m.IsStatic || m.GetParameters().Length != 0))
                throw new InvalidOperationException("Сигнатуры меток направляющей не совпали.");
            var fieldToken = token == 0x06000AD3 ? 0x04000463 : token == 0x06000B37 ? 0x04000487 : 0;
            if (fieldToken == 0) throw new InvalidOperationException("Неизвестный профиль кандидатов.");
            var lines = module.ResolveField(fieldToken);
            var circles = module.ResolveField(fieldToken - 1);
            var starts = new List<int>();
            var counts = new List<int>();
            var increments = new List<int>();
            for (var i = 0; i + 4 < code.Count; i++)
            {
                if (code[i].opcode == OpCodes.Ldarg_0 && code[i + 1].opcode == OpCodes.Ldfld && Equals(code[i + 1].operand, lines) &&
                    code[i + 2].opcode == OpCodes.Ldloc_2 && Calls(code[i + 3], lines.FieldType.GetProperty("Item").GetGetMethod()) &&
                    code[i + 4].opcode == OpCodes.Stloc_3) starts.Add(i + 5);
                if (code[i].opcode == OpCodes.Ldloc_2 && code[i + 1].opcode == OpCodes.Ldc_I4_1 &&
                    code[i + 2].opcode == OpCodes.Add && code[i + 3].opcode == OpCodes.Stloc_2) increments.Add(i);
                if (code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, circles) &&
                    Calls(code[i + 1], circles.FieldType.GetProperty("Count").GetGetMethod())) counts.Add(i + 2);
            }
            if (starts.Count != 1 || counts.Count != 1 || increments.Count != 1 ||
                code.Any(c => Equals(c.operand, typeof(NativePolarPatch).GetMethod(nameof(AllowLine), BindingFlags.Static | BindingFlags.NonPublic))))
                throw new InvalidOperationException("Не совпали циклы линейных/круговых кандидатов.");
            // Reuse the existing continue label; no host state/list mutation, allocation, or reflection per candidate.
            var next = code[increments[0]].labels;
            if (next.Count == 0) throw new InvalidOperationException("Не найдена метка следующего кандидата.");
            var filter = new[]
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldloca_S, (byte)3), new CodeInstruction(OpCodes.Call, index),
                new CodeInstruction(OpCodes.Ldloca_S, (byte)3), new CodeInstruction(OpCodes.Call, snapType),
                new CodeInstruction(OpCodes.Ldloca_S, (byte)3), new CodeInstruction(OpCodes.Call, origin),
                new CodeInstruction(OpCodes.Call, typeof(NativePolarPatch).GetMethod(nameof(AllowLine), BindingFlags.Static | BindingFlags.NonPublic)),
                new CodeInstruction(OpCodes.Brfalse, next[0])
            };
            // Insert from the end so earlier indices remain stable. These insertion points have no branch/EH entries.
            if (counts[0] <= starts[0] || code[counts[0]].labels.Count != 0 || code[counts[0]].blocks.Count != 0 ||
                code[starts[0]].labels.Count != 0 || code[starts[0]].blocks.Count != 0)
                throw new InvalidOperationException("Неожиданные границы вставки фильтра.");
            code.InsertRange(counts[0], new[] { new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(NativePolarPatch).GetMethod(nameof(CircleCount), BindingFlags.Static | BindingFlags.NonPublic)) });
            code.InsertRange(starts[0], filter);
            return code;
        }
    }
}
