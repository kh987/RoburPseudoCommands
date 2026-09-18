using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using RoburPseudoCommands;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;

internal static class NativeRotationTests
{
    private static int _checks;
    internal static void Run(MethodInfo projection)
    {
        var oldPolar = DraftingSettings.PolarTracking;
        var oldStep = DraftingSettings.PolarTrackingAngleStep;
        var oldAdditional = DraftingSettings.AdditionalAngles;
        var oldAdditionalEnabled = DraftingSettings.PolarTrackingAdditionalAngles;
        var view = (CadView)FormatterServices.GetUninitializedObject(typeof(CadView));
        // No native window or CAD document is created. Populate only fields used by native ray generation.
        GC.SuppressFinalize(view);
        var layer = FormatterServices.GetUninitializedObject(projection.DeclaringType);
        foreach (var f in projection.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (typeof(IList).IsAssignableFrom(f.FieldType) && !f.FieldType.IsArray)
                f.SetValue(layer, Activator.CreateInstance(f.FieldType));
        var viewGetter = typeof(CadViewLayer).GetProperty("CadView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod(true);
        Field(viewGetter).SetValue(layer, view);
        var first = typeof(CadView).GetProperty("FirstLinePoint");
        var layerField = PatchProcessor.GetOriginalInstructions(first.GetSetMethod()).Where(c => c.opcode == System.Reflection.Emit.OpCodes.Ldfld)
            .Select(c => (FieldInfo)c.operand).Single();
        layerField.SetValue(view, layer);
        var lineType = projection.GetMethodBody().LocalVariables[3].LocalType;
        var lines = (IList)projection.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(f => f.FieldType.IsGenericType && f.FieldType.GetGenericArguments()[0] == lineType && typeof(IList).IsAssignableFrom(f.FieldType)).GetValue(layer);
        var endpoint = (MethodInfo)projection.Module.ResolveMethod(projection.MetadataToken + 3);
        var origin = new Vector3D(120, -80, 7);
        Func<int, double> angle = i =>
        {
            var point = (Vector3D)endpoint.Invoke(lines[i], null);
            Check(Math.Abs(point.Z - origin.Z) < 1e-9, "Z preserved");
            return Math.Atan2(point.Y - origin.Y, point.X - origin.X);
        };
        try
        {
            DraftingSettings.PolarTracking = true;
            DraftingSettings.PolarTrackingAngleStep = Math.PI / 2;
            DraftingSettings.AdditionalAngles = new[] { Math.PI / 6 };
            DraftingSettings.PolarTrackingAdditionalAngles = true;
            var screenshotDegrees = 343d + 34d / 60 + 6d / 3600;
            // Both equivalent representations; the dialog alone does not establish the SDK's sign.
            var screenshotRadians = screenshotDegrees * Math.PI / 180;
            foreach (var rotation in new[] { screenshotRadians - 2 * Math.PI, screenshotRadians,
                -(screenshotRadians - 2 * Math.PI), 0d, .3, -.3, Math.PI / 2 })
            foreach (var ucs in new[] { 0d, .2 })
            {
                Set(view, "ScreenRotation", rotation);
                Set(view, "UCSRotation", ucs);
                var s = view.ScreenRotation; var u = view.UCSRotation; // SDK stores these as float.
                NativePolarPatch.SetRotationEnabled(false);
                view.FirstLinePoint = origin;
                EqualAngle(angle(0), u, "mode off preserves native UCS");
                NativePolarPatch.SetRotationEnabled(true);
                view.FirstLinePoint = origin;
                Check(lines.Count == 5, "four principal plus additional native ray");
                // Independent screen-space oracle, including native Y reflections and pixel mapping.
                foreach (var dimensions in new[] { new[] { 800, 600 }, new[] { 1920, 1080 } })
                foreach (var scale in new[] { .1, 25d })
                {
                    EqualAngle(ScreenAngle(angle(0), s, scale, dimensions[0], dimensions[1]), u,
                        "full view chain: principal ray screen angle");
                    EqualAngle(ScreenAngle(angle(1), s, scale, dimensions[0], dimensions[1]), u + Math.PI / 2,
                        "full view chain: perpendicular ray screen angle");
                    EqualAngle(ScreenAngle(angle(4), s, scale, dimensions[0], dimensions[1]), u + Math.PI / 6,
                        "full view chain: additional ray screen angle");
                }
                EqualAngle(angle(0), u - s, "principal ray corrected");
                EqualAngle(angle(4), u - s + Math.PI / 6, "additional ray corrected");
                var before = lines.Count;
                var generator = (MethodInfo)projection.Module.ResolveMethod(projection.MetadataToken - 22);
                generator.Invoke(layer, new object[] { origin, false, true, 0, ObjectSnapFlags.EndPoint });
                EqualAngle(angle(before), u, "object tracking rays not rotated");
                NativePolarPatch.RefreshRotationView(view);
                Set(view, "ScreenRotation", rotation + .15);
                NativePolarPatch.RefreshRotationView(view);
                EqualAngle(angle(0), u - view.ScreenRotation, "existing rays rebuilt after view rotation");
                EqualAngle(ScreenAngle(angle(0), view.ScreenRotation, 1, 800, 600), u, "refreshed ray screen angle");
                Check(lines.Count == 5, "refresh does not duplicate rays");
                NativePolarPatch.SetRotationEnabled(false);
                NativePolarPatch.RefreshRotationView(view);
                EqualAngle(angle(0), u, "toggle off restores native rays");
                Check(view.FirstLinePoint.Value.Equals(origin), "base point preserved");
            }
            NativePolarPatch.SetRotationEnabled(true);
            NativePolarPatch.RefreshRotationView(view);
            NativePolarPatch.Disable();
            EqualAngle(angle(0), view.UCSRotation, "disable restores existing rays before detaching");
            NativePolarPatch.Enable();
            var gen = (MethodInfo)projection.Module.ResolveMethod(projection.MetadataToken - 22);
            var original = PatchProcessor.GetOriginalInstructions(gen);
            Check(NativePolarPatch.RewriteRotation(original).Count() == original.Count + 4, "two getter replacements only");
            var refused = false;
            try { NativePolarPatch.RewriteRotation(new CodeInstruction[0]).ToList(); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "unknown generator rejected");
            var foreign = new Harmony("rotation-test-foreign");
            NativePolarPatch.Disable();
            try
            {
                foreign.Patch(gen, prefix: new HarmonyMethod(typeof(NativeRotationTests), nameof(NoOp)));
                refused = false;
                try { NativePolarPatch.Enable(); } catch (InvalidOperationException) { refused = true; }
                Check(refused && !NativePolarPatch.Enabled, "foreign generator patch refused");
            }
            finally { foreign.Unpatch(gen, HarmonyPatchType.All, "rotation-test-foreign"); NativePolarPatch.Enable(); }
            Console.WriteLine("PASS native rotation checks=" + _checks + "; generation/additional/UCS/native-matrix/refresh/rollback; no window or document");
        }
        finally
        {
            NativePolarPatch.SetRotationEnabled(false);
            NativePolarPatch.RefreshRotationView(view);
            DraftingSettings.PolarTracking = oldPolar; DraftingSettings.PolarTrackingAngleStep = oldStep;
            DraftingSettings.AdditionalAngles = oldAdditional; DraftingSettings.PolarTrackingAdditionalAngles = oldAdditionalEnabled;
        }
    }
    private static void NoOp() { }
    private static double ScreenAngle(double worldAngle, double rotation, double scale, int width, int height)
    {
        // Exact planar chain read from CadView's projection-matrix update and ProjectPoint.
        // ScreenRatio=1 (undistorted plan); translations cancel when comparing two projected points.
        var matrix = Matrix.CreateOrthographicOffCenter(-scale, scale, scale, -scale, -10000, 10000);
        matrix *= Matrix.CreateTranslation(-12 / scale, 8 / scale, 0);
        matrix *= Matrix.CreateScale(1, 1, 1);
        matrix *= Matrix.CreateRotationZ(-rotation);
        matrix *= Matrix.CreateScale(2d / width, -2d / height, 1);
        var a = Vector3D.Transform(new Vector3D(120, -80, 0), matrix);
        var b = Vector3D.Transform(new Vector3D(120 + Math.Cos(worldAngle), -80 + Math.Sin(worldAngle), 0), matrix);
        // ProjectPoint maps X=(X+1)*width/2, Y=(-Y+1)*height/2.
        // Negate pixel delta-Y again when reporting the visible mathematical angle.
        return Math.Atan2((b.Y - a.Y) * height / 2, (b.X - a.X) * width / 2);
    }
    private static FieldInfo Field(MethodInfo getter)
    {
        return PatchProcessor.GetOriginalInstructions(getter).Where(c => c.opcode == System.Reflection.Emit.OpCodes.Ldfld)
            .Select(c => (FieldInfo)c.operand).Single();
    }
    private static void Set(CadView view, string property, double value)
    {
        var f = Field(typeof(CadView).GetProperty(property).GetGetMethod());
        f.SetValue(view, Convert.ChangeType(value, f.FieldType));
    }
    private static void EqualAngle(double actual, double expected, string name)
    {
        Check(Math.Abs(Math.Atan2(Math.Sin(actual - expected), Math.Cos(actual - expected))) < 1e-6, name);
    }
    private static void Check(bool value, string name)
    {
        _checks++; if (!value) throw new Exception("FAIL rotation: " + name);
    }
}
