namespace GabsHybridApp.Shared.Services;

public interface IDeviceIdProvider
{
    string GetDeviceId();
}

public class DefaultDeviceIdProvider : IDeviceIdProvider
{
    private static readonly string _id = $"DEV-{Guid.NewGuid():N}"[..12].ToUpper();
    public string GetDeviceId() => _id;
}
