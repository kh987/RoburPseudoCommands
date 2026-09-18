using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    public partial class Module
    {
        [cmd("pseudo_polar_diagnostics")]
        public void PolarDiagnosticsCommand()
        {
            try
            {
                if (PolarDiagnostics.IsRunning)
                {
                    var path = PolarDiagnostics.Stop("user-stop");
                    MessageBox.Show("Диагностика остановлена. Лог:\n" + path,
                        "Полярное отслеживание");
                    return;
                }

                var view = CadView;
                if (view == null || view.IsDisposed || view.IsGettingValue || view.IsModalEdit)
                {
                    MessageBox.Show("Завершите текущую команду и откройте чертёж.",
                        "Полярное отслеживание");
                    return;
                }

                if (MessageBox.Show("Включить диагностический лог на 10 минут?\n" +
                    "Он содержит координаты курсора и настройки, но не меняет чертёж.\n" +
                    "После закрытия окна постройте полилинию. Для остановки снова выберите этот пункт меню.",
                    "Полярное отслеживание — диагностика", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information) != DialogResult.OK) return;

                PolarDiagnostics.Start(view);
            }
            catch (Exception ex)
            {
                PolarDiagnostics.Stop("start-error");
                Logger.Error("polar diagnostics failed", ex);
                MessageBox.Show("Не удалось включить диагностику:\n" + ex.Message,
                    "Полярное отслеживание");
            }
        }
    }

    // Deliberately read-only: event ordering must be observed before correcting native input.
    internal sealed class PolarDiagnostics : IDisposable
    {
        private static PolarDiagnostics _session;
        private readonly CadView _view;
        private readonly Timer _timer;
        private readonly StreamWriter _writer;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastMove;
        private long _lastSnapshot;
        private long _writtenBytes;
        private long _sequence;
        private int _request;
        private bool _wasGettingValue;
        private string _settings;
        private bool _disposed;
        private const long MaxBytes = 5L * 1024 * 1024;

        private PolarDiagnostics(CadView view, string path)
        {
            _view = view;
            Path = path;
            _timer = new Timer { Interval = 250 };
            _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        }

        internal string Path { get; private set; }
        internal static bool IsRunning { get { return _session != null; } }

        internal static void Start(CadView view)
        {
            if (_session != null) throw new InvalidOperationException("Diagnostics already running.");
            var directory = UserDataPaths.GetPluginDirectory();
            Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "RoburPolar-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
                "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".log");
            var session = new PolarDiagnostics(view, path);
            _session = session;
            try { session.Attach(); }
            catch { Stop("attach-error"); throw; }
        }

        internal static string Stop(string reason)
        {
            var session = _session;
            if (session == null) return string.Empty;
            _session = null;
            try { session.Write("stop reason=" + reason); }
            catch { /* Disk errors must never escape into a Robur input callback. */ }
            finally { session.Dispose(); }
            return session.Path;
        }

        private void Attach()
        {
            var assembly = typeof(Module).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            Write("start readOnly=True version=" + (version == null ? "unknown" : version.InformationalVersion) +
                " dll=" + assembly.Location + " culture=" + CultureInfo.CurrentCulture.Name);
            var sdk = typeof(CadView).Assembly;
            Write("sdk=" + sdk.FullName + " fileVersion=" +
                FileVersionInfo.GetVersionInfo(sdk.Location).FileVersion + " path=" + sdk.Location);
            Write("angleStep is raw SDK value; units are not inferred. accepting is a pre-accept observation, not commit proof.");
            _wasGettingValue = _view.IsGettingValue;
            _settings = Settings();
            Write("settings " + _settings);
            Snapshot("initial");
            CadView.GetValueStarted += OnStarted;
            _view.CurrentCursorPointChanged += OnPointChanged;
            _view.Accepting += OnAccepting;
            CadView.EmptyActionStack += OnEmptyStack;
            _view.Disposed += OnViewDisposed;
            _timer.Tick += OnTick;
            _writer.Flush();
            _timer.Start();
            Logger.Info("polar diagnostics started path='" + Path + "' readOnly=True");
        }

        private void Guard(Action action)
        {
            if (_disposed) return;
            try { action(); }
            catch (Exception ex)
            {
                Logger.Error("polar diagnostics stopped after error", ex);
                Stop("error-" + ex.GetType().Name);
            }
        }

        private void OnStarted(object sender, EventArgs args)
        {
            Guard(() =>
            {
                if (!ReferenceEquals(sender, _view) && !_view.IsGettingValue) return;
                _request++;
                _wasGettingValue = _view.IsGettingValue;
                Snapshot("global-get-value-started senderMatches=" + ReferenceEquals(sender, _view));
            });
        }

        private void OnPointChanged(object sender, EventArgs args)
        {
            Guard(() =>
            {
                if (!_view.IsGettingValue || _clock.ElapsedMilliseconds - _lastMove < 250) return;
                _lastMove = _clock.ElapsedMilliseconds;
                Snapshot("cursor-point-changed");
            });
        }

        private void OnAccepting(object sender, CancelEventArgs args)
        {
            Guard(() => Snapshot("accepting cancelObserved=" + args.Cancel));
        }

        private void OnEmptyStack(object sender, EventArgs args)
        {
            Guard(() => Snapshot("global-empty-action-stack"));
        }

        private void OnViewDisposed(object sender, EventArgs args) { Stop("view-disposed"); }

        private void OnTick(object sender, EventArgs args)
        {
            Guard(() =>
            {
                if (_clock.Elapsed >= TimeSpan.FromMinutes(10) || _writtenBytes >= MaxBytes)
                {
                    Stop("time-or-size-limit");
                    return;
                }
                var settings = Settings();
                if (settings != _settings) { _settings = settings; Write("settings " + settings); }
                var getting = _view.IsGettingValue;
                if (getting != _wasGettingValue)
                {
                    _wasGettingValue = getting;
                    Snapshot(getting ? "input-active-observed" : "input-ended-observed");
                }
                if (getting && _clock.ElapsedMilliseconds - _lastSnapshot >= 1000)
                    Snapshot("input-heartbeat");
                _writer.Flush();
            });
        }

        private string Settings()
        {
            var s = _view.DraftingSettings;
            return "nativePolarPatch=" + NativePolarPatch.Enabled + " polarViewRotation=" + NativePolarPatch.RotationEnabled +
                " polar=" + DraftingSettings.PolarTracking + " angleStepRaw=" + Number(DraftingSettings.PolarTrackingAngleStep) +
                " additionalEnabled=" + DraftingSettings.PolarTrackingAdditionalAngles +
                " additionalRaw=" + (DraftingSettings.AdditionalAngles == null ? "null" :
                    string.Join(",", Array.ConvertAll(DraftingSettings.AdditionalAngles, Number))) +
                " ortho=" + s.Ortho + " stepSnap=" + s.SSnap + " objectSnap=" + DraftingSettings.OSnap +
                " snapFlags=" + DraftingSettings.ObjectSnap + " objectTracking=" + DraftingSettings.OTracking +
                " objectTrackingOrthoOnly=" + DraftingSettings.OTrackingOrthoOnly +
                " displayHints=" + DraftingSettings.DisplayHints + " pointerInput=" + DraftingSettings.EnablePointerInput +
                " auxiliaryDelay=" + DraftingSettings.AuxiliaryDelay + " snapLookupSize=" + DraftingSettings.SnapLookupSize +
                " defaultPointCursor=" + DraftingSettings.DefaultPointCursorType +
                " defaultLinearCursor=" + DraftingSettings.DefaultLinearCursorType;
        }

        private void Snapshot(string name)
        {
            _lastSnapshot = _clock.ElapsedMilliseconds;
            var cursor = _view.CurrentCursor;
            var mouse = _view.LastMousePoint;
            Write(name + " request=" + _request + " getting=" + _view.IsGettingValue +
                " modal=" + _view.IsModalEdit + " stack=" + CadView.ActionStackCount +
                " focused=" + _view.ContainsFocus + " cursorType=" + _view.CursorType +
                " cursor=" + (cursor == null ? "null" : cursor.GetType().FullName) +
                " base=" + Point(_view.FirstLinePoint) + " previousBase=" + Point(_view.PreviousFirstLinePoint) +
                " current=" + Point(_view.CurrentCursorPointF) + " last=" + Point(_view.LastPoint) +
                " lastMouseRaw=(" + Number(mouse.X) + "," + Number(mouse.Y) + ")" +
                " dynamic=" + Point(cursor == null ? null : cursor.DynamicPoint) +
                " snap=" + _view.LastObjectSnap + " ucsRotation=" + Number(_view.UCSRotation) +
                " ucsScale=" + Number(_view.UCSScale) + " screenRotation=" + Number(_view.ScreenRotation));
        }

        private static string Number(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private static string Point(Vector3D? point)
        {
            if (!point.HasValue) return "null";
            var p = point.Value;
            return "(" + Number(p.X) + "," + Number(p.Y) + "," + Number(p.Z) + ")";
        }

        private void Write(string message)
        {
            if (_disposed || _writtenBytes >= MaxBytes) return;
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                " seq=" + (++_sequence) + " ms=" + _clock.ElapsedMilliseconds + " " + message;
            _writer.WriteLine(line);
            _writtenBytes += Encoding.UTF8.GetByteCount(line) + 2;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer.Dispose();
            CadView.GetValueStarted -= OnStarted;
            _view.CurrentCursorPointChanged -= OnPointChanged;
            _view.Accepting -= OnAccepting;
            CadView.EmptyActionStack -= OnEmptyStack;
            _view.Disposed -= OnViewDisposed;
            try { _writer.Dispose(); }
            catch { /* Best effort flush on disk failure. */ }
        }
    }
}
