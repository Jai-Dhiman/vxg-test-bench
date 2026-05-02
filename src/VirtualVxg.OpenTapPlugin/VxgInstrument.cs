using System.Globalization;
using System.Net.Sockets;
using System.Text;
using OpenTap;

namespace VirtualVxg.OpenTapPlugin;

[Display("Virtual VXG", "Simulated Keysight M9484C VXG over SCPI/TCP.", "VirtualVxg")]
public class VxgInstrument : Instrument
{
    [Display("Host")] public string Host { get; set; } = "127.0.0.1";
    [Display("Port")] public int Port { get; set; } = 5025;

    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public override void Open()
    {
        base.Open();
        _client = new TcpClient();
        _client.Connect(Host, Port);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.ASCII);
        _writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };
    }

    public override void Close()
    {
        try { _writer?.Dispose(); _reader?.Dispose(); _client?.Dispose(); }
        finally { _writer = null; _reader = null; _client = null; base.Close(); }
    }

    public void SetFrequency(double hz) =>
        Send($"FREQ {hz.ToString("R", CultureInfo.InvariantCulture)}");

    public void SetPower(double dbm) =>
        Send($"POW {dbm.ToString("R", CultureInfo.InvariantCulture)}");

    public void EnableOutput() => Send("OUTP ON");

    public void DisableOutput() => Send("OUTP OFF");

    public double MeasurePower()
    {
        var reply = Query("MEAS:POW?");
        return double.Parse(reply, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private void Send(string command)
    {
        if (_writer is null) throw new InvalidOperationException("Instrument not open");
        _writer.WriteLine(command);
    }

    private string Query(string command)
    {
        Send(command);
        if (_reader is null) throw new InvalidOperationException("Instrument not open");
        var line = _reader.ReadLine() ?? throw new IOException("Connection closed during query");
        return line;
    }
}
