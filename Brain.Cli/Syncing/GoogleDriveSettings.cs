// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Settings;

namespace Brain.Cli.Syncing;

internal sealed class GoogleDriveSettings : UserSettingsBase
{
    protected override string SettingsFileName => "google-drive.json";

    public string ClientId
    {
        get => Get<string>();
        set => Set(value);
    }

    public string ClientSecret
    {
        get => Get<string>();
        set => Set(value);
    }

    public Dictionary<string, string> Tokens
    {
        get => Get<Dictionary<string, string>>();
        set => Set(value);
    }

    protected override void ApplyDefaults()
    {
        Tokens = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public void Clear()
    {
        ClientId = null;
        ClientSecret = null;
        Tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        Save();
    }
}
