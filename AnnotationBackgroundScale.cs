using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using HarmonyLib;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.View;
using Topomatic.Cad.View.Hints;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;
using Topomatic.Dwg.Layer;
using Topomatic.Maps.Entities;

namespace RoburPseudoCommands
{
    public partial class Module
    {
        [cmd("pseudo_annotation_background_scale")]
        public void AnnotationBackgroundScaleCommand()
        {
            AnnotationBackgroundScale.Execute(CadView);
        }
    }

    internal static class AnnotationBackgroundScale
    {
        internal const double DefaultScale = 1.05;
        internal const double MinimumScale = 1.0;
        internal const double MaximumScale = 5.0;

        internal static bool Execute(CadView cadView)
        {
            if (cadView == null)
            {
                MessageBox.Show("Нет активного окна чертежа.", "Коэффициент фона аннотаций",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!AnnotationBackgroundScalePatch.Enabled)
            {
                MessageBox.Show("Заплатка коэффициента фона не была подключена. Перезапустите Robur и проверьте журнал плагина.",
                    "Коэффициент фона аннотаций", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var selection = cadView.SelectionSet;
            var selected = SnapshotSelection(selection);
            var leaders = selected.OfType<MapsLeaderEntity>().Distinct().ToList();
            var skipped = selected.Count - leaders.Count;

            if (leaders.Count == 0)
            {
                var result = selection.SelectObjectsAtScreen(
                    value => value is MapsLeaderEntity,
                    "Выберите мультивыноски Robur:",
                    new string[0]);
                if (result == GetPointResult.Cancel)
                    return true;

                selected = SnapshotSelection(selection);
                leaders = selected.OfType<MapsLeaderEntity>().Distinct().ToList();
                skipped = selected.Count - leaders.Count;
                if (leaders.Count == 0)
                {
                    MessageBox.Show("Подходящие мультивыноски Robur не выбраны.",
                        "Коэффициент фона аннотаций", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }
            }

            var scale = PluginSettings.GetAnnotationBackgroundScale();
            var inputResult = CadCursors.GetDoubleWithDefault(cadView, ref scale,
                "Коэффициент перекрытия фона:", new string[0]);
            if (inputResult == GetPointResult.Cancel)
                return true;

            if (!IsValidScale(scale))
            {
                MessageBox.Show("Введите коэффициент от 1,00 до 5,00.",
                    "Коэффициент фона аннотаций", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var layer = DrawingLayer.GetDrawingLayer(cadView);
            var drawing = layer == null ? null : layer.Drawing;
            if (drawing == null)
            {
                MessageBox.Show("Не удалось получить активный чертёж.", "Коэффициент фона аннотаций",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            PluginSettings.SetAnnotationBackgroundScale(scale);
            var changed = 0;
            var regeneratedTexts = 0;
            var trackedLeaders = 0;
            drawing.BeginUpdate("Коэффициент фона аннотаций");
            try
            {
                foreach (var leader in leaders)
                {
                    AnnotationBackgroundScalePatch.StoreScale(leader, scale);
                    trackedLeaders += AnnotationBackgroundScalePatch.TrackDynamicLeaders(leader, scale);
                    regeneratedTexts += AnnotationBackgroundScalePatch.ApplyScale(leader, scale);
                    leader.Invalidate();
                    changed++;
                }
            }
            finally
            {
                drawing.EndUpdate();
            }

            cadView.Unlock();
            cadView.Invalidate();
            Logger.Info("annotation background scale applied scale=" +
                scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                " changed=" + changed + " skipped=" + skipped);
            Logger.Info("annotation background scale regenerated texts=" + regeneratedTexts);
            Logger.Info("annotation background scale tracked dynamic leaders=" + trackedLeaders +
                " recreateRecoveries=" + AnnotationBackgroundScalePatch.RecreateRecoveries);
            MessageBox.Show("Изменено: " + changed + Environment.NewLine + "Пропущено: " + skipped,
                "Коэффициент фона аннотаций", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        internal static bool IsValidScale(double scale)
        {
            return !double.IsNaN(scale) && !double.IsInfinity(scale) &&
                scale >= MinimumScale && scale <= MaximumScale;
        }

        private static List<object> SnapshotSelection(IEnumerable selection)
        {
            return selection == null ? new List<object>() : selection.Cast<object>().ToList();
        }
    }

    internal static class AnnotationBackgroundScalePatch
    {
        internal const string Owner = "RoburPseudoCommands.AnnotationBackgroundScale.v1";
        private const string DictionaryName = "RoburPseudoCommands";
        private const string ScaleName = "AnnotationBackgroundScale";
        private static volatile bool _enabled;
        private static MethodInfo _target;
        private static MethodInfo _recreateTarget;
        private static readonly ConditionalWeakTable<DwgDynamicLeader, ScaleHolder> DynamicScales =
            new ConditionalWeakTable<DwgDynamicLeader, ScaleHolder>();
        private static readonly object DynamicScalesSync = new object();
        private static int _recreateRecoveries;
        [ThreadStatic]
        private static bool _insideRecreatePostfix;

        internal static bool Enabled { get { return _enabled; } }
        internal static int RecreateRecoveries { get { return Volatile.Read(ref _recreateRecoveries); } }

        private sealed class ScaleHolder
        {
            internal double Value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Enable()
        {
            if (_enabled) return;

            var signature = new[] { typeof(IList<DwgEntity>), typeof(LayoutEntityEventArgs) };
            var target = typeof(MapsLeaderEntity).GetMethod("Layout",
                BindingFlags.Instance | BindingFlags.Public, null, signature, null);
            if (target == null || target.ReturnType != typeof(void))
                throw new NotSupportedException("Сигнатура MapsLeaderEntity.Layout не совпала.");
            var recreate = typeof(DwgDynamicLeader).GetMethod("Recreate",
                BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (recreate == null || recreate.ReturnType != typeof(void))
                throw new NotSupportedException("Сигнатура DwgDynamicLeader.Recreate не совпала.");

            var harmony = new Harmony(Owner);
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null || !info.Postfixes.Any(p => p.owner == Owner))
                    harmony.Patch(target, postfix: new HarmonyMethod(
                        typeof(AnnotationBackgroundScalePatch), nameof(AfterLayout)));
                var recreateInfo = Harmony.GetPatchInfo(recreate);
                if (recreateInfo == null || !recreateInfo.Postfixes.Any(p => p.owner == Owner))
                    harmony.Patch(recreate, postfix: new HarmonyMethod(
                        typeof(AnnotationBackgroundScalePatch), nameof(AfterDynamicLeaderRecreate)));

                info = Harmony.GetPatchInfo(target);
                recreateInfo = Harmony.GetPatchInfo(recreate);
                if (info == null || info.Postfixes.Count(p => p.owner == Owner) != 1 ||
                    recreateInfo == null || recreateInfo.Postfixes.Count(p => p.owner == Owner) != 1)
                    throw new InvalidOperationException("Harmony не подтвердил обе ступени заплатки коэффициента фона.");

                _target = target;
                _recreateTarget = recreate;
                _enabled = true;
                Logger.Info("annotation background scale patch enabled maps=" +
                    typeof(MapsLeaderEntity).Assembly.GetName().Version + " layoutTarget=" +
                    target.MetadataToken.ToString("X8") + " recreateTarget=" + recreate.MetadataToken.ToString("X8"));
            }
            catch
            {
                _enabled = false;
                harmony.Unpatch(target, HarmonyPatchType.Postfix, Owner);
                harmony.Unpatch(recreate, HarmonyPatchType.Postfix, Owner);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Disable()
        {
            _enabled = false;
            if (_target != null)
                new Harmony(Owner).Unpatch(_target, HarmonyPatchType.Postfix, Owner);
            if (_recreateTarget != null)
                new Harmony(Owner).Unpatch(_recreateTarget, HarmonyPatchType.Postfix, Owner);
        }

        private static void AfterLayout(MapsLeaderEntity __instance, IList<DwgEntity> __0)
        {
            try
            {
                double scale;
                if (!_enabled || !TryReadScale(__instance, out scale)) return;
                TrackDynamicLeaders(__0, scale);
                ApplyScale(__0, scale);
            }
            catch (Exception ex)
            {
                Logger.Error("annotation background scale layout restore failed", ex);
            }
        }

        private static void AfterDynamicLeaderRecreate(DwgDynamicLeader __instance)
        {
            if (!_enabled || _insideRecreatePostfix) return;
            double scale;
            try
            {
                // MapsLeaderEntity itself owns the generated text in the normal drawing path.
                // Layout-created DwgLeader instances still use the weak-table fallback.
                var mapsLeader = __instance as MapsLeaderEntity;
                if (mapsLeader != null)
                {
                    if (!TryReadScale(mapsLeader, out scale)) return;
                }
                else if (!TryGetTrackedScale(__instance, out scale)) return;
                _insideRecreatePostfix = true;
                if (ApplyScale(__instance, scale) > 0)
                    Interlocked.Increment(ref _recreateRecoveries);
            }
            catch (Exception ex)
            {
                Logger.Error("annotation background scale recreate restore failed", ex);
            }
            finally
            {
                _insideRecreatePostfix = false;
            }
        }

        internal static void StoreScale(MapsLeaderEntity leader, double scale)
        {
            if (leader == null) throw new ArgumentNullException("leader");
            if (!AnnotationBackgroundScale.IsValidScale(scale))
                throw new ArgumentOutOfRangeException("scale");

            if (!leader.HasExtensionDictionary)
                leader.CreateExtensionDictionary();
            var root = leader.GetExtensionDictionary();
            var plugin = root.GetDictionary(DictionaryName) ?? root.AddDictionary(DictionaryName);
            plugin.SetDouble(ScaleName, scale);
        }

        internal static bool TryReadScale(MapsLeaderEntity leader, out double scale)
        {
            scale = AnnotationBackgroundScale.DefaultScale;
            if (leader == null || !leader.HasExtensionDictionary) return false;
            var root = leader.GetExtensionDictionary();
            var plugin = root == null ? null : root.GetDictionary(DictionaryName);
            if (plugin == null) return false;
            var value = plugin.GetDouble(ScaleName, double.NaN);
            if (!AnnotationBackgroundScale.IsValidScale(value)) return false;
            scale = value;
            return true;
        }

        internal static int ApplyScale(IEnumerable<DwgEntity> entities, double scale)
        {
            if (entities == null || !AnnotationBackgroundScale.IsValidScale(scale)) return 0;
            var changed = 0;
            foreach (var entity in entities)
            {
                var text = entity as DwgMText;
                if (text != null)
                {
                    text.FillBoxScale = scale;
                    text.Regen(EventArgs.Empty);
                    changed++;
                }

                var complex = entity as DwgComplexEntity;
                if (complex != null)
                    changed += ApplyScale(complex, scale);
            }
            return changed;
        }

        internal static int TrackDynamicLeaders(IEnumerable<DwgEntity> entities, double scale)
        {
            if (entities == null || !AnnotationBackgroundScale.IsValidScale(scale)) return 0;
            var tracked = 0;
            foreach (var entity in entities)
            {
                var leader = entity as DwgLeader;
                if (leader != null)
                {
                    lock (DynamicScalesSync)
                    {
                        ScaleHolder holder;
                        if (!DynamicScales.TryGetValue(leader, out holder))
                        {
                            holder = new ScaleHolder();
                            DynamicScales.Add(leader, holder);
                        }
                        holder.Value = scale;
                    }
                    tracked++;
                }

                var complex = entity as DwgComplexEntity;
                if (complex != null)
                    tracked += TrackDynamicLeaders(complex, scale);
            }
            return tracked;
        }

        internal static bool TryGetTrackedScale(DwgDynamicLeader leader, out double scale)
        {
            scale = AnnotationBackgroundScale.DefaultScale;
            if (leader == null) return false;
            lock (DynamicScalesSync)
            {
                ScaleHolder holder;
                if (!DynamicScales.TryGetValue(leader, out holder) ||
                    !AnnotationBackgroundScale.IsValidScale(holder.Value)) return false;
                scale = holder.Value;
                return true;
            }
        }
    }
}
