using System.Runtime.CompilerServices;
using Azure.Core;
using Microsoft.Graph;
using Microsoft.Graph.Me.Contacts;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace DeduplicateContacts;

public class Contacts(AuthenticationResult authenticationResult)
{
    private readonly GraphServiceClient _client = new(new CompletedAuthenticationProvider(authenticationResult));

    public async IAsyncEnumerable<Contact> GetContactsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folders = await _client.Me.ContactFolders.GetAsync(null, cancellationToken);

        var requests = from f in folders?.Value ?? []
                       select _client.Me.Contacts.WithUrl($"{_client.RequestAdapter.BaseUrl}/me/contactfolders/{f.Id}/contacts");

        requests = requests.Prepend(_client.Me.Contacts);

        foreach(var request in requests)
        {
            await foreach (var contact in GetContactsAsync(request, cancellationToken))
            {
                yield return contact;
            }
        }
    }

    public async IAsyncEnumerable<Contact> GetContactsAsync(ContactsRequestBuilder request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var contacts = await request.GetAsync(null, cancellationToken).ConfigureAwait(false);
        if (contacts?.Value == null)
        {
            yield break;
        }

        foreach (var contact in contacts.Value)
        {
            yield return contact;
        }

        while (contacts?.OdataNextLink != null)
        {
            contacts = await _client.Me.Contacts.WithUrl(contacts.OdataNextLink).GetAsync(null, cancellationToken).ConfigureAwait(false);
            foreach (var contact in contacts?.Value ?? Enumerable.Empty<Contact>())
            {
                yield return contact;
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
