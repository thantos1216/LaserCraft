using System.Globalization;
using LaserCraft.Domain;
namespace LaserCraft.Devices;

public sealed partial class GrblMonitor
{
    private static bool HomingEnabled(IEnumerable<GrblSetting> settings)
    {
        var values=settings.Where(s=>s.Number==22).ToArray();
        return values.Length==1 && double.TryParse(values[0].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var value) && value==1;
    }

    public async Task<GrblManualResult> HomeAfterConnectAsync(DeviceProfile profile,CancellationToken token=default)
    {
        profile.Validate();
        if(profile.Connection==ConnectionKind.Simulator || profile.Controller!=ControllerFamily.Grbl)
            return new([],"Automatic homing skipped: physical GRBL profile required.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted)!=0,this);
        using var startup=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);
        await Task.Delay(TimeSpan.FromSeconds(1),startup.Token);
        var settings=await QuerySettingsAsync(startup.Token);
        if(!HomingEnabled(settings))return new([],"Connected; automatic homing skipped because firmware $22 is not enabled. No settings changed.");
        return await ExecuteManualAsync(profile,new(GrblManualAction.Home),true,cancellationToken:startup.Token);
    }
}
