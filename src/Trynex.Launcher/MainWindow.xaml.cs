using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Trynex.Launcher.ViewModels;

namespace Trynex.Launcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.Navigate("library");
        if (viewModel.CurrentPage is LibraryViewModel library)
        {
            library.SearchText = viewModel.GlobalSearchText;
        }

        e.Handled = true;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is null)
        {
            return;
        }

        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        UpdateWindowShape();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            const int dwmWindowCornerPreference = 33;
            const int roundCorners = 2;
            var preference = roundCorners;
            _ = DwmSetWindowAttribute(
                new WindowInteropHelper(this).Handle,
                dwmWindowCornerPreference,
                ref preference,
                Marshal.SizeOf<int>());
        }

        UpdateWindowShape();
    }

    private void UpdateWindowShape()
    {
        if (RootFrame is null)
        {
            return;
        }

        var isMaximized = WindowState == WindowState.Maximized;
        RootFrame.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(14);
        RootFrame.BorderThickness = isMaximized ? new Thickness(0) : new Thickness(1);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
