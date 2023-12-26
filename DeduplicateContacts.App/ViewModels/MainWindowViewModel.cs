using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace DeduplicateContacts.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BehaviorSubject<bool> _canLoadContactsSubject = new(true);
    private readonly SourceCache<ContactSummary, string> _contacts;
    private readonly ObservableAsPropertyHelper<IEnumerable<ContactSummary>?> _selectedContactsProperty;

    private Contacts? _connection;

    private IGroup<ContactSummary, string, string>? _selectedGroup;

    public MainWindowViewModel()
    {
        GetContactsCommand = ReactiveCommand.CreateFromTask(ct => LoadContactsAsync(ct), _canLoadContactsSubject);

        var canRemoveContactsObservable = ContactsToRemove.ObserveCollectionChanges()
            .Select(_ => ContactsToRemove.Count > 0)
            .DistinctUntilChanged();

        RemoveContactsCommand = ReactiveCommand.CreateFromTask(ct => RemoveContactsAsync(ct), canRemoveContactsObservable);

        _contacts = new SourceCache<ContactSummary, string>(x => x.Id!);

        _contacts.Connect()
            .Group(x => x.DisplayName?.Trim() ?? string.Empty)
            //.FilterOnObservable(g => g.Cache.CountChanged.Select(x => x > 1))
            .SortBy(x => x.Key)
            .Bind(out ReadOnlyObservableCollection<IGroup<ContactSummary, string, string>> groups)
            .Subscribe();

        _selectedContactsProperty = this.WhenAnyValue(x => x.SelectedGroup)
            .Select(x => x?.Cache.Items)
            .ToProperty(this, x => x.SelectedContacts);

        Groups = groups;
    }

    public nint WindowHandle { get; set; }

    public IEnumerable<ContactSummary>? SelectedContacts => _selectedContactsProperty.Value;

    public ObservableCollection<ContactSummary> ContactsToRemove { get; } = [];

    public ICommand GetContactsCommand { get; }

    public ICommand RemoveContactsCommand { get; }

    public ReadOnlyObservableCollection<IGroup<ContactSummary, string, string>> Groups { get; }

    public IGroup<ContactSummary, string, string>? SelectedGroup
    {
        get => _selectedGroup;
        set => this.RaiseAndSetIfChanged(ref _selectedGroup, value);
    }

    private async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            return true;
        }

        var res = await Authorization.AcquireAuthorizationAsync(WindowHandle, cancellationToken);
        if (res != null)
        {
            _connection = new(res);
            return true;
        }

        return false;
    }

    private async Task RemoveContactsAsync(CancellationToken cancellationToken = default)
    {
        if (!(await ConnectAsync(cancellationToken)) || _connection == null)
        {
            return;
        }
        
        var toRemove = ContactsToRemove.ToList();

        await _connection.DeleteContactsAsync(toRemove, cancellationToken);

        foreach (var c in toRemove)
        {
            _contacts.Remove(c);
        }
    }

    private async Task LoadContactsAsync(CancellationToken cancellationToken = default)
    {
        _canLoadContactsSubject.OnNext(false);
        try
        {
            _contacts.Clear();

            if (!(await ConnectAsync(cancellationToken)))
            {
                return;
            }

            if (_connection != null)
            {
                await foreach (var c in _connection.GetContactsAsync(cancellationToken))
                {
                    _contacts.AddOrUpdate(c);
                }
            }
        }
        finally
        {
            _canLoadContactsSubject.OnNext(true);
        }
    }
}
