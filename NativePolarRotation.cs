using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    internal static partial class NativePolarPatch
    {
        private static volatile bool _rotationEnabled;
        private static MethodInfo _generator;
        private static MethodInfo _layerView;
        private static ConditionalWeakTable<CadView, Orientation> _orientations = new ConditionalWeakTable<CadView, Orientation>();
        private static readonly List<WeakReference> RotationViews = new List<WeakReference>();
        private sealed class Orientation
        {
            internal bool Applied, Failed;
            internal double Screen, Ucs;
        }
        internal static bool RotationEnabled { get { return _rotationEnabled; } }
        internal static void SetRotationEnabled(bool value) { _rotationEnabled = value; }

        private static void InstallRotation(Harmony harmony, Profile profile)
        {
            _generator = (MethodInfo)_target.Module.ResolveMethod(profile.TargetToken - 12);
            _layerView = typeof(CadViewLayer).GetProperty("CadView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod(true);
            var expected = profile.Version == "16.0.55.83"
                ? "59A23A701A96D73CCD2731E1FA72DEA4070067ECA1667311607ACDE704B41377"
                : "4BA339885C587DE875E1874FA2992A05841C82892C6E1BC5D0E9A191DB14482F";
            if (_generator.IsStatic || _generator.ReturnType != typeof(void) || _generator.DeclaringType != _target.DeclaringType ||
                !_generator.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Vector3D), typeof(bool), typeof(bool), typeof(int), typeof(ObjectSnapFlags) }))
                throw new NotSupportedException("Сигнатура генератора полярных лучей не совпала.");
            using (var stream = new MemoryStream(_generator.GetMethodBody().GetILAsByteArray()))
                if (Hash(stream) != expected) throw new NotSupportedException("SHA-256 генератора лучей не совпал.");
            var info = Harmony.GetPatchInfo(_generator);
            if (info != null && info.Owners.Any(id => id != Owner)) throw new InvalidOperationException("Генератор уже изменён другим патчем.");
            if (info == null || !info.Transpilers.Any(p => p.owner == Owner))
                harmony.Patch(_generator, transpiler: new HarmonyMethod(typeof(NativePolarPatch), nameof(RewriteRotation)));
        }

        internal static IEnumerable<CodeInstruction> RewriteRotation(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(c => new CodeInstruction(c)).ToList();
            var getter = typeof(CadView).GetProperty("UCSRotation").GetGetMethod();
            if (code.Count(c => Calls(c, getter)) != 2) throw new InvalidOperationException("Ожидалось два чтения угла: основной шаг и дополнительные углы.");
            var result = new List<CodeInstruction>();
            foreach (var c in code)
            {
                if (!Calls(c, getter)) { result.Add(c); continue; }
                var first = new CodeInstruction(OpCodes.Ldarg_S, (byte)4);
                first.labels.AddRange(c.labels); first.blocks.AddRange(c.blocks);
                result.Add(first);
                result.Add(new CodeInstruction(OpCodes.Ldarg_S, (byte)5));
                result.Add(new CodeInstruction(OpCodes.Call, typeof(NativePolarPatch).GetMethod(nameof(PolarRotation), BindingFlags.Static | BindingFlags.NonPublic)));
            }
            return result;
        }

        internal static double CorrectRotation(double ucs, double screen, bool apply, int index, ObjectSnapFlags kind)
        {
            // The complete Robur view chain reflects Y around CreateRotationZ(-ScreenRotation).
            // In the undistorted plan the visible angle is world angle + ScreenRotation.
            // Subtract the view angle; testing the rotation matrix alone gives the wrong sign.
            return apply && index == -1 && kind == ObjectSnapFlags.MarkerSnap &&
                !double.IsNaN(screen) && !double.IsInfinity(screen) ? ucs - screen : ucs;
        }
        private static double PolarRotation(CadView view, int index, ObjectSnapFlags kind)
        {
            return CorrectRotation(view.UCSRotation, view.ScreenRotation, _enabled && _rotationEnabled, index, kind);
        }

        // Runs before snapping. Rebuild only after an orientation/mode change, not on every mouse move.
        // FirstLinePoint setter uses native ClearSnaps, just as Robur's own view/input workflow does.
        private static void RefreshPolarOrientation(object __instance)
        {
            // Preserve the hotfix.16 path when the optional feature has never been used.
            if (!_rotationEnabled && RotationViews.Count == 0) return;
            var view = (CadView)_layerView.Invoke(__instance, null);
            if (view == null || view.IsDisposed) return;
            RefreshRotationView(view);
        }
        internal static void RefreshRotationView(CadView view)
        {
            Orientation state;
            var apply = _enabled && _rotationEnabled;
            if (!_orientations.TryGetValue(view, out state))
            {
                if (!apply) return;
                state = new Orientation();
                _orientations.Add(view, state);
                RotationViews.RemoveAll(w => !w.IsAlive);
                RotationViews.Add(new WeakReference(view));
            }
            if (state.Failed || (!apply && !state.Applied)) return;
            var screen = view.ScreenRotation;
            var ucs = view.UCSRotation;
            if (state.Applied == apply && state.Screen == screen && state.Ucs == ucs) return;
            try
            {
                view.FirstLinePoint = view.FirstLinePoint;
                state.Applied = apply; state.Screen = screen; state.Ucs = ucs;
                Logger.Info("polar orientation refreshed apply=" + apply + " screen=" + screen + " ucs=" + ucs);
            }
            catch (Exception ex)
            {
                state.Failed = true;
                _rotationEnabled = false;
                Logger.Info("polar orientation refresh failed; restart Robur: " + ex);
            }
        }
        private static void RestoreRotationViews()
        {
            foreach (var weak in RotationViews.ToArray())
            {
                var view = weak.Target as CadView;
                if (view != null && !view.IsDisposed) RefreshRotationView(view);
            }
            RotationViews.Clear();
            _orientations = new ConditionalWeakTable<CadView, Orientation>();
        }
        private static void RemoveRotation()
        {
            try { RestoreRotationViews(); }
            finally { if (_generator != null) new Harmony(Owner).Unpatch(_generator, HarmonyPatchType.Transpiler, Owner); }
        }
    }
}
