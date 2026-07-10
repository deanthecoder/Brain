// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Google.Apis.Util.Store;
using System.Text.Json;

namespace Brain.Cli.Syncing;

internal sealed class GoogleDriveTokenStore : IDataStore
{
    private readonly GoogleDriveSettings m_settings;

    public GoogleDriveTokenStore(GoogleDriveSettings settings)
    {
        m_settings = settings;
    }

    public Task StoreAsync<T>(string key, T value)
    {
        var tokens = GetTokens();
        tokens[key] = JsonSerializer.Serialize(value);
        Save(tokens);
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        var tokens = GetTokens();
        tokens.Remove(key);
        Save(tokens);
        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        var tokens = GetTokens();
        return Task.FromResult(tokens.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json) : default);
    }

    public Task ClearAsync()
    {
        Save(new Dictionary<string, string>(StringComparer.Ordinal));
        return Task.CompletedTask;
    }

    public bool HasTokens() => GetTokens().Count > 0;

    private Dictionary<string, string> GetTokens() => m_settings.Tokens ?? new Dictionary<string, string>(StringComparer.Ordinal);

    private void Save(Dictionary<string, string> tokens)
    {
        m_settings.Tokens = tokens;
        m_settings.Save();
    }
}
