# BJ Manager

Installs, updates and removes Bruno Broiero's screen-reader mods for Steam games, from GitHub.
This folder is both the live copy a player would have (`BJManager.exe` at the root, ignored by git)
and the source repository (`src/`, `tests/`, `build.ps1`). GitHub: BrunoBroiero/bj-manager.

## Rules that are not obvious from the code

- **Mods are discovered, never hardcoded.** Every public repo of BrunoBroiero with a `mod.json`
  at its root is a mod; its latest release's zip (matching the `asset` wildcard) is what gets
  installed. To add a mod, publish a repo with a `mod.json` and a release. See README.md for the fields.
- **Every release of BJ Manager must ship an asset named exactly `BJManager.exe`**: that is what
  the self-updater downloads. Release 1.1.0 also carried `BroieroInstaller.exe` for copies older
  than the rename; from 1.1.1 on only `BJManager.exe` is needed.
- **Player-facing Spanish is neutral Spanish** ("elige", "usa", "abre", "tienes"), never voseo.
- **The mod list is a plain owner-drawn ListBox** (`ModListBox.cs`), not a ListView grid: NVDA read
  only the first column of the grid. Each item's `ToString()` is the whole sentence the screen
  reader says, with an available update first. Keep any new control plain and readable the same way.
- **Results arrive as dialogs** (MessageBox), because screen readers announce those on their own;
  a label changing quietly is not heard.

## Releasing

```powershell
# bump <Version> in src\BJManager.csproj, commit, then:
.\build.ps1                          # dist\BJManager.exe
git tag vX.Y.Z; git push origin main vX.Y.Z
gh release create vX.Y.Z dist\BJManager.exe --title "BJ Manager X.Y.Z" --notes "..."
```

`.github/workflows/release.yml` does the same on a tag push, but it is not committed yet: the gh
token on this PC lacks the `workflow` scope (`gh auth refresh -h github.com -s workflow` fixes that).

## Testing without a window

```powershell
dotnet run --project tests\Smoke -- <some temp folder>
```

Discovers the mods, finds the games, installs the first mod with a release into the temp folder,
uninstalls it, and checks the self-update lookup. To test the self-update flow for real, publish
a build stamped with an older version (`.\build.ps1 -Version 1.0.9`) and open it: it offers the
latest release.

## Related repos

- samsho-reader, kof-xiv-reader: public download repos (READMEs, `mod.json`, releases).
- kof-xiv-reader-source, re4access: private source repos. Each game's Steam folder is that mod's
  working copy (SamuraiShodown, THE KING OF FIGHTERS XIV).
