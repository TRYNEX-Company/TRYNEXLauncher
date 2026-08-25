using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Trynex.Bootstrapper;

public partial class BootstrapperWindow : Window
{
    private static readonly TimeSpan MinimumVisibleTime = TimeSpan.FromMilliseconds(550);

    private readonly BootstrapperApplication _bootstrapper;
    private readonly BootstrapperLogger _logger;
    private CancellationTokenSource? _cancellation;
    private bool _isRunning;
    private bool _allowClose;

    internal BootstrapperWindow(BootstrapperApplication bootstrapper, BootstrapperLogger logger)
    {
        _bootstrapper = bootstrapper;
        _logger = logger;
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RunBootstrapperAsync();
    }

    private async Task RunBootstrapperAsync()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        RetryButton.Visibility = Visibility.Collapsed;
        _cancellation = new CancellationTokenSource();
        var visibleTimer = Stopwatch.StartNew();
        var progress = new Progress<BootstrapperProgress>(ApplyProgress);

        try
        {
            var exitCode = await _bootstrapper.RunAsync(progress, _cancellation.Token);
            var remaining = MinimumVisibleTime - visibleTimer.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining);
            }

            ExitApplication(exitCode);
        }
        catch (OperationCanceledException)
        {
            ExitApplication(0);
        }
        catch (Exception exception)
        {
            _logger.Error("Bootstrapper stopped unexpectedly.", exception);
            ShowFatalError();
        }
        finally
        {
            _isRunning = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private void ApplyProgress(BootstrapperProgress progress)
    {
        StatusTitle.Text = progress.Title;
        StatusDetail.Text = progress.Detail;
        TransferText.Text = progress.TransferText ?? string.Empty;

        UpdateProgress.IsIndeterminate = progress.Percentage is null;
        if (progress.Percentage is not null)
        {
            UpdateProgress.Value = progress.Percentage.Value;
        }

        var warning = progress.Stage is BootstrapperStage.Warning or BootstrapperStage.RollingBack;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(warning ? "#FFB020" : "#00E436"));
        SecurityText.Text = warning
            ? "Рабочая версия останется доступной"
            : "Подпись и SHA-256 проверяются автоматически";
    }

    private void ShowFatalError()
    {
        StatusTitle.Text = "Не удалось запустить TRYNEX";
        StatusDetail.Text = "Подробности сохранены в журнале. Можно повторить запуск — рабочие версии не удалены.";
        TransferText.Text = string.Empty;
        UpdateProgress.IsIndeterminate = false;
        UpdateProgress.Value = 0;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4D67"));
        SecurityText.Text = "Установленные версии и незавершённая загрузка сохранены";
        RetryButton.Visibility = Visibility.Visible;
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBootstrapperAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        ExitApplication(0);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            _cancellation?.Cancel();
            _allowClose = true;
            Application.Current.Shutdown(0);
        }

        base.OnClosing(e);
    }

    private void ExitApplication(int exitCode)
    {
        if (_allowClose)
        {
            return;
        }

        _allowClose = true;
        Close();
        Application.Current.Shutdown(exitCode);
    }
}
