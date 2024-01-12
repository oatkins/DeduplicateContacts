using System.Runtime.CompilerServices;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Me.Contacts;
using Microsoft.Graph.Beta.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace DeduplicateContacts;

public record ContactSummary(string? DisplayName, string? EmailAddresses, string? Phone, string? LastName, string? FirstName, string? Categories, string Id, string ParentFolderId, DateTimeOffset? CreatedDate, DateTimeOffset? UpdatedDate)
{
    public string? Folder { get; init; }

    public ContactSummary(Contact c)
        : this(
              c.DisplayName,
              c.EmailAddresses == null ? null : string.Join(", ", c.EmailAddresses.Select(x => x.Address)),
              c.Phones == null ? null : string.Join(", ", c.Phones.Select(x => x.Number)),
              c.Surname,
              c.GivenName,
              c.Categories == null ? null : string.Join(", ", c.Categories),
              c.Id ?? string.Empty,
              c.ParentFolderId ?? string.Empty,
              c.CreatedDateTime,
              c.LastModifiedDateTime)
    {
    }
}

public class Contacts(AuthenticationResult authenticationResult)
{
    private readonly GraphServiceClient _client = new(new CompletedAuthenticationProvider(authenticationResult));

    public async Task DeleteContactsAsync(IEnumerable<ContactSummary> contacts, CancellationToken cancellationToken)
    {
        var folders = await _client.Me.ContactFolders.GetAsync(null, cancellationToken);

        foreach (var c in contacts)
        {
            var folder = folders?.Value?.FirstOrDefault(f => f.Id == c.ParentFolderId);
            if (folder == null)
            {
                await _client.Me.Contacts[c.Id].DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _client.Me.ContactFolders[folder.Id].Contacts[c.Id].DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async IAsyncEnumerable<ContactSummary> GetContactsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var categories = await _client.Me.Outlook.MasterCategories.GetAsync(null, cancellationToken);

        var folders = await _client.Me.ContactFolders.GetAsync(null, cancellationToken);

        var requests = from f in folders?.Value ?? []
                       select (Request: _client.Me.Contacts.WithUrl($"{_client.RequestAdapter.BaseUrl}/me/contactfolders/{f.Id}/contacts"), Folder: f.DisplayName);

        requests = requests.Prepend((_client.Me.Contacts, null));

        foreach (var request in requests)
        {
            await foreach (var contact in GetContactsAsync(request.Request, cancellationToken))
            {
                yield return contact with { Folder = request.Folder };
            }
        }
    }

    public async IAsyncEnumerable<ContactSummary> GetContactsAsync(ContactsRequestBuilder request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var contacts = await request.GetAsync(null, cancellationToken).ConfigureAwait(false);
        if (contacts?.Value == null)
        {
            yield break;
        }

        var ids = new HashSet<string>();

        foreach (var contact in contacts.Value)
        {
            if (contact.Id != null && ids.Add(contact.Id))
            {
                yield return new(contact);
            }
        }

        while (contacts?.OdataNextLink != null)
        {
            contacts = await _client.Me.Contacts.WithUrl(contacts.OdataNextLink).GetAsync(null, cancellationToken).ConfigureAwait(false);
            foreach (var contact in contacts?.Value ?? Enumerable.Empty<Contact>())
            {
                if (contact.Id != null && ids.Add(contact.Id))
                {
                    yield return new(contact);
                }
            }
        }
    }

    public class CompletedAuthenticationProvider(AuthenticationResult authenticationResult) : IAuthenticationProvider
    {
        private readonly AuthenticationResult _authenticationResult = authenticationResult;

        public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            request.Headers.Add("Authorization", _authenticationResult.CreateAuthorizationHeader());
            return Task.CompletedTask;
        }
    }
}
