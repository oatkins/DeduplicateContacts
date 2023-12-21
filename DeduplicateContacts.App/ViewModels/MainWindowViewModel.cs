using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using Microsoft.Graph.Models;
using ReactiveUI;

namespace DeduplicateContacts.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BehaviorSubject<bool> _canLoadContactsSubject = new(true);
    private readonly SourceCache<Contact, string> _contacts;
    private readonly ObservableAsPropertyHelper<IEnumerable<Contact>?> _selectedContactsProperty;

    private Contacts? _connection;

    private IGroup<Contact, string, string>? _selectedGroup;

    public MainWindowViewModel()
    {
        GetContactsCommand = ReactiveCommand.Create(() => LoadContactsAsync(), _canLoadContactsSubject);

        _contacts = new SourceCache<Contact, string>(x => x.Id!);

        _contacts.Connect()
            .Group(x => x.DisplayName?.Trim() ?? string.Empty)
            .FilterOnObservable(g => g.Cache.CountChanged.Select(x => x > 1))
            .SortBy(x => x.Key)
            .Bind(out ReadOnlyObservableCollection<IGroup<Contact, string, string>> groups)
            .Subscribe();

        _selectedContactsProperty = this.WhenAnyValue(x => x.SelectedGroup)
            .Select(x => x?.Cache.Items)
            .ToProperty(this, x => x.SelectedContacts);

        Groups = groups;
    }

    public nint WindowHandle { get; set; }

    public IEnumerable<Contact>? SelectedContacts => _selectedContactsProperty.Value;

    public ICommand GetContactsCommand { get; }

    public ReadOnlyObservableCollection<IGroup<Contact, string, string>> Groups { get; }

    public IGroup<Contact, string, string>? SelectedGroup
    {
        get => _selectedGroup;
        set => this.RaiseAndSetIfChanged(ref _selectedGroup, value);
    }

    private async Task LoadContactsAsync()
    {
        _canLoadContactsSubject.OnNext(false);
        try
        {
            _contacts.Clear();

            if (_connection == null)
            {
                var res = await Authorization.AcquireAuthorizationAsync(WindowHandle);
                if (res != null)
                {
                    _connection = new(res);
                }
            }

            if (_connection != null)
            {
                await foreach (var c in _connection.GetContactsAsync())
                {
                    _contacts.AddOrUpdate(c);
                }

                var nonDefaultProperties = (from c in _contacts.Items
                                            from p in c.BackingStore.Enumerate()
                                            select p.Key).Distinct();

                var countProperties = (from c in _contacts.Items
                                       from p in c.BackingStore.Enumerate()
                                       let cc = GetCount(p.Value)
                                       where cc > 0
                                       group cc by p.Key into g
                                       select (Name: g.Key, MaxValues: g.Max())).ToList();

                var ee = _contacts.Items.Where(c => c.EmailAddresses?.Select(x => x.Address).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1).ToList();

                static int? GetCount(object? v)
                {
                    return v switch
                    {
                        null => null,
                        System.Collections.ICollection cc => cc.Count,
                        { } t when t.GetType().IsGenericType && t.GetType().GetGenericTypeDefinition() == typeof(Tuple<,>) => ((dynamic)t).Item1.Count,
                        _ => null
                    };
                }

                var interesting = from c in _contacts.Items
                                  where c.GivenName != null
                                  group c by c.GivenName!.Split(' ')[0] into g
                                  where g.Distinct().Count() > 1
                                  from i in g
                                  select i;

                var toRemove = _contacts.Items.Except(interesting).ToList();

                foreach (var r in toRemove)
                {
                    _contacts.Remove(r);
                }
            }
        }
        finally
        {
            _canLoadContactsSubject.OnNext(true);
        }
    }
}
