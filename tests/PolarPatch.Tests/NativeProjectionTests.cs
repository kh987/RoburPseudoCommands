using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using RoburPseudoCommands;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;

internal static class NativeProjectionTests
{
    private static int _checks;
    internal static void Run(MethodInfo projection)
    {
        var previous = DraftingSettings.PolarTracking;
        try
        {
            DraftingSettings.PolarTracking = true;
            var basePoint = new Vector3D(0, 0, 0);
            var layer = FormatterServices.GetUninitializedObject(projection.DeclaringType);
            var fields = PatchProcessor.GetOriginalInstructions(projection)
                .Where(i => i.operand is FieldInfo).Select(i => (FieldInfo)i.operand).Distinct().ToArray();
            var lineType = projection.GetMethodBody().LocalVariables[3].LocalType;
            var lineField = fields.Single(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>) &&
                f.FieldType.GetGenericArguments()[0] == lineType);
            var circleField = fields.Single(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>) && f != lineField);
            var lines = (IList)Activator.CreateInstance(lineField.FieldType);
            var circles = (IList)Activator.CreateInstance(circleField.FieldType);
            lineField.SetValue(layer, lines);
            circleField.SetValue(layer, circles);

            Action<Vector3D, Vector3D, int, ObjectSnapFlags> line = (start, end, index, kind) =>
            {
                var item = Activator.CreateInstance(lineType);
                Set(projection, 2, item, start);
                Set(projection, 4, item, end);
                Set(projection, 6, item, Line2D.CreateLine2D(start.Pos, end.Pos));
                Set(projection, 8, item, index);
                Set(projection, 10, item, kind);
                lines.Add(item);
            };
            Func<Vector3D, ObjectSnapFlags, bool, Vector3D?> project = (point, flags, hasBase) =>
            {
                var bounds = new BoundingBox2D(new Vector2D(point.X - 1, point.Y - 1), new Vector2D(point.X + 1, point.Y + 1));
                var args = new ObjectSnapEventArgs(new ObjectSnaps(flags), point, hasBase ? (Vector3D?)basePoint : null, bounds);
                var values = new object[] { bounds, args, new Vector3D() };
                var found = (bool)projection.Invoke(layer, values);
                Check(args.SourceSnaps.Flags == flags, "native projection preserves request flags");
                return found ? (Vector3D?)values[2] : null;
            };
            line(new Vector3D(5, 0, 0), new Vector3D(15, 10, 0), 0, ObjectSnapFlags.EndPoint);
            Check(!project(new Vector3D(10, 5.05, 0), ObjectSnapFlags.None, true).HasValue, "off-angle object rejected");
            Check(project(new Vector3D(10, 5.05, 0), ObjectSnapFlags.Nearest, true).HasValue, "same object retained with Nearest");
            Check(!project(new Vector3D(10, 5.05, 0), ObjectSnapFlags.EndPoint, true).HasValue, "other flags do not enable Nearest");
            lines.Clear();
            line(basePoint, new Vector3D(100, 0, 0), -1, ObjectSnapFlags.MarkerSnap);
            line(new Vector3D(0, .1, 0), new Vector3D(100, .1, 0), 0, ObjectSnapFlags.EndPoint);
            var polar = project(new Vector3D(10, .11, 0), ObjectSnapFlags.None, true);
            Check(polar.HasValue && Math.Abs(polar.Value.Y) < 1e-9, "polar ray wins despite closer object");
            var nearest = project(new Vector3D(10, .11, 0), ObjectSnapFlags.Nearest, true);
            Check(nearest.HasValue && Math.Abs(nearest.Value.Y - .1) < 1e-9, "Nearest on retains closer object");
            DraftingSettings.PolarTracking = false;
            Check(!project(new Vector3D(10, .11, 0), ObjectSnapFlags.None, true).HasValue, "polar off rejects stale polar rays");
            DraftingSettings.PolarTracking = true;
            Check(!project(new Vector3D(10, .11, 0), ObjectSnapFlags.None, false).HasValue, "no base rejects polar rays");
            lines.Clear();
            line(new Vector3D(0, 5, 0), new Vector3D(100, 5, 0), -1, ObjectSnapFlags.MarkerSnap);
            Check(!project(new Vector3D(10, 5.05, 0), ObjectSnapFlags.None, true).HasValue, "old base rejected");
            lines.Clear();
            line(basePoint, new Vector3D(100, 100, 0), 0, ObjectSnapFlags.EndPoint);
            Check(!project(new Vector3D(10, 10.05, 0), ObjectSnapFlags.None, true).HasValue, "object at same base rejected");
            lines.Clear();

            var circleType = circleField.FieldType.GetGenericArguments()[0];
            var circle = Activator.CreateInstance(circleType);
            var setter = (MethodInfo)projection.Module.ResolveMethod(projection.MetadataToken + 14);
            var arcType = setter.GetParameters()[0].ParameterType;
            var arc = Activator.CreateInstance(arcType);
            arcType.GetField("Center").SetValue(arc, new Vector3D(10, 10, 0));
            arcType.GetField("Radius").SetValue(arc, 5d);
            arcType.GetField("StartAngle").SetValue(arc, 0d);
            arcType.GetField("EndAngle").SetValue(arc, Math.PI * 2);
            setter.Invoke(circle, new[] { arc });
            circles.Add(circle);
            Check(!project(new Vector3D(13, 14.05, 0), ObjectSnapFlags.None, true).HasValue, "circle rejected without Nearest");
            Check(project(new Vector3D(13, 14.05, 0), ObjectSnapFlags.Nearest, true).HasValue, "circle retained with Nearest");

            NativePolarPatch.Disable();
            Check(project(new Vector3D(13, 14.05, 0), ObjectSnapFlags.None, true).HasValue, "unpatched shared method reproduces circle attraction");
            lines.Clear(); circles.Clear();
            line(new Vector3D(5, 0, 0), new Vector3D(15, 10, 0), 0, ObjectSnapFlags.EndPoint);
            Check(project(new Vector3D(10, 5.05, 0), ObjectSnapFlags.None, true).HasValue, "unpatched shared method reproduces line attraction");
            NativePolarPatch.Enable();
            Check(!project(new Vector3D(10, 5.05, 0), ObjectSnapFlags.None, true).HasValue, "re-enable rejects off-angle object again");
            Console.WriteLine("PASS native projection regression checks=" + _checks + "; line/circle and mixed candidates; no CadView/host/model created");
        }
        finally { DraftingSettings.PolarTracking = previous; }
    }
    private static void Set(MethodInfo projection, int delta, object item, object value)
    {
        ((MethodInfo)projection.Module.ResolveMethod(projection.MetadataToken + delta)).Invoke(item, new[] { value });
    }
    private static void Check(bool result, string name)
    {
        _checks++;
        if (!result) throw new Exception("FAIL native geometry: " + name);
    }
}
