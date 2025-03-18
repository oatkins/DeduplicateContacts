using System.Diagnostics;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;

namespace DeduplicateContacts;

public static class Authorization
{
    // Below are the clientId (Application Id) of your app registration and the tenant information. 
    // You have to replace:
    // - the content of ClientID with the Application Id for your app registration
    // - The content of Tenant by the information about the accounts allowed to sign-in in your application:
    //   - For Work or School account in your org, use your tenant ID, or domain
    //   - for any Work or School accounts, use organizations
    //   - for any Work or School accounts, or Microsoft personal account, use consumers
    //   - for Microsoft Personal account, use consumers
    private const string ClientId = "c79f6798-0c41-46ca-a31a-c95c244f23f1";

    private const string Tenant = "consumers";
    private const string Instance = "https://login.microsoftonline.com/";
    private static readonly IPublicClientApplication _deduplicateContactsApp;

    //Set the API Endpoint to Graph 'me' endpoint. 
    // To change from Microsoft public cloud to a national cloud, use another value of _graphAPIEndpoint.
    // Reference with Graph endpoints here: https://docs.microsoft.com/graph/deployments#microsoft-graph-and-graph-explorer-service-root-endpoints
    private const string _graphAPIEndpoint = "https://graph.microsoft.com/v1.0/me";

    //Set the scope for API call to user.read
    private static readonly string[] _scopes = ["user.read", "Contacts.ReadWrite"];

    public static IPublicClientApplication DeduplicateContactsApp => _deduplicateContactsApp;

    static Authorization()
    {
        _deduplicateContactsApp = CreateApplication();
    }

    private static IPublicClientApplication CreateApplication()
    {
        BrokerOptions? brokerOptions = new(BrokerOptions.OperatingSystems.Windows);

        var c = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority($"{Instance}{Tenant}")
            .WithDefaultRedirectUri()
            .WithBroker(brokerOptions)
            .Build();

        MsalCacheHelper? cacheHelper = CreateCacheHelperAsync().GetAwaiter().GetResult();

        // Let the cache helper handle MSAL's cache, otherwise the user will be prompted to sign-in every time.
        cacheHelper.RegisterCache(c.UserTokenCache);

        return c;
    }

    private static async Task<MsalCacheHelper> CreateCacheHelperAsync()
    {
        // Since this is a WPF application, only Windows storage is configured
        var storageProperties = new StorageCreationPropertiesBuilder(
                          System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".msalcache.bin",
                          MsalCacheHelper.UserRootDirectory)
                            .Build();

        MsalCacheHelper? cacheHelper = await MsalCacheHelper.CreateAsync(
                    storageProperties,
                    new TraceSource("MSAL.CacheTrace"))
                 .ConfigureAwait(false);

        return cacheHelper;
    }
    public static async Task<AuthenticationResult?> AcquireAuthorizationAsync(IntPtr parentWindowHandle, CancellationToken cancellationToken)
    {
        var app = DeduplicateContactsApp;

        // if the user signed-in before, remember the account info from the cache
        IAccount? firstAccount = (await app.GetAccountsAsync()).FirstOrDefault();

        // otherwise, try with the Windows account
        firstAccount ??= PublicClientApplication.OperatingSystemAccount;

        AuthenticationResult? authResult;
        try
        {
            authResult = await app.AcquireTokenSilent(_scopes, firstAccount)
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException ex)
        {
            // A MsalUiRequiredException happened on AcquireTokenSilent. 
            // This indicates you need to call AcquireTokenInteractive to acquire a token
            Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

            try
            {
                authResult = await app.AcquireTokenInteractive(_scopes)
                    .WithAccount(firstAccount)
                    .WithParentActivityOrWindow(parentWindowHandle)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync(cancellationToken);
            }
            catch (MsalException)
            {
                throw;
            }
        }

        return authResult;
    }
}
