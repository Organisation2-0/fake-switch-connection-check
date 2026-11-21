// Main window of the FSCC application
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Fscc;

/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window
{
    private readonly FscsServer _server = new();
    private readonly ObservableCollection<string> _logLines = new();
    private readonly DispatcherTimer _pulseTimer = new();
    private double _currentScale = 1.0;

    public MainWindow()
    {
        InitializeComponent();
        AdapterCombo.ItemsSource = _server.AvailableAdapters;

        _server.LogChanged += OnLog;
        _server.StatusChanged += OnStatus;
        _server.ConnectionDetected += OnConnectionDetected;
        _server.ConnectionLost += OnConnectionLost;

        // Pulse animation for the ring
        _pulseTimer.Interval = TimeSpan.FromMilliseconds(300);
        _pulseTimer.Tick += (s, e) => PulseRing();
        _pulseTimer.IsEnabled = false;

        // Initial log
        _logLines.Add($"{DateTime.Now:HH:mm:ss}  FSCC started – select a network adapter");
        LogList.ItemsSource = _logLines;
    }

    private void OnLog(LogEntry entry)
    {
        LogScroll.Dispatcher.InvokeAsync(() =>
        {
            _logLines.Add($"{entry.Timestamp}  {entry.Message}");
            LogScroll.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private void OnStatus(ServerStatus status)
    {
        StatusText.Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text = status switch
            {
                ServerStatus.Stopped    => "Status: Stopped",
                ServerStatus.Starting   => "Status: Starting...",
                ServerStatus.Running    => "Status: Active",
                ServerStatus.Stopping   => "Status: Stopping...",
                _                       => "Status: ?"
            };

            StartBtn.IsEnabled = status == ServerStatus.Stopped;
            StopBtn.IsEnabled = status == ServerStatus.Running;
            AdapterCombo.IsEnabled = status == ServerStatus.Stopped;

            if (status == ServerStatus.Running)
            {
                StatusText.Foreground = Brushes.LightGreen;
            }
            else
            {
                StatusText.Foreground = Brushes.Gray;
                SetConnectionInactive();
            }
        }, DispatcherPriority.Send);
    }

    private void OnConnectionDetected()
    {
        StatusText.Dispatcher.InvokeAsync(() =>
        {
            SetConnectionActive();
        }, DispatcherPriority.Send);
    }

    private void OnConnectionLost()
    {
        StatusText.Dispatcher.InvokeAsync(() =>
        {
            SetConnectionInactive();
        }, DispatcherPriority.Send);
    }

    private void SetConnectionActive()
    {
        ConnectionRing.Stroke = (Brush)this.FindResource("ActiveBrush") ?? Brushes.LightGreen;
        ConnectionText.Text = "Active";
        ConnectionText.Foreground = Brushes.LightGreen;
        ConnectionStatus.Text = "Switch connected";
        ConnectionStatus.Foreground = Brushes.LightGreen;
        _pulseTimer.IsEnabled = true;
    }

    private void SetConnectionInactive()
    {
        ConnectionRing.Stroke = (Brush)this.FindResource("InactiveBrush") ?? Brushes.Gray;
        ConnectionText.Text = "Waiting";
        ConnectionText.Foreground = Brushes.Gray;
        ConnectionStatus.Text = "No connection";
        ConnectionStatus.Foreground = Brushes.Gray;
        _pulseTimer.IsEnabled = false;
    }

    private void PulseRing()
    {
        double targetScale = _currentScale >= 1.02 ? 0.98 : 1.02;
        _currentScale = targetScale;

        var scaleTransform = ConnectionRing.RenderTransform as ScaleTransform ?? new ScaleTransform();
        scaleTransform.ScaleX = targetScale;
        scaleTransform.ScaleY = targetScale;
        ConnectionRing.RenderTransform = scaleTransform;
        ConnectionRing.Opacity = 0.7 + (targetScale - 0.98) * 10;
    }

    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        StartBtn.IsEnabled = false;
        StopBtn.IsEnabled = false;

        var ip = AdapterCombo.SelectedValue as string;
        if (string.IsNullOrEmpty(ip))
        {
            _logLines.Add($"{DateTime.Now:HH:mm:ss}  NO adapter selected");
            StartBtn.IsEnabled = true;
            return;
        }

        await _server.StartServer(ip);
    }

    private async void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        StopBtn.IsEnabled = false;
        await _server.StopServer();
    }

    protected override void OnClosed(EventArgs e)
    {
        _pulseTimer.Stop();
        _ = _server.StopServer();
        base.OnClosed(e);
    }
}
