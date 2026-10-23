using System.Diagnostics;

namespace BJManager;

/// <summary>One window: the mods in a list, and the buttons that act on the selected one. Plain
/// Windows controls only, so NVDA and JAWS read every piece without help.</summary>
public sealed class MainForm : Form
{
    private readonly bool _justUpdated;
    private readonly HttpClient _http = GitHubCatalog.NewHttpClient();
    private readonly GitHubCatalog _catalog;
    private AppSettings _settings = SettingsStore.Load();

    private readonly Label _intro = new();
    private readonly ListView _list = new();
    private readonly TextBox _description = new();
    private readonly Button _install = new();
    private readonly Button _uninstall = new();
    private readonly Button _browse = new();
    private readonly Button _readme = new();
    private readonly Button _refresh = new();
    private readonly Button _close = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();

    private bool _busy;

    public MainForm(bool justUpdated)
    {
        _justUpdated = justUpdated;
        _catalog = new GitHubCatalog(_http);
        BuildLayout();
    }

    private ModEntry? Selected => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as ModEntry : null;

    // ----- layout -------------------------------------------------------------------------------

    private void BuildLayout()
    {
        Text = $"{L.T("title")} {SelfUpdater.CurrentVersion}";
        Font = new Font("Segoe UI", 10f);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 420);
        Size = new Size(820, 540);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _intro.Text = L.T("intro");
        _intro.AutoSize = true;
        _intro.MaximumSize = new Size(780, 0);
        _intro.Margin = new Padding(0, 0, 0, 8);

        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.MultiSelect = false;
        _list.Dock = DockStyle.Fill;
        _list.AccessibleName = L.T("list.name");
        _list.Columns.Add(L.T("col.mod"), 180);
        _list.Columns.Add(L.T("col.game"), 240);
        _list.Columns.Add(L.T("col.status"), 340);
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.ItemActivate += async (_, _) => await InstallAsync();
        _list.Margin = new Padding(0, 0, 0, 8);

        _description.ReadOnly = true;
        _description.Multiline = true;
        _description.ScrollBars = ScrollBars.Vertical;
        _description.Height = 64;
        _description.Dock = DockStyle.Fill;
        _description.AccessibleName = L.T("desc.name");
        _description.Margin = new Padding(0, 0, 0, 8);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        foreach (var (button, key, handler) in new (Button, string, Func<Task>)[]
        {
            (_install, "btn.install", InstallAsync),
            (_uninstall, "btn.uninstall", UninstallAsync),
            (_browse, "btn.browse", () => { Browse(); return Task.CompletedTask; }),
            (_readme, "btn.readme", () => { OpenReadme(); return Task.CompletedTask; }),
            (_refresh, "btn.refresh", LoadAsync),
            (_close, "btn.close", () => { Close(); return Task.CompletedTask; }),
        })
        {
            button.Text = L.T(key);
            button.AutoSize = true;
            button.Padding = new Padding(6, 2, 6, 2);
            button.Margin = new Padding(0, 0, 8, 0);
            button.Click += async (_, _) => await handler();
            buttons.Controls.Add(button);
        }

        _status.Text = L.T("ready");
        _status.AutoSize = true;
        _status.AccessibleName = L.T("status.name");
        _status.Margin = new Padding(0, 0, 0, 4);

        _progress.Dock = DockStyle.Fill;
        _progress.Height = 18;
        _progress.Visible = false;
        _progress.AccessibleName = L.T("status.name");

        root.Controls.Add(_intro, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(_description, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        root.Controls.Add(_status, 0, 4);
        root.Controls.Add(_progress, 0, 5);
        Controls.Add(root);

        CancelButton = _close;
        UpdateButtons();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_justUpdated)
            MessageBox.Show(this, L.F("selfUpdate.done", SelfUpdater.CurrentVersion), L.T("done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        await LoadAsync();
        await CheckSelfUpdateAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _http.Dispose();
        base.OnFormClosed(e);
    }

    // ----- the list -----------------------------------------------------------------------------

    private async Task LoadAsync()
    {
        if (_busy) return;
        SetBusy(true, L.T("loading"));
        _list.Items.Clear();
        _settings = SettingsStore.Load();
        try
        {
            var manifests = await _catalog.DiscoverAsync(CancellationToken.None);
            var entries = manifests.Select(m => new ModEntry { Manifest = m }).ToList();
            await Task.WhenAll(entries.Select(async entry =>
            {
                try { entry.Release = await _catalog.LatestReleaseAsync(entry.Manifest, CancellationToken.None); }
                catch (HttpRequestException) { entry.Release = null; }
            }));
            foreach (var entry in entries)
            {
                Resolve(entry);
                var item = new ListViewItem(new[] { NameText(entry), entry.Manifest.Game, StatusText(entry) }) { Tag = entry };
                _list.Items.Add(item);
            }
            SetStatus(entries.Count == 0 ? L.T("noMods") : L.F("loaded", entries.Count));
        }
        catch (Exception ex)
        {
            SetStatus(L.F("loadFailed", ex.Message));
            MessageBox.Show(this, L.F("loadFailed", ex.Message), L.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
        if (_list.Items.Count > 0)
        {
            // Land on the first mod with an update waiting, so it is the first thing heard.
            var first = _list.Items.Cast<ListViewItem>().FirstOrDefault(i => (i.Tag as ModEntry)?.State == InstallState.UpdateAvailable)
                        ?? _list.Items[0];
            first.Selected = true;
            first.Focused = true;
        }
        _list.Focus();
        UpdateButtons();
    }

    /// <summary>Works out where the game is and whether the mod is in it.</summary>
    private void Resolve(ModEntry entry)
    {
        var mod = entry.Manifest;
        entry.GamePath = null;
        if (_settings.GamePaths.TryGetValue(mod.Id, out var chosen) && SteamLibrary.LooksLikeGame(chosen, mod))
            entry.GamePath = chosen;
        entry.GamePath ??= SteamLibrary.FindGame(mod);

        entry.Record = _settings.Installed.TryGetValue(mod.Id, out var record) && ModInstaller.RecordHolds(record, entry) ? record : null;

        if (entry.GamePath is null) entry.State = InstallState.GameNotFound;
        else if (entry.Record is not null)
            entry.State = entry.Release is not null && Versions.Compare(entry.Release.Version, entry.Record.Version) > 0
                ? InstallState.UpdateAvailable
                : InstallState.Installed;
        else if (ModInstaller.LooksInstalledByHand(entry)) entry.State = InstallState.InstalledByHand;
        else entry.State = entry.Release is null ? InstallState.NoRelease : InstallState.NotInstalled;
    }

    /// <summary>The first column, which a screen reader says first: an update waiting goes before the name.</summary>
    private static string NameText(ModEntry e) =>
        e.State == InstallState.UpdateAvailable ? L.F("name.update", e.Manifest.Name) : e.Manifest.Name;

    private static string StatusText(ModEntry e) => e.State switch
    {
        InstallState.GameNotFound => L.T("state.gameNotFound"),
        InstallState.NoRelease => L.T("state.noRelease"),
        InstallState.NotInstalled => L.F("state.notInstalled", e.Release!.Version),
        InstallState.Installed => e.Release is null ? L.F("state.installedOffline", e.Record!.Version) : L.F("state.installed", e.Record!.Version),
        InstallState.UpdateAvailable => L.F("state.updateAvailable", e.Record!.Version, e.Release!.Version),
        InstallState.InstalledByHand => e.Release is null ? L.T("state.byHandNoRelease") : L.F("state.byHand", e.Release.Version),
        _ => "",
    };

    private void RefreshRow(ModEntry entry)
    {
        Resolve(entry);
        foreach (ListViewItem item in _list.Items)
        {
            if (!ReferenceEquals(item.Tag, entry)) continue;
            item.SubItems[0].Text = NameText(entry);
            item.SubItems[2].Text = StatusText(entry);
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var e = Selected;
        var canAct = e is not null && !_busy;
        _install.Enabled = canAct && e!.GamePath is not null && e.Release is not null;
        _install.Text = L.T(e?.State switch
        {
            InstallState.UpdateAvailable => "btn.update",
            InstallState.Installed or InstallState.InstalledByHand => "btn.reinstall",
            _ => "btn.install",
        });
        _uninstall.Enabled = canAct && e!.GamePath is not null && (e.Record is not null || e.State == InstallState.InstalledByHand);
        _browse.Enabled = canAct;
        _readme.Enabled = canAct;
        _refresh.Enabled = !_busy;
        _description.Text = e is null ? "" : $"{e.Manifest.DescriptionFor(L.Lang)}\r\n{e.GamePath ?? ""}";
    }

    // ----- actions ------------------------------------------------------------------------------

    private async Task InstallAsync()
    {
        var entry = Selected;
        if (entry is null) { Say("noSelection"); return; }
        if (_busy || entry.GamePath is null || entry.Release is null) return;
        if (ModInstaller.IsGameRunning(entry.Manifest))
        {
            MessageBox.Show(this, L.F("gameRunning", entry.Manifest.Game), L.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true, L.F("downloading", entry.Release.AssetName, L.Bytes(0), L.Bytes(entry.Release.AssetSize)));
        try
        {
            var record = await ModInstaller.InstallAsync(_catalog, entry, DownloadProgress(entry.Release.AssetName),
                new Progress<string>(SetStatus), CancellationToken.None);
            _settings.Installed[entry.Manifest.Id] = record;
            SettingsStore.Save(_settings);
            SetBusy(false, L.T("ready"));
            RefreshRow(entry);
            MessageBox.Show(this, L.F("installed.ok", entry.Manifest.Name, record.Version, entry.TargetPath), L.T("done"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            SetBusy(false, L.F("install.failed", ex.Message));
            MessageBox.Show(this, L.F("install.failed", ex.Message), L.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        _list.Focus();
    }

    private async Task UninstallAsync()
    {
        var entry = Selected;
        if (entry is null) { Say("noSelection"); return; }
        if (_busy || entry.GamePath is null) return;
        if (ModInstaller.IsGameRunning(entry.Manifest))
        {
            MessageBox.Show(this, L.F("gameRunning", entry.Manifest.Game), L.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var answer = MessageBox.Show(this, L.F("uninstall.confirm", entry.Manifest.Name, entry.Manifest.Game), L.T("question"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        SetBusy(true);
        try
        {
            await Task.Run(() => ModInstaller.Uninstall(entry));
            _settings.Installed.Remove(entry.Manifest.Id);
            SettingsStore.Save(_settings);
            SetBusy(false, L.T("ready"));
            RefreshRow(entry);
            MessageBox.Show(this, L.F("uninstall.ok", entry.Manifest.Name), L.T("done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            SetBusy(false, L.F("uninstall.failed", ex.Message));
            MessageBox.Show(this, L.F("uninstall.failed", ex.Message), L.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        _list.Focus();
    }

    private void Browse()
    {
        var entry = Selected;
        if (entry is null) { Say("noSelection"); return; }
        using var dialog = new FolderBrowserDialog
        {
            Description = L.F("browse.title", entry.Manifest.Game, entry.Manifest.GameExe),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = entry.GamePath ?? SteamLibrary.FindSteamRoot() ?? "",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!SteamLibrary.LooksLikeGame(dialog.SelectedPath, entry.Manifest))
        {
            MessageBox.Show(this, L.F("browse.wrong", entry.Manifest.GameExe), L.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _settings.GamePaths[entry.Manifest.Id] = dialog.SelectedPath;
        SettingsStore.Save(_settings);
        RefreshRow(entry);
        _list.Focus();
    }

    private void OpenReadme()
    {
        var entry = Selected;
        if (entry is null) { Say("noSelection"); return; }
        var url = entry.Manifest.RepoUrl;
        if (L.Lang == "es") url += "/blob/HEAD/README.es.md";
        else url += "#readme";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async Task CheckSelfUpdateAsync()
    {
        ReleaseInfo? newer;
        try { newer = await SelfUpdater.CheckAsync(_catalog, CancellationToken.None); }
        catch { return; } // a missing network or an unpublished installer is no reason to nag
        if (newer is null) return;

        var answer = MessageBox.Show(this, L.F("selfUpdate.ask", newer.Version, SelfUpdater.CurrentVersion), L.T("question"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        SetBusy(true, L.T("selfUpdate.downloading"));
        try
        {
            await SelfUpdater.ApplyAsync(_catalog, newer, DownloadProgress(SelfUpdater.AssetName), CancellationToken.None);
            // On success the application is already exiting.
        }
        catch (Exception ex)
        {
            SetBusy(false, L.T("ready"));
            MessageBox.Show(this, L.F("selfUpdate.failed", ex.Message), L.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ----- helpers ------------------------------------------------------------------------------

    private IProgress<(long done, long total)> DownloadProgress(string assetName)
    {
        var lastShown = DateTime.MinValue;
        return new Progress<(long done, long total)>(p =>
        {
            if (p.total > 0)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = (int)Math.Clamp(p.done * 100 / p.total, 0, 100);
            }
            var now = DateTime.UtcNow;
            if ((now - lastShown).TotalMilliseconds < 250 && p.done != p.total) return;
            lastShown = now;
            SetStatus(L.F("downloading", assetName, L.Bytes(p.done), p.total > 0 ? L.Bytes(p.total) : "?"));
        });
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _progress.Visible = busy;
        if (busy) { _progress.Style = ProgressBarStyle.Marquee; _progress.Value = 0; }
        if (status is not null) SetStatus(status);
        UpdateButtons();
    }

    private void SetStatus(string text) => _status.Text = text;

    private void Say(string key) =>
        MessageBox.Show(this, L.T(key), L.T("title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
}
