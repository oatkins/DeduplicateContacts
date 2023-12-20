using Avalonia.Controls;
using DeduplicateContacts.App.ViewModels;

namespace DeduplicateContacts.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindowLoaded;
    }

    private void MainWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((MainWindowViewModel)DataContext!).WindowHandle = TryGetPlatformHandle()?.Handle ?? 0;
    }
}