namespace BJManager;

/// <summary>A small modal window shown while the installer replaces itself: it tells what is
/// happening, shows the download, and cannot be closed, so the main window is out of reach until
/// the new installer takes over (or the update fails and this closes on its own).</summary>
public sealed class UpdateDialog : Form
{
    private readonly Func<IProgress<(long done, long total)>, Task> _work;
    private readonly Label _text = new();
    private readonly ProgressBar _bar = new();

    public Exception? Error { get; private set; }

    public UpdateDialog(string version, Func<IProgress<(long done, long total)>, Task> work)
    {
        _work = work;
        Text = L.T("selfUpdate.title");
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 120);
        AccessibleName = L.T("selfUpdate.title");

        _text.Text = L.F("selfUpdate.body", version);
        _text.AccessibleName = L.T("selfUpdate.title");
        _text.Location = new Point(16, 16);
        _text.Size = new Size(428, 60);
        _text.AutoEllipsis = false;

        _bar.Location = new Point(16, 84);
        _bar.Size = new Size(428, 20);
        _bar.Style = ProgressBarStyle.Marquee;
        _bar.AccessibleName = L.T("status.name");

        Controls.Add(_text);
        Controls.Add(_bar);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        var progress = new Progress<(long done, long total)>(p =>
        {
            if (p.total <= 0) return;
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.Value = (int)Math.Clamp(p.done * 100 / p.total, 0, 100);
        });
        try
        {
            await _work(progress);
            // On success the application is already exiting; nothing more to do here.
        }
        catch (Exception ex)
        {
            Error = ex;
            DialogResult = DialogResult.Abort;
            Close();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Only the update itself (failure) or Application.Exit (success) may close this window.
        if (e.CloseReason == CloseReason.UserClosing && DialogResult != DialogResult.Abort)
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }
}
