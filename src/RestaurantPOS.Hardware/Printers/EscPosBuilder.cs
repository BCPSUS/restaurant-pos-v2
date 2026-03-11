using System.Text;

namespace RestaurantPOS.Hardware.Printers;

public sealed class EscPosBuilder
{
    private readonly StringBuilder _sb = new();

    public EscPosBuilder Text(string text) { _sb.Append(text); return this; }
    public EscPosBuilder Line(string text = "") { _sb.AppendLine(text); return this; }

    // TODO: implement ESC/POS bytes (bold, align, cut, open drawer, QR, etc.)
    public byte[] BuildBytes() => Encoding.UTF8.GetBytes(_sb.ToString());
}
