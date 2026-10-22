# Broiero Installer

Installs, updates and removes Bruno Broiero's screen-reader mods for Steam games, from GitHub, in
one accessible window. / Instala, actualiza y quita los mods de accesibilidad de Bruno Broiero
para juegos de Steam, desde GitHub, en una ventana accesible.

[English](#english) · [Español](#español)

---

## English

### For players

1. Download `BroieroInstaller.exe` from [Releases](../../releases/latest). It is a single file;
   nothing else to install.
2. Run it. It lists every mod, the game it is for, and whether it is installed in your copy of
   the game. Steam games are found on their own; if one is not, use **Choose game folder**.
3. Pick a mod and press **Install** (or **Update**). The files go into the game folder. Start the
   game from Steam as usual.

Everything is a plain Windows control, so NVDA, JAWS and Narrator read it all: arrow through the
list, Tab to the buttons, and every result arrives as a dialog. The installer speaks Spanish when
Windows does, English otherwise.

When a newer installer is published, it offers to update itself at start.

### For mod authors (how a mod gets listed)

The installer asks GitHub for every public repository of `BrunoBroiero` and keeps those with a
`mod.json` at their root. Nothing in the installer names a mod, so publishing a new one is
publishing a new repository with a `mod.json` and a release:

```json
{
  "schema": 1,
  "id": "samsho-reader",
  "name": "SamSho Reader",
  "game": "SAMURAI SHODOWN",
  "steamAppId": 1342260,
  "installDir": "SamuraiShodown",
  "gameExe": "SamuraiShodown.exe",
  "target": "SamuraiShodown/Binaries/Win64",
  "asset": "SamShoReader-*.zip",
  "uninstall": ["dwmapi.dll", "ue4ss"],
  "description": { "en": "...", "es": "..." }
}
```

| Field | Meaning |
|---|---|
| `id` | Stable key; settings and install records use it. |
| `name`, `game` | What the list shows. |
| `steamAppId` | Steam looks the game up by this (`steamapps/appmanifest_<id>.acf`). |
| `installDir` | Folder name under `steamapps/common`, used when the app manifest is missing. |
| `gameExe` | File at the game's root that proves a hand-picked folder is right. |
| `target` | Where the zip is extracted, relative to the game folder; `""` for the root. |
| `asset` | Wildcard the release's zip must match. The latest release with a matching asset is offered; its tag (`v1.1.0` or `1.1.0`) is the version. |
| `uninstall` | Files and folders, relative to `target`, that a hand-made install leaves. The installer deletes exactly what it extracted, then these. |
| `description` | Short text by language code. |

The zip is extracted as-is into `target`, so its layout is the same one the README of the mod
tells players to extract by hand.

### Building

```powershell
.\build.ps1          # dist\BroieroInstaller.exe, self-contained, single file
dotnet run --project tests\Smoke   # no window: discover, install into a temp folder, uninstall
```

Needs the .NET 9 SDK (or newer). A push of a tag `v*` builds and publishes a release through
GitHub Actions; the installer updates itself from those releases.

---

## Español

### Para jugadores

1. Bajá `BroieroInstaller.exe` desde [Releases](../../releases/latest). Es un solo archivo; no hay
   que instalar nada más.
2. Abrilo. Muestra cada mod, el juego al que pertenece y si está instalado en tu copia del juego.
   Los juegos de Steam se encuentran solos; si alguno no aparece, usá **Elegir carpeta del juego**.
3. Elegí un mod y apretá **Instalar** (o **Actualizar**). Los archivos van a la carpeta del juego.
   Abrí el juego desde Steam como siempre.

Todo son controles comunes de Windows, así que NVDA, JAWS y Narrador lo leen entero: flechas en la
lista, Tab a los botones, y cada resultado llega en un diálogo. El instalador habla en español si
Windows está en español, y en inglés si no.

Cuando se publica un instalador nuevo, al abrirse ofrece actualizarse solo.

### Para autores de mods

El instalador pide a GitHub todos los repositorios públicos de `BrunoBroiero` y se queda con los
que tienen un `mod.json` en la raíz. El instalador no nombra ningún mod, así que publicar uno
nuevo es publicar un repositorio nuevo con su `mod.json` y un release con el zip. Los campos están
descritos en la sección en inglés.

### Compilar

```powershell
.\build.ps1          # dist\BroieroInstaller.exe, autocontenido, un solo archivo
dotnet run --project tests\Smoke   # sin ventana: descubre, instala en una carpeta temporal, desinstala
```

Necesita el SDK de .NET 9 o más nuevo. Al subir una etiqueta `v*`, GitHub Actions compila y
publica el release; el instalador se actualiza desde esos releases.

---

Author: Bruno Broiero. Built with Claude.
