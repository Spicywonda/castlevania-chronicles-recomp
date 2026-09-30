using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ChroniclesLauncher;

public sealed class LauncherForm : Form
{
    private readonly TextBox _discPath = new();
    private readonly Button _play = new();
    private readonly Label _status = new();
    private bool _running;

    public LauncherForm(string? incomingPath)
    {
        Text = "Castlevania Chronicles";
        ClientSize = new Size(560, 218);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);

        var title = new Label { Text = "Castlevania Chronicles", Font = new Font("Segoe UI", 17, FontStyle.Bold),
            Location = new Point(20, 16), AutoSize = true };
        var subtitle = new Label { Text = "Selecciona tu imagen USA SLUS-01384 para jugar.",
            Location = new Point(22, 53), AutoSize = true };
        _discPath.SetBounds(22, 90, 407, 30);
        _discPath.PlaceholderText = "Archivo .cue o .chd";
        var browse = new Button { Text = "Seleccionar ROM", Location = new Point(435, 88), Size = new Size(108, 34) };
        browse.Click += (_, _) => Browse();
        _play.Text = "Jugar";
        _play.SetBounds(22, 138, 120, 38);
        _play.Click += async (_, _) => await PlayAsync();
        AcceptButton = _play;
        var logs = new Button { Text = "Abrir carpeta de logs", Location = new Point(151, 138), Size = new Size(185, 38) };
        logs.Click += (_, _) => OpenLogFolder();
        _status.SetBounds(22, 185, 520, 26);
        _status.ForeColor = Color.DarkSlateGray;
        Controls.AddRange([title, subtitle, _discPath, browse, _play, logs, _status]);

        _discPath.Text = incomingPath ?? LauncherStorage.LoadSelectedPath(LauncherStorage.DataDirectory) ?? "";
        FormClosing += (_, e) =>
        {
            if (!_running) return;
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
        };
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Seleccionar Castlevania Chronicles USA",
            Filter = "Imágenes de disco (*.cue;*.chd)|*.cue;*.chd|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (File.Exists(_discPath.Text)) dialog.InitialDirectory = Path.GetDirectoryName(_discPath.Text);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _discPath.Text = dialog.FileName;
        SetStatus("Imagen seleccionada. Pulsa Jugar.", false);
    }

    private async Task PlayAsync()
    {
        if (_running) return;
        var result = DiscSelection.Validate(_discPath.Text);
        if (!result.IsValid)
        {
            SetStatus(result.Error!, true);
            return;
        }

        var gameExe = Path.Combine(AppContext.BaseDirectory, "CastlevaniaChronicles.exe");
        if (!File.Exists(gameExe))
        {
            SetStatus("Falta CastlevaniaChronicles.exe. Extrae el ZIP completo.", true);
            return;
        }

        var dataDirectory = LauncherStorage.DataDirectory;
        try
        {
            LauncherStorage.SaveSelectedPath(dataDirectory, result.Path!);
            _running = true;
            _play.Enabled = false;
            SetStatus("Validando disco…", false);
            var validation = GameLaunch.CreateValidationStartInfo(gameExe, result.Path!, dataDirectory);
            var validationLog = Path.Combine(dataDirectory, "validate.log");
            if (await GameSession.RunAsync(validation, validationLog) != 0)
            {
                SetStatus("Disco incompatible o ilegible. Revisa validate.log.", true);
                return;
            }
            SetStatus("Juego en ejecución…", false);
            var startInfo = GameLaunch.CreateStartInfo(gameExe, result.Path!, dataDirectory);
            var logPath = Path.Combine(dataDirectory, "run.log");
            var exitCode = await GameSession.RunAsync(startInfo, logPath);
            if (exitCode == 0) SetStatus("Juego cerrado.", false);
            else
            {
                SetStatus($"El juego terminó con código {exitCode}. Revisa run.log.", true);
                MessageBox.Show(this, $"El juego terminó con código {exitCode}.\n\nLog: {logPath}",
                    "Error al jugar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception error)
        {
            SetStatus($"No se pudo iniciar: {error.Message}", true);
            MessageBox.Show(this, error.Message, "Error al iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _running = false;
            _play.Enabled = true;
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LauncherStorage.DataDirectory);
            Process.Start(new ProcessStartInfo(LauncherStorage.DataDirectory) { UseShellExecute = true });
        }
        catch (Exception error) { SetStatus(error.Message, true); }
    }

    private void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.ForeColor = error ? Color.Firebrick : Color.DarkSlateGray;
    }
}
