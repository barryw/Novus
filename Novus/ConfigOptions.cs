using CommandLine;

namespace Novus;

[Verb("config", HelpText = "Show or change per-user settings, such as where your Amiga NDK lives")]
public class ConfigOptions
{
    [Value(0, MetaName = "action", Required = false,
        HelpText = "'show' (default) to print current settings, or 'set' to change one")]
    public string? Action { get; set; }

    [Value(1, MetaName = "key", Required = false, HelpText = "Setting to change: ndk-path")]
    public string? Key { get; set; }

    [Value(2, MetaName = "value", Required = false, HelpText = "New value for the setting")]
    public string? Value { get; set; }
}
