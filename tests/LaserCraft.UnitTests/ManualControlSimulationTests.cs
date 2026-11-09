using LaserCraft.Devices;
using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class ManualControlSimulationTests
{
    [Fact]
    public void PhysicalProfilesCannotAcquireManualControls()
    {
        foreach (var profile in new[] { DeviceProfile.Simulator() with { Connection = ConnectionKind.Serial, SerialPort = "COM1" }, DeviceProfile.Simulator() with { Connection = ConnectionKind.Tcp, Host = "localhost" } })
            Assert.Throws<InvalidDataException>(() => new ManualControlSimulation(profile));
    }
    [Fact]
    public void JogChecksBoundsBeforeChangingPositionOrTranscript()
    {
        var sim = new ManualControlSimulation(DeviceProfile.Simulator() with { BedWidth = 10, BedHeight = 20 });
        sim.Jog(10, 20, 600); Assert.Equal(new PointMm(10, 20), sim.Position);
        Assert.Equal(new[] { "M5", "$J=G21 G91 X10 Y20 F600" }, sim.Commands);
        foreach (var movement in new[] { new PointMm(1, 0), new(-11, 0), new(0, 1), new(0, -21), new(double.NaN, 0), new(0, 0) })
            Assert.Throws<InvalidDataException>(() => sim.Jog(movement.X, movement.Y, 600));
        Assert.Throws<InvalidDataException>(() => sim.Jog(-1, 0, double.PositiveInfinity));
        Assert.Equal(new PointMm(10, 20), sim.Position); Assert.Equal(2, sim.Commands.Count);
    }
    [Fact]
    public void PulseQuantizesDownAndExpiresOrStopsExplicitly()
    {
        var sim = new ManualControlSimulation(DeviceProfile.Simulator() with { MaxPower = 255 });
        sim.StartPulse(1, 100); Assert.Equal(2, sim.SValue);
        Assert.Equal(new[] { "M3 S2", "G4 P0.1" }, sim.Commands);
        Assert.Throws<InvalidDataException>(() => sim.Jog(1, 0, 600));
        Assert.Throws<InvalidDataException>(() => sim.StartPulse(1, 100));
        sim.Advance(TimeSpan.FromMilliseconds(99)); Assert.True(sim.IsPulsing);
        sim.Advance(TimeSpan.FromMilliseconds(1)); Assert.False(sim.IsPulsing); Assert.Equal("M5", sim.Commands[^1]);
        sim.StartPulse(5, 500); sim.Stop(); Assert.Equal(0, sim.SValue); Assert.Equal(0, sim.RemainingPulseSeconds);
    }
    [Theory]
    [InlineData(0, 100)] [InlineData(5.1, 100)] [InlineData(double.NaN, 100)] [InlineData(1, 0)] [InlineData(1, 501)]
    public void InvalidPulsesLeaveSimulatorOff(double percent, int duration)
    {
        var sim = new ManualControlSimulation(DeviceProfile.Simulator());
        Assert.Throws<InvalidDataException>(() => sim.StartPulse(percent, duration)); Assert.False(sim.IsPulsing); Assert.Empty(sim.Commands);
    }
    [Fact]
    public void SubStepPowerIsRejectedAndTranscriptIsBounded()
    {
        var sim = new ManualControlSimulation(DeviceProfile.Simulator() with { MaxPower = 1 });
        Assert.Throws<InvalidDataException>(() => sim.StartPulse(5, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Advance(TimeSpan.FromTicks(-1)));
        for (int i = 0; i < 300; i++) sim.Stop();
        Assert.Equal(256, sim.Commands.Count); Assert.Equal(44, sim.DroppedCommands);
    }
    [Fact]
    public async Task MonitorReportsTheSameSimulationAndDisconnectStopsPulse()
    {
        var transport = new SimulatedGrblTransport(DeviceProfile.Simulator() with { BedWidth = 123, MaxPower = 255 });
        var monitor = await GrblMonitor.ConnectAsync(transport); var sim = Assert.IsType<ManualControlSimulation>(monitor.Simulation);
        sim.Jog(1.25, 2.5, 600); sim.StartPulse(5, 500);
        var status = await monitor.QueryStatusAsync(); Assert.Equal(1.25, status.X); Assert.Equal(2.5, status.Y); Assert.Equal(12d, status.Spindle); Assert.Equal("Run", status.State);
        Assert.Equal(new[] { "$I\n", "?" }, transport.Commands);
        await monitor.DisposeAsync(); Assert.False(sim.IsPulsing);
    }
    [Fact]
    public void AirOutputIsValidatedAndM7ExportStillTurnsOffWithM9()
    {
        Assert.Equal(AirAssistOutput.M8, DeviceProfile.Simulator().AirOutput);
        Assert.Throws<InvalidDataException>(() => (DeviceProfile.Simulator() with { AirOutput = (AirAssistOutput)99 }).Validate());
        var plan = new ToolpathPlan([new(new(0, 0), new(1, 1), 0, false, 600, true, 10)]);
        var code = GrblExport.Write(plan, DeviceProfile.Simulator() with { AirAssistEnabled = true, AirOutput = AirAssistOutput.M7 });
        Assert.Contains("M7\n", code); Assert.DoesNotContain("M8\n", code); Assert.EndsWith("M5\nM9\n", code);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var p = DeviceProfile.Simulator() with { AirOutput = AirAssistOutput.M7 }; ProfileStore.Save(path, [p]); Assert.Equal(p, Assert.Single(ProfileStore.Load(path)));
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            legacy[0]!.AsObject().Remove("AirOutput"); File.WriteAllText(path, legacy.ToJsonString());
            Assert.Equal(AirAssistOutput.M8, Assert.Single(ProfileStore.Load(path)).AirOutput);
        }
        finally { File.Delete(path); }
    }
}
