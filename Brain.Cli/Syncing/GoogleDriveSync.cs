// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Brain.Cli.Storage;
using DTC.Core.Extensions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Brain.Cli.Syncing;

internal sealed class GoogleDriveSync : IBrainSynchroniser
{
    // Google treats installed-app credentials as public; PKCE protects each authorization exchange.
    private const string ClientId = "707137292996-927c9q5r8fj3sfg7lvl0hakf0bbq9v78.apps.googleusercontent.com";
    private const string ClientSecret = "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";
    private static readonly TimeSpan PullInterval = TimeSpan.FromHours(1);

    private readonly BrainStore m_store;
    private readonly GoogleDriveSettings m_settings = new();

    public GoogleDriveSync(BrainStore store)
    {
        m_store = store;
    }

    public bool IsConnected => new GoogleDriveTokenStore(m_settings).HasTokens();

    public bool CanSynchroniseAutomatically => IsConnected;

    public bool IsPullDue => DateTime.UtcNow - m_settings.LastPulledAtUtc >= PullInterval;

    public void Connect()
    {
        m_settings.LastPulledAtUtc = DateTime.MinValue;
        using var service = CreateService(false);
        m_settings.Save();
    }

    public DriveSyncResult Sync()
    {
        var pulled = Pull();
        var pushed = Push();

        return new DriveSyncResult(pushed.Uploaded, pulled.Downloaded);
    }

    public DriveSyncResult Pull()
    {
        using var service = CreateService();
        var remoteFiles = ListEntries(service)
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var localFiles = m_store.GetSyncFiles()
            .ToDictionary(x => x.Name, x => x, StringComparer.Ordinal);

        var downloaded = 0;
        foreach (var remoteFile in remoteFiles)
        {
            if (localFiles.ContainsKey(remoteFile.Key))
                continue;

            using var stream = new MemoryStream();
            service.Files.Get(remoteFile.Value.Id).Download(stream);
            stream.Position = 0;
            m_store.Import(remoteFile.Key, stream);
            downloaded++;
        }

        m_settings.LastPulledAtUtc = DateTime.UtcNow;
        m_settings.Save();

        return new DriveSyncResult(0, downloaded);
    }

    public DriveSyncResult Push()
    {
        using var service = CreateService();
        var remoteNames = ListEntries(service)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);
        var uploaded = 0;

        foreach (var localFile in m_store.GetSyncFiles())
        {
            if (remoteNames.Contains(localFile.Name))
                continue;

            using var stream = localFile.OpenRead();
            var request = service.Files.Create(new DriveFile
            {
                Name = localFile.Name,
                Parents = new List<string> { "appDataFolder" },
                MimeType = "application/json"
            }, stream, "application/json");
            request.Fields = "id";
            request.Upload();
            uploaded++;
        }

        return new DriveSyncResult(uploaded, 0);
    }

    public void Disconnect()
    {
        m_settings.Clear();
    }

    private DriveService CreateService(bool requireConnection = true)
    {
        if (requireConnection)
            EnsureConnected();

        var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = ClientId,
                        ClientSecret = ClientSecret
                    }
                },
                new[] { DriveService.Scope.DriveAppdata },
                "brain",
                true,
                CancellationToken.None,
                new GoogleDriveTokenStore(m_settings))
            .GetAwaiter()
            .GetResult();

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Brain"
        });
    }

    private IEnumerable<DriveFile> ListEntries(DriveService service)
    {
        string pageToken = null;

        do
        {
            var request = service.Files.List();
            request.Spaces = "appDataFolder";
            request.Q = "name contains 'entry-' or name contains 'forgotten-'";
            request.Fields = "nextPageToken, files(id, name)";
            request.PageToken = pageToken;

            var page = request.Execute();
            foreach (var file in page.Files)
                yield return file;

            pageToken = page.NextPageToken;
        } while (pageToken != null);
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new BrainUsageException("Google Drive is not connected. Run 'brain drive connect' first.");
    }
}

internal sealed record DriveSyncResult(int Uploaded, int Downloaded);
