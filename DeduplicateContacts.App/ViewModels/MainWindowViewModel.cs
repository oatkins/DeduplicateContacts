using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Graph.Models;
using ReactiveUI;

namespace DeduplicateContacts.App.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly BehaviorSubject<bool> _canLoadContactsSubject = new(true);
    private Contacts? _contacts;

    public MainWindowViewModel()
    {
        GetContactsCommand = ReactiveCommand.Create(() => LoadContactsAsync(), _canLoadContactsSubject);
    }

    public nint WindowHandle { get; set; }

    public ICommand GetContactsCommand { get; }

    public ObservableCollection<Contact> Contacts { get; } = [];

    private async Task LoadContactsAsync()
    {
        _canLoadContactsSubject.OnNext(false);
        try
        {
            Contacts.Clear();

            if (_contacts == null)
            {
                var res = await Authorization.AcquireAuthorizationAsync(WindowHandle);
                if (res != null)
                {
                    _contacts = new(res);
                }
            }

            if (_contacts != null)
            {
                await foreach(var c in _contacts.GetContactsAsync())
                {
                    Contacts.Add(c);
                }

                var interesting = from c in Contacts
                                  where c.GivenName != null
                                  group c by c.GivenName!.Split(' ')[0] into g
                                  where g.Count() > 1
                                  from i in g
                                  select i;

                var toRemove = Contacts.Except(interesting).ToList();

                foreach(var r in toRemove)
                {
                    Contacts.Remove(r);
                }
            }
        }
        finally
        {
            _canLoadContactsSubject.OnNext(true);
        }
    }
}
