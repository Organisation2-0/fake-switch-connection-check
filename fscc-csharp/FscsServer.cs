// DNS and HTTP server for Nintendo Connection Check emulation
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Fscc;

/// <summary>Log entry</summary>
public record LogEntry(string Timestamp, string Message);

/// <summary>Server status</summary>
public enum ServerStatus
{
    Stopped,
    Starting,
    Running,
    Stopping
}

/// <summary>Emulates Nintendo Connection Check server (DNS + HTTP)</summary>
public class FscsServer : IDisposable
{
    private string _localIp = "";
    private byte[] _ipBytes = [];
    private Socket? _dnsSocket;
    private TcpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private bool _hasConnection = false;
    private DateTime _lastActivity = DateTime.MinValue;
    private const int KeepaliveSeconds = 10;

    public event Action<LogEntry>? LogChanged;
    public event Action<ServerStatus>? StatusChanged;
    public event Action? ConnectionDetected;
    public event Action? ConnectionLost;
    public List<NetworkAdapter> AvailableAdapters { get; } = new();

    public ServerStatus Status { get; private set; } = ServerStatus.Stopped;

    public FscsServer()
    {
        AvailableAdapters = GetLocalAdapters();
    }

    public async Task StartServer(string selectedIp)
    {
        Status = ServerStatus.Starting;
        RaiseStatus();
        RaiseLog($"Starting server on {selectedIp}...");

        _localIp = selectedIp;
        _ipBytes = IPAddress.Parse(selectedIp).GetAddressBytes();
        _cts = new CancellationTokenSource();

        try
        {
            // Start DNS server (port 53, with ReuseAddress)
            _dnsSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                SendTimeout = 5000,
                ReceiveTimeout = 5000
            };
            _dnsSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _dnsSocket.Bind(new IPEndPoint(IPAddress.Parse(selectedIp), 53));

            // Start HTTP server (port 80)
            _httpListener = new TcpListener(IPAddress.Parse(selectedIp), 80);
            _httpListener.Start();

            // Start background tasks
            _ = DnsLoop(_cts.Token);
            _ = HttpLoop(_cts.Token);
            _ = KeepaliveLoop(_cts.Token);

            Status = ServerStatus.Running;
            RaiseStatus();
            RaiseLog("Server active – DNS port 53 + HTTP port 80");
        }
        catch (Exception ex)
        {
            Status = ServerStatus.Stopped;
            RaiseStatus();
            RaiseLog($"ERROR: {ex.Message}");
            await StopServer();
        }
    }

    public async Task StopServer()
    {
        if (Status == ServerStatus.Stopped) return;

        Status = ServerStatus.Stopping;
        RaiseStatus();
        RaiseLog("Stopping server...");

        // Cancel token first to stop loops from accepting new requests
        _cts?.Cancel();

        // Close sockets to unblock pending ReceiveFrom/Accept calls
        try
        {
            _dnsSocket?.Close();
            _dnsSocket?.Dispose();
            _httpListener?.Stop();
            _httpListener?.Dispose();
        }
        catch { }

        _dnsSocket = null;
        _httpListener = null;
        _cts?.Dispose();
        _cts = null;
        _hasConnection = false;

        Status = ServerStatus.Stopped;
        RaiseStatus();
        RaiseLog("Server stopped");
        await Task.Yield();
    }

    private async Task DnsLoop(CancellationToken ct)
    {
        var buffer = new byte[512];

        while (!ct.IsCancellationRequested)
        {
            try
            {
                EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                int received = await Task.Run(() => _dnsSocket!.ReceiveFrom(buffer, SocketFlags.None, ref remoteEp));

                var (domain, endOffset) = ParseDomain(buffer, received);
                var question = new byte[endOffset - 12];
                Array.Copy(buffer, 12, question, 0, endOffset - 12);

                var id = new byte[2];
                Array.Copy(buffer, id, 2);

                if (domain.Contains("ctest") || domain.Contains("conntest"))
                {
                    _lastActivity = DateTime.Now;

                    if (!_hasConnection)
                    {
                        _hasConnection = true;
                        ConnectionDetected?.Invoke();
                    }

                    var res = BuildAllowedResponse(id, question, _ipBytes);
                    await Task.Run(() => _dnsSocket!.SendTo(res, SocketFlags.None, remoteEp));
                    RaiseLog($"[ALLOWED] {domain} (from {((IPEndPoint)remoteEp).Address}) -> {_localIp}");
                }
                else
                {
                    var res = BuildBlockedResponse(id, question);
                    await Task.Run(() => _dnsSocket!.SendTo(res, SocketFlags.None, remoteEp));
                    RaiseLog($"[BLOCKED] {domain} (from {((IPEndPoint)remoteEp).Address})");
                }
            }
            catch (ObjectDisposedException) { break; }
            catch (InvalidOperationException) { break; }
            catch (SocketException) { }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task HttpLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener is not null)
        {
            try
            {
                var client = await _httpListener.AcceptTcpClientAsync();
                _ = HandleClient(client);
            }
            catch (ObjectDisposedException) { break; }
            catch (InvalidOperationException) { break; }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task KeepaliveLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);

            if (_hasConnection && (DateTime.Now - _lastActivity).TotalSeconds >= KeepaliveSeconds)
            {
                _hasConnection = false;
                ConnectionLost?.Invoke();
            }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        try
        {
            using var ns = client.GetStream();
            var requestBuffer = new byte[1024];
            int read = await ns.ReadAsync(requestBuffer, 0, requestBuffer.Length);
            string requestText = Encoding.UTF8.GetString(requestBuffer, 0, read);
            string? url = requestText.Split('\r', '\n')[0].Split(' ')[1];
            RaiseLog($"[HTTP CHECK] {url}");

            var response = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nX-Organization: Nintendo\r\nContent-Length: 2\r\n\r\nok";
            await ns.WriteAsync(Encoding.UTF8.GetBytes(response));
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            RaiseLog($"[HTTP ERROR] {ex.Message}");
        }
    }

    private static byte[] BuildAllowedResponse(byte[] id, byte[] question, byte[] ipBytes)
    {
        var res = new List<byte>(id);
        res.AddRange(new byte[] { 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 });
        res.AddRange(question);
        res.AddRange(new byte[] { 0xc0, 0x0c, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3c, 0x00, 0x04 });
        res.AddRange(ipBytes);
        return res.ToArray();
    }

    private static byte[] BuildBlockedResponse(byte[] id, byte[] question)
    {
        var res = new List<byte>(id);
        res.AddRange(new byte[] { 0x81, 0x83, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        res.AddRange(question);
        return res.ToArray();
    }

    private static (string Domain, int EndOffset) ParseDomain(byte[] msg, int length)
    {
        int offset = 12;
        var parts = new List<string>();
        while (offset < length && msg[offset] != 0)
        {
            int len = msg[offset];
            var part = Encoding.UTF8.GetString(msg, offset + 1, len);
            parts.Add(part);
            offset += len + 1;
        }
        return (string.Join(".", parts).ToLowerInvariant(), offset + 5);
    }

    private static List<NetworkAdapter> GetLocalAdapters()
    {
        var list = new List<NetworkAdapter>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (var p in ni.GetIPProperties().UnicastAddresses)
            {
                if (p.Address.AddressFamily == AddressFamily.InterNetwork)
                    list.Add(new NetworkAdapter(ni.Name, p.Address.ToString()));
            }
        }
        return list;
    }

    private void RaiseLog(string message) => LogChanged?.Invoke(new LogEntry(DateTime.Now.ToString("HH:mm:ss"), message));
    private void RaiseStatus() => StatusChanged?.Invoke(Status);

    public void Dispose()
    {
        if (Status != ServerStatus.Stopped)
            _ = StopServer();
        _cts?.Dispose();
    }
}

public record NetworkAdapter(string Name, string Address)
{
    /// <summary>Display name for the ComboBox (adapter name + IP)</summary>
    public string DisplayName => $"{Name} ({Address})";
}
