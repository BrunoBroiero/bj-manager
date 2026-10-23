namespace BJManager;

/// <summary>One row of the list. What a screen reader says is ToString(): the whole story in one
/// sentence, an update waiting first. What the eye sees is two lines, drawn by ModListBox.</summary>
public sealed class ModRow
{
    public ModEntry Entry { get; }
    public string Headline { get; }
    public string Game { get; }
    public string Detail { get; }

    public ModRow(ModEntry entry, string headline, string game, string detail)
    {
        Entry = entry;
        Headline = headline;
        Game = game;
        Detail = detail;
    }

    public override string ToString() => $"{Headline}, {Game}. {Detail}";
}

/// <summary>A plain list box, which every screen reader reads whole, item by item, drawn with the
/// mod and its game on a first line and the status underneath.</summary>
public sealed class ModListBox : ListBox
{
    private Font? _bold;

    public ModListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        IntegralHeight = false;
        BorderStyle = BorderStyle.FixedSingle;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _bold?.Dispose();
        _bold = new Font(Font, FontStyle.Bold);
        ItemHeight = Font.Height * 2 + 14;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) { base.OnDrawItem(e); return; }
        var row = Items[e.Index] as ModRow;
        var selected = (e.State & DrawItemState.Selected) != 0;
        e.DrawBackground();

        var bold = _bold ?? Font;
        var fore = selected ? SystemColors.HighlightText : SystemColors.ControlText;
        var dim = selected ? SystemColors.HighlightText : SystemColors.GrayText;
        var x = e.Bounds.Left + 8;
        var y = e.Bounds.Top + 6;
        var width = e.Bounds.Width - 16;
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        if (row is null)
        {
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(x, y, width, Font.Height), fore, flags);
        }
        else
        {
            // First line: the headline in bold, then the game in regular weight after it.
            var headSize = TextRenderer.MeasureText(e.Graphics, row.Headline, bold, new Size(width, Font.Height), flags);
            TextRenderer.DrawText(e.Graphics, row.Headline, bold, new Rectangle(x, y, Math.Min(headSize.Width, width), Font.Height), fore, flags);
            var rest = width - headSize.Width - 12;
            if (rest > 40)
                TextRenderer.DrawText(e.Graphics, row.Game, Font, new Rectangle(x + headSize.Width + 12, y, rest, Font.Height), dim, flags);
            // Second line: the status.
            TextRenderer.DrawText(e.Graphics, row.Detail, Font, new Rectangle(x, y + Font.Height + 2, width, Font.Height), dim, flags);
        }
        e.DrawFocusRectangle();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bold?.Dispose();
        base.Dispose(disposing);
    }
}
