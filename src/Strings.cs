using System.Globalization;

namespace BroieroInstaller;

/// <summary>User-facing text, in Spanish or English after the Windows display language.</summary>
public static class L
{
    public static readonly string Lang =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";

    private static readonly Dictionary<string, (string en, string es)> Table = new()
    {
        ["title"] = ("Broiero Installer", "Broiero Installer"),
        ["intro"] = ("Screen-reader mods by Bruno Broiero. Pick a mod from the list and use the buttons below it.",
                     "Mods de accesibilidad de Bruno Broiero. Elegí un mod de la lista y usá los botones de abajo."),
        ["col.mod"] = ("Mod", "Mod"),
        ["col.game"] = ("Game", "Juego"),
        ["col.status"] = ("Status", "Estado"),
        ["list.name"] = ("Mods", "Mods"),
        ["desc.name"] = ("Description of the selected mod", "Descripción del mod elegido"),
        ["status.name"] = ("Progress", "Progreso"),

        ["btn.install"] = ("&Install", "&Instalar"),
        ["btn.update"] = ("&Update", "Actual&izar"),
        ["btn.reinstall"] = ("&Reinstall", "&Reinstalar"),
        ["btn.uninstall"] = ("&Uninstall", "&Desinstalar"),
        ["btn.browse"] = ("Choose &game folder...", "Elegir carpeta del &juego..."),
        ["btn.readme"] = ("Open i&nstructions", "Abrir instr&ucciones"),
        ["btn.refresh"] = ("Re&fresh list", "Re&cargar lista"),
        ["btn.close"] = ("&Close", "C&errar"),

        ["state.gameNotFound"] = ("Game not found. Use \"Choose game folder\".", "Juego no encontrado. Usá \"Elegir carpeta del juego\"."),
        ["state.noRelease"] = ("No download published yet.", "Todavía no hay descarga publicada."),
        ["state.notInstalled"] = ("Not installed. Available: {0}.", "No instalado. Disponible: {0}."),
        ["state.installed"] = ("Installed: {0}. Up to date.", "Instalado: {0}. Al día."),
        ["state.installedOffline"] = ("Installed: {0}.", "Instalado: {0}."),
        ["state.updateAvailable"] = ("Installed: {0}. Update available: {1}.", "Instalado: {0}. Actualización disponible: {1}."),
        ["state.byHand"] = ("Installed by hand. Available: {0}.", "Instalado a mano. Disponible: {0}."),
        ["state.byHandNoRelease"] = ("Installed by hand.", "Instalado a mano."),
        ["name.update"] = ("Update available: {0}", "Actualización disponible: {0}"),

        ["loading"] = ("Looking for mods on GitHub...", "Buscando mods en GitHub..."),
        ["loaded"] = ("{0} mods found.", "{0} mods encontrados."),
        ["loadFailed"] = ("Could not reach GitHub: {0}", "No se pudo conectar con GitHub: {0}"),
        ["noMods"] = ("No mods found.", "No se encontraron mods."),
        ["ready"] = ("Ready.", "Listo."),
        ["downloading"] = ("Downloading {0}: {1} of {2}", "Descargando {0}: {1} de {2}"),
        ["extracting"] = ("Copying files into the game folder...", "Copiando archivos a la carpeta del juego..."),

        ["gameRunning"] = ("{0} is running. Close the game and try again.", "{0} está abierto. Cerrá el juego y probá de nuevo."),
        ["installed.ok"] = ("{0} {1} is installed in:\n{2}\n\nStart the game from Steam.", "{0} {1} quedó instalado en:\n{2}\n\nAbrí el juego desde Steam."),
        ["install.failed"] = ("The installation failed: {0}", "La instalación falló: {0}"),
        ["uninstall.confirm"] = ("Remove {0} from {1}?", "¿Quitar {0} de {1}?"),
        ["uninstall.ok"] = ("{0} was removed.", "{0} fue quitado."),
        ["uninstall.failed"] = ("The uninstall failed: {0}", "La desinstalación falló: {0}"),
        ["browse.title"] = ("Choose the folder where {0} is installed (it contains {1})", "Elegí la carpeta donde está instalado {0} (contiene {1})"),
        ["browse.wrong"] = ("That folder does not contain {0}.", "Esa carpeta no contiene {0}."),
        ["noSelection"] = ("Choose a mod from the list first.", "Primero elegí un mod de la lista."),

        ["selfUpdate.ask"] = ("Broiero Installer {0} is available (you have {1}). Update now?", "Hay una versión nueva de Broiero Installer: {0} (tenés la {1}). ¿Actualizar ahora?"),
        ["selfUpdate.downloading"] = ("Downloading the new installer...", "Descargando el instalador nuevo..."),
        ["selfUpdate.restart"] = ("The installer will close and open again updated.", "El instalador se va a cerrar y abrir de nuevo actualizado."),
        ["selfUpdate.failed"] = ("The installer could not update itself: {0}", "El instalador no pudo actualizarse: {0}"),
        ["selfUpdate.done"] = ("Broiero Installer is now version {0}.", "Broiero Installer ya está en la versión {0}."),

        ["error"] = ("Error", "Error"),
        ["done"] = ("Done", "Listo"),
        ["question"] = ("Confirm", "Confirmar"),
    };

    public static string T(string key) =>
        Table.TryGetValue(key, out var t) ? (Lang == "es" ? t.es : t.en) : key;

    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    public static string Bytes(long n)
    {
        if (n >= 1 << 20) return $"{n / (double)(1 << 20):0.#} MB";
        if (n >= 1 << 10) return $"{n / (double)(1 << 10):0} KB";
        return $"{n} B";
    }
}
