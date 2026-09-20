using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HarmonyLib;

internal static class Program
{
    private static Assembly plugin;
    private static string fixture;
    private static int checks;
    private static int annotationRegens;
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string name) { return plugin.GetType("RoburPseudoCommands." + name, true); }
    private static object Call(string type, string method, params object[] args) { return T(type).GetMethod(method, Flags).Invoke(null, args); }
    private static bool Saved(string name) { return (bool)Call("PluginSettings", name); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static bool Redirect(ref string __result) { __result = fixture; return false; }
    private static void CountAnnotationRegen() { annotationRegens++; }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
            fixture = Path.Combine(root, "tests", "Ui.Tests", "artifacts", "run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixture);
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
                var name = new AssemblyName(e.Name).Name;
                var path = Path.Combine(@"C:\Program Files\Topomatic Robur Genplan 16.0", name + ".dll");
                return name.StartsWith("Topomatic.") && File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            plugin = Assembly.LoadFrom(Path.Combine(root, "bin", "Release", "net48", "RoburPseudoCommands.dll"));
            var harness = new Harmony("RoburPseudoCommands.UiTests");
            harness.Patch(T("UserDataPaths").GetMethod("GetPluginDirectory", Flags), prefix: new HarmonyMethod(typeof(Program), "Redirect"));
            Check(plugin.GetType("RoburPseudoCommands.PolarDiagnostics") == null, "temporary tracing excluded");
            TestProtectedMenuCommands();
            TestAnnotationBackgroundScale();
            var settingsPath = (string)T("PluginSettings").GetProperty("SettingsPath", Flags).GetValue(null);
            Check(settingsPath.StartsWith(fixture, StringComparison.OrdinalIgnoreCase), "isolated settings");
            Application.EnableVisualStyles();
            using (var form = (Form)Activator.CreateInstance(T("AliasEditorForm"), Flags, null, new object[] { null }, null))
            {
                Render(form, new Size(1120, 680), "default.png");
                Render(form, new Size(900, 520), "minimum.png");
                form.Scale(new SizeF(1.5f, 1.5f));
                Render(form, new Size(1350, 780), "scaled150.png");
            }
            Console.WriteLine("PASS UI/settings checks=" + checks + " renders=" + fixture);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void TestProtectedMenuCommands()
    {
        var argumentBuilder = T("ProtectedCommandArguments").GetMethod("Build", Flags);
        var requiresPacked = T("ProtectedCommandArguments").GetMethod("RequiresPackedHandler", Flags);
        foreach (var command in new[] { "options", "dsettings", "smdx_manager" })
        {
            var original = new object[] { "sample" };
            var packed = (object[])argumentBuilder.Invoke(null, new object[] { command, original });
            Check((bool)requiresPacked.Invoke(null, new object[] { command }),
                "packed handler detected " + command);
            Check(packed.Length == 1 && ReferenceEquals(packed[0], original),
                "command arguments packed " + command);
        }
        foreach (var command in new[] {
            "smt_manager", "materials_settings_manager", "toolbar_settings",
            "point_sign_library", "area_sign_library", "linear_sign_library", "models_library"
        })
        {
            var original = new object[0];
            var unchanged = (object[])argumentBuilder.Invoke(null, new object[] { command, original });
            Check(!(bool)requiresPacked.Invoke(null, new object[] { command }),
                "parameterless handler retained " + command);
            Check(ReferenceEquals(unchanged, original), "command arguments unchanged " + command);
        }
    }
    private static void TestAnnotationBackgroundScale()
    {
        Check(Math.Abs((double)Call("PluginSettings", "GetAnnotationBackgroundScale") - 1.05) < 1e-9,
            "annotation scale default 1.05");
        Check((bool)Call("AnnotationBackgroundScale", "IsValidScale", 1.0), "annotation scale lower bound");
        Check((bool)Call("AnnotationBackgroundScale", "IsValidScale", 5.0), "annotation scale upper bound");
        Check(!(bool)Call("AnnotationBackgroundScale", "IsValidScale", 0.99), "annotation scale rejects low");
        Check(!(bool)Call("AnnotationBackgroundScale", "IsValidScale", double.NaN), "annotation scale rejects NaN");
        Call("PluginSettings", "SetAnnotationBackgroundScale", 1.25);
        T("PluginSettings").GetField("_settings", Flags).SetValue(null, null);
        Check(Math.Abs((double)Call("PluginSettings", "GetAnnotationBackgroundScale") - 1.25) < 1e-9,
            "annotation scale persists");

        Call("AnnotationBackgroundScalePatch", "Enable");
        Check((bool)T("AnnotationBackgroundScalePatch").GetProperty("Enabled", Flags).GetValue(null),
            "annotation layout patch enabled");
        var layoutTarget = (MethodInfo)T("AnnotationBackgroundScalePatch").GetField("_target", Flags).GetValue(null);
        var recreateTarget = (MethodInfo)T("AnnotationBackgroundScalePatch").GetField("_recreateTarget", Flags).GetValue(null);
        Check(Harmony.GetPatchInfo(layoutTarget).Postfixes.Count(p => p.owner == "RoburPseudoCommands.AnnotationBackgroundScale.v1") == 1,
            "annotation layout postfix installed once");
        Check(Harmony.GetPatchInfo(recreateTarget).Postfixes.Count(p => p.owner == "RoburPseudoCommands.AnnotationBackgroundScale.v1") == 1,
            "annotation recreate postfix installed once");

        var mtextType = Type.GetType("Topomatic.Dwg.Entities.DwgMText, Topomatic.Dwg", true);
        var lineType = Type.GetType("Topomatic.Dwg.Entities.DwgLine, Topomatic.Dwg", true);
        var entityType = Type.GetType("Topomatic.Dwg.Entities.DwgEntity, Topomatic.Dwg", true);
        var mtext = Activator.CreateInstance(mtextType);
        var line = Activator.CreateInstance(lineType);
        var entities = Array.CreateInstance(entityType, 2);
        entities.SetValue(mtext, 0);
        entities.SetValue(line, 1);
        var regenHarness = new Harmony("RoburPseudoCommands.UiTests.AnnotationRegen");
        var inheritedRegen = mtextType.GetMethod("Regen", Flags);
        var regen = inheritedRegen.DeclaringType.GetMethod("Regen",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        annotationRegens = 0;
        regenHarness.Patch(regen, prefix: new HarmonyMethod(typeof(Program), "CountAnnotationRegen"));
        int changed;
        try
        {
            changed = (int)T("AnnotationBackgroundScalePatch").GetMethod("ApplyScale", Flags)
                .Invoke(null, new object[] { entities, 1.25 });
        }
        finally
        {
            regenHarness.Unpatch(regen, HarmonyPatchType.All, regenHarness.Id);
        }
        Check(changed == 1, "annotation patch changes only MText");
        Check(annotationRegens == 1, "annotation patch regenerates changed MText");
        Check(Math.Abs((double)mtextType.GetProperty("FillBoxScale").GetValue(mtext) - 1.25) < 1e-9,
            "annotation MText coefficient applied");

        var leaderType = Type.GetType("Topomatic.Maps.Entities.MapsLeaderEntity, Topomatic.Maps", true);
        var drawingType = Type.GetType("Topomatic.Dwg.Drawing, Topomatic.Dwg", true);
        var leader = Activator.CreateInstance(leaderType);
        var drawing = Activator.CreateInstance(drawingType);
        leaderType.GetMethod("Prepare").Invoke(leader, new[] { drawing });
        T("AnnotationBackgroundScalePatch").GetMethod("StoreScale", Flags)
            .Invoke(null, new[] { leader, (object)1.25 });
        var readArgs = new object[] { leader, 0.0 };
        Check((bool)T("AnnotationBackgroundScalePatch").GetMethod("TryReadScale", Flags).Invoke(null, readArgs) &&
            Math.Abs((double)readArgs[1] - 1.25) < 1e-9, "annotation scale stored on leader");

        var dwgLeaderType = Type.GetType("Topomatic.Dwg.Entities.DwgLeader, Topomatic.Dwg", true);
        var dynamicLeaderType = Type.GetType("Topomatic.Dwg.Entities.DwgDynamicLeader, Topomatic.Dwg", true);
        var generatedLeader = Activator.CreateInstance(dwgLeaderType);
        var generatedEntities = Array.CreateInstance(entityType, 1);
        generatedEntities.SetValue(generatedLeader, 0);
        var tracked = (int)T("AnnotationBackgroundScalePatch").GetMethod("TrackDynamicLeaders", Flags)
            .Invoke(null, new object[] { generatedEntities, 1.25 });
        var trackedArgs = new object[] { generatedLeader, 0.0 };
        Check(tracked == 1 && (bool)T("AnnotationBackgroundScalePatch").GetMethod("TryGetTrackedScale", Flags)
            .Invoke(null, trackedArgs) && Math.Abs((double)trackedArgs[1] - 1.25) < 1e-9,
            "generated dynamic leader tracked");
        var untrackedLeader = Activator.CreateInstance(dwgLeaderType);
        var untrackedArgs = new object[] { untrackedLeader, 0.0 };
        Check(!(bool)T("AnnotationBackgroundScalePatch").GetMethod("TryGetTrackedScale", Flags)
            .Invoke(null, untrackedArgs), "ordinary dynamic leader remains untracked");
        Check(dynamicLeaderType.IsInstanceOfType(generatedLeader), "DwgLeader is dynamic leader");

        // Reproduce the real ownership shape: MapsLeaderEntity -> MText, no child DwgLeader.
        var complexType = Type.GetType("Topomatic.Dwg.Entities.DwgComplexEntity, Topomatic.Dwg", true);
        var children = (System.Collections.IList)complexType.GetProperty("EntityCollection", Flags).GetValue(leader);
        children.Clear();
        children.Add(mtext);
        var postfix = T("AnnotationBackgroundScalePatch").GetMethod("AfterDynamicLeaderRecreate", Flags);
        var recoveries = T("AnnotationBackgroundScalePatch").GetProperty("RecreateRecoveries", Flags);
        int priorRecoveries = (int)recoveries.GetValue(null);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            // Model native Recreate replacing its text and resetting the coefficient to 1.
            var replacement = Activator.CreateInstance(mtextType);
            mtextType.GetProperty("FillBoxScale").SetValue(replacement, 1.0);
            children.Clear();
            children.Add(replacement);
            postfix.Invoke(null, new[] { leader });
            Check(Math.Abs((double)mtextType.GetProperty("FillBoxScale").GetValue(replacement) - 1.25) < 1e-6,
                "MapsLeader replacement text restored without DwgLeader tracking cycle=" + cycle);
        }
        Check((int)recoveries.GetValue(null) == priorRecoveries + 2, "MapsLeader recoveries counted");
        var plainMapsLeader = Activator.CreateInstance(leaderType);
        var plainChildren = (System.Collections.IList)complexType.GetProperty("EntityCollection", Flags).GetValue(plainMapsLeader);
        var plainText = Activator.CreateInstance(mtextType);
        mtextType.GetProperty("FillBoxScale").SetValue(plainText, 1.0);
        plainChildren.Add(plainText);
        postfix.Invoke(null, new[] { plainMapsLeader });
        Check((double)mtextType.GetProperty("FillBoxScale").GetValue(plainText) == 1.0 &&
            (int)recoveries.GetValue(null) == priorRecoveries + 2, "unmarked MapsLeader untouched");

        Call("AnnotationBackgroundScalePatch", "Disable");
        Check(!(bool)T("AnnotationBackgroundScalePatch").GetProperty("Enabled", Flags).GetValue(null),
            "annotation layout patch disabled");
        Call("PluginSettings", "SetAnnotationBackgroundScale", 1.05);
    }
    private static void Render(Form form, Size size, string file)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000);
        form.Opacity = 0;
        form.Size = size;
        form.Show();
        var advanced = (CheckBox)T("AliasEditorForm").GetField("_advancedCheckBox", Flags).GetValue(form);
        var log = (CheckBox)T("AliasEditorForm").GetField("_logEnabledCheckBox", Flags).GetValue(form);
        Check(!log.Visible, "log hidden in basic UI");
        advanced.Checked = true;
        Check(log.Visible, "log visible in advanced UI");
        advanced.Checked = false;
        form.PerformLayout();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(fixture, file));
        }
        form.Hide();
    }
}
