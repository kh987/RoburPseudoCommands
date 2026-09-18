using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoburPseudoCommands;
using Topomatic.Cad.View;

internal static class Program
{
    private static string _sdk;
    private static int _checks;
    private static int Main(string[] args)
    {
        _sdk = args.Length == 0 ? @"C:\Program Files\Topomatic Robur Genplan 16.0" : args[0];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            var name = new AssemblyName(e.Name).Name;
            if (!name.StartsWith("Topomatic.", StringComparison.Ordinal)) return null;
            var path = Path.Combine(_sdk, name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); Console.WriteLine("PASS checks=" + _checks); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var nearest = ObjectSnapFlags.Nearest;
        foreach (var enabled in new[] { false, true })
        foreach (var polar in new[] { false, true })
        foreach (var hasBase in new[] { false, true })
        foreach (var original in new[] { ObjectSnapFlags.None, ObjectSnapFlags.EndPoint, nearest, nearest | ObjectSnapFlags.EndPoint })
        {
            var result = NativePolarPatch.GateFlags(original, enabled, polar, hasBase);
            Check(result == (enabled && polar && hasBase ? original | nearest : original), "truth table");
            Check((result & ~nearest) == (original & ~nearest), "other flags retained");
        }

        var assembly = typeof(CadView).Assembly;
        var profile = NativePolarPatch.Profiles.Single(p => p.Version == assembly.GetName().Version.ToString());
        var target = (MethodInfo)assembly.ManifestModule.ResolveMethod(profile.TargetToken);
        var projection = (MethodInfo)assembly.ManifestModule.ResolveMethod(profile.ProjectionToken);
        var generator = new DynamicMethod("labels", typeof(void), Type.EmptyTypes).GetILGenerator();
        var originalCode = PatchProcessor.GetOriginalInstructions(target, generator);
        var changed = NativePolarPatch.Rewrite(originalCode, projection);
        Check(changed.Count == originalCode.Count, "instruction count preserved");
        var differences = Enumerable.Range(0, changed.Count).Where(i => changed[i].opcode != originalCode[i].opcode ||
            !Equals(changed[i].operand, originalCode[i].operand)).ToArray();
        Check(differences.Length == 4 && differences[1] == differences[0] + 1 &&
            differences[3] == differences[2] + 1, "only two read pairs changed");
        for (var i = 0; i < changed.Count; i++)
        {
            Check(changed[i].labels.SequenceEqual(originalCode[i].labels), "branch labels preserved");
            Check(changed[i].blocks.SequenceEqual(originalCode[i].blocks), "exception blocks preserved");
        }
        var source = differences[0];
        Check(changed[source].opcode == OpCodes.Nop && changed[source + 1].opcode == OpCodes.Call, "gate rewritten");
        Reject(() => NativePolarPatch.Rewrite(new CodeInstruction[0], projection), "missing gate rejected");
        Reject(() => NativePolarPatch.Rewrite(originalCode.Concat(originalCode), projection), "duplicate gate rejected");
        Reject(() => NativePolarPatch.Rewrite(changed, projection), "already modified gate rejected");
        Reject(() => NativePolarPatch.Rewrite(originalCode, typeof(Program).GetMethod("ForeignPrefix", BindingFlags.NonPublic | BindingFlags.Static)), "wrong target rejected");

        // Install and remove the actual Robur method in this disposable console process.
        // Does not invoke a Robur command, create a CadView, or touch a host process.
        NativePolarPatch.Enable();
        Check(NativePolarPatch.Enabled, "actual SDK patch enabled");
        TestHelper();
        NativeProjectionTests.Run(projection);
        NativeRotationTests.Run(projection);
        var candidateOriginal = PatchProcessor.GetOriginalInstructions(projection, new DynamicMethod("candidateLabels", typeof(void), Type.EmptyTypes).GetILGenerator());
        var candidateChanged = NativePolarPatch.RewriteCandidates(candidateOriginal, projection);
        Check(candidateChanged.Count == candidateOriginal.Count + 11, "candidate filter adds only 11 instructions");
        Reject(() => NativePolarPatch.RewriteCandidates(new CodeInstruction[0], projection), "missing candidate loops rejected");
        Reject(() => NativePolarPatch.RewriteCandidates(candidateOriginal.Concat(candidateOriginal), projection), "duplicate candidate loops rejected");
        Reject(() => NativePolarPatch.RewriteCandidates(candidateChanged, projection), "duplicate candidate filtering rejected");
        NativePolarPatch.Enable();
        Check(Harmony.GetPatchInfo(target).Transpilers.Count(p => p.owner == NativePolarPatch.Owner) == 1, "no double patch");
        NativePolarPatch.Disable();
        Check(!NativePolarPatch.Enabled, "disabled");
        Check(Harmony.GetPatchInfo(target) == null || !Harmony.GetPatchInfo(target).Owners.Contains(NativePolarPatch.Owner), "own detour removed");
        Check(Harmony.GetPatchInfo(projection) == null || !Harmony.GetPatchInfo(projection).Owners.Contains(NativePolarPatch.Owner), "candidate detour removed");
        NativePolarPatch.Disable();
        NativePolarPatch.Enable();
        NativePolarPatch.Disable();
        Check(!NativePolarPatch.Enabled, "re-enable / disable cycle");

        var foreign = new Harmony("polar-test-foreign");
        try
        {
            foreign.Patch(target, prefix: new HarmonyMethod(typeof(Program), "ForeignPrefix"));
            Reject(() => NativePolarPatch.Enable(), "foreign patch rejected");
            Check(!NativePolarPatch.Enabled, "foreign refusal leaves disabled");
        }
        finally { foreign.Unpatch(target, HarmonyPatchType.All, "polar-test-foreign"); }
        try
        {
            foreign.Patch(projection, prefix: new HarmonyMethod(typeof(Program), "ForeignPrefix"));
            Reject(() => NativePolarPatch.Enable(), "foreign projection patch rejected");
            Check(!NativePolarPatch.Enabled, "foreign projection refusal leaves disabled");
            Check(Harmony.GetPatchInfo(target) == null || !Harmony.GetPatchInfo(target).Owners.Contains(NativePolarPatch.Owner), "foreign refusal does not open shared gate");
        }
        finally { foreign.Unpatch(projection, HarmonyPatchType.All, "polar-test-foreign"); }
        Console.WriteLine("SDK=" + assembly.FullName + "; native projection tested; host commands NOT executed");
    }

    private static void ForeignPrefix() { }
    private static void TestHelper()
    {
        var helper = typeof(NativePolarPatch).GetMethod("PolarGateFlags", BindingFlags.NonPublic | BindingFlags.Static);
        var previousPolar = DraftingSettings.PolarTracking;
        try
        {
            foreach (var polar in new[] { false, true })
            foreach (var hasBase in new[] { false, true })
            foreach (var flags in new[] { ObjectSnapFlags.None, ObjectSnapFlags.EndPoint, ObjectSnapFlags.Nearest })
            {
                DraftingSettings.PolarTracking = polar;
                var snaps = new ObjectSnaps(flags);
                var args = new ObjectSnapEventArgs(snaps, new Topomatic.Cad.Foundation.Vector3D(),
                    hasBase ? (Topomatic.Cad.Foundation.Vector3D?)new Topomatic.Cad.Foundation.Vector3D() : null,
                    new Topomatic.Cad.Foundation.BoundingBox2D());
                var result = (ObjectSnapFlags)helper.Invoke(null, new object[] { args });
                Check(result == NativePolarPatch.GateFlags(flags, true, polar, hasBase), "actual helper result");
                Check(snaps.Flags == flags, "request flags not mutated");
            }
        }
        finally { DraftingSettings.PolarTracking = previousPolar; }
    }
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new Exception("FAIL " + message);
    }
    private static void Reject(Action action, string message)
    {
        var rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
}

namespace RoburPseudoCommands
{
    internal static class Logger
    {
        internal static void Info(string message) { Console.WriteLine(message); }
    }
}
