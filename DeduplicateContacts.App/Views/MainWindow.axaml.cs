using System.Collections.Immutable;
using System.Linq;
using Avalonia.Controls;
using DeduplicateContacts.App.ViewModels;
using Microsoft.Graph.Models;

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
        var vm = (MainWindowViewModel?)DataContext;
        if (vm == null)
        {
            return;
        }

        vm.WindowHandle = TryGetPlatformHandle()?.Handle ?? 0;

        Contacts.SelectionChanged += ContactsSelectionChanged;
    }

    private void ContactsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var s = Contacts.SelectedItems.Cast<ContactSummary>().ToImmutableHashSet();
        var vm = (MainWindowViewModel?)DataContext;

        if (vm == null)
        {
            return;
        }

        var changes = s.SymmetricExcept(vm.ContactsToRemove);
        foreach (var change in changes)
        {
            if (s.Contains(change))
            {
                vm.ContactsToRemove.Add(change);
            }
            else
            {
                vm.ContactsToRemove.Remove(change);
            }
        }
    }
}