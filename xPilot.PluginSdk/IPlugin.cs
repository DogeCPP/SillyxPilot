namespace Vatsim.Xpilot.PluginSdk;

/// <summary>
/// The main xPilot plugin interface. Implement this interface in your plugin class.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Gets the name of the plugin. Used for logging purposes.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Called by xPilot after the plugin is instantiated. Store the broker reference and subscribe to events here.
    /// </summary>
    /// <param name="broker">An <see cref="IBroker"/> instance for interacting with xPilot.</param>
    public void Initialize(IBroker broker);
}
