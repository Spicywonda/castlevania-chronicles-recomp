using RecompOne.Runtime;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Sdk;

const uint IrqAddress = 0x1F801DA4;
const uint Control = 0x1F801DAA;
const uint Status = 0x1F801DAE;
var failures = 0;
void Check(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

Check("SPU IRQ address readback", () =>
{
    var spu = new Spu();
    spu.WriteReg16(IrqAddress, 0x1234);
    Require(spu.ReadReg16(IrqAddress) == 0x1234, "IRQ register lost its value");
});
Check("SPU DMA IRQ enable, match, acknowledge and rearm", () =>
{
    var spu = new Spu();
    spu.WriteReg16(IrqAddress, 0x200);
    spu.DmaWrite(0x1000, new byte[16]);
    Require((spu.ReadReg16(Status) & 0x40) == 0, "IRQ fired while disabled");
    spu.WriteReg16(Control, 0xC040);
    spu.DmaWrite(0x1010, new byte[16]);
    Require((spu.ReadReg16(Status) & 0x40) == 0, "IRQ fired outside the target");
    spu.DmaWrite(0x1000, new byte[16]);
    Require((spu.ReadReg16(Status) & 0x40) != 0, "Matching DMA did not set IRQ");
    spu.WriteReg16(Control, 0xC000);
    Require((spu.ReadReg16(Status) & 0x40) == 0, "Acknowledge did not clear IRQ");
    spu.WriteReg16(Control, 0xC040);
    spu.DmaWrite(0x1000, new byte[16]);
    Require((spu.ReadReg16(Status) & 0x40) != 0, "IRQ failed to rearm");
});
Check("SPU ADPCM read raises IRQ", () =>
{
    var spu = new Spu();
    spu.DmaWrite(0x1000, new byte[16]);
    spu.WriteReg16(0x1F801C04, 0x1000);
    spu.WriteReg16(0x1F801C06, 0x200);
    spu.WriteReg16(IrqAddress, 0x200);
    spu.WriteReg16(Control, 0xC040);
    spu.WriteReg16(0x1F801D88, 1);
    spu.Mix(new short[128], 64);
    Require((spu.ReadReg16(Status) & 0x40) != 0, "ADPCM block read did not set IRQ");
});
Check("SPU DMA address wrap", () =>
{
    var spu = new Spu();
    spu.WriteReg16(IrqAddress, 0);
    spu.WriteReg16(Control, 0xC040);
    spu.DmaWrite(Spu.RamSize - 8, new byte[16]);
    Require((spu.ReadReg16(Status) & 0x40) != 0, "Wrapped transfer did not hit address zero");
});
Check("SPU IRQ reaches CPU interrupt controller", () =>
{
    var memory = new RecompOne.Runtime.Memory.PSMemory();
    var cpu = new RecompOne.Runtime.Context.CpuContext();
    var spu = new Spu();
    RecompOne.Runtime.Runtime.Spu = spu;
    Interrupts.ClearPending();
    Interrupts.WriteMask(0);
    spu.WriteReg16(IrqAddress, 0x200);
    spu.WriteReg16(Control, 0xC040);
    spu.DmaWrite(0x1000, new byte[16]);
    Interrupts.PollNow(cpu, memory);
    Require((Interrupts.ReadStat() & (1u << 9)) != 0, "SPU IRQ did not reach I_STAT bit 9");
    Interrupts.WriteStat(0);
    Interrupts.PollNow(cpu, memory);
    Require((Interrupts.ReadStat() & (1u << 9)) == 0, "Same request was delivered twice");
    Interrupts.ClearPending();
    Interrupts.WriteMask(0x7FF);
});
Check("SPU mixer yields pending IRQ without blocking register writes", () =>
{
    var spu = new Spu();
    spu.WriteReg16(IrqAddress, 0x200);
    spu.WriteReg16(Control, 0xC040);
    spu.DmaWrite(0x1000, new byte[16]);
    using var started = new ManualResetEventSlim();
    var waiter = Task.Run(() => { started.Set(); spu.WaitForIrqService(1000); });
    started.Wait();
    var returnedBeforeAck = waiter.Wait(30);
    spu.WriteReg16(Control, 0xC000);
    var returnedBeforeRefill = waiter.Wait(10);
    spu.DmaWrite(0x1000, new byte[16]);
    spu.CompleteIrqService();
    Require(waiter.Wait(500), "Mixer did not resume after IRQ acknowledgement");
    Require(!returnedBeforeAck, "Mixer ran ahead before the pending IRQ was acknowledged");
    Require(!returnedBeforeRefill, "Mixer resumed on acknowledgement before DMA refill completed");
});
Check("SPU mixer wait is bounded if CPU cannot service IRQ", () =>
{
    var spu = new Spu();
    spu.WriteReg16(IrqAddress, 0x200);
    spu.WriteReg16(Control, 0xC040);
    spu.DmaWrite(0x1000, new byte[16]);
    Require(Task.Run(() => spu.WaitForIrqService(5)).Wait(500), "Unserviced IRQ deadlocked mixer");
});
Check("SPU synchronized mix yields when IRQ occurs inside an output buffer", () =>
{
    var spu = new Spu();
    spu.DmaWrite(0x1000, new byte[16]);
    spu.WriteReg16(0x1F801C04, 0x1000);
    spu.WriteReg16(0x1F801C06, 0x200);
    spu.WriteReg16(IrqAddress, 0x200);
    spu.WriteReg16(Control, 0xC040);
    spu.WriteReg16(0x1F801D88, 1);
    var mixer = Task.Run(() => spu.Mix(new short[512], 256, synchronizeIrq: true));
    Require(SpinWait.SpinUntil(() => (spu.ReadReg16(Status) & 0x40) != 0, 1000), "No IRQ from block read");
    var ranAhead = mixer.Wait(5);
    spu.WriteReg16(Control, 0xC000);
    spu.CompleteIrqService();
    Require(mixer.Wait(500), "Mixer did not finish after IRQ acknowledgement");
    Require(!ranAhead, "Mixer consumed the entire output buffer before servicing its IRQ");
});
foreach (var mode in new uint[] { 0x80, 0xA0 })
Check($"LibDs/CdGetSector shared cursor mode {mode:X2}", () =>
{
    var memory = new PSMemory();
    var cpu = new CpuContext { SP = 0x801FF000 };
    using var fs = DiscFs.FromImage(new SyntheticDisc());
    memory.SetCd(new CdController(fs, memory));
    RecompOne.Runtime.Runtime.SetContext(cpu, memory);
    LibDs.DsInit(cpu, memory);
    var called = false;
    Dispatcher.Register("sector-test", new SectorOverlay((c, m) =>
    {
        if (c.A0 != 1) return;
        called = true;
        c.A0 = 0x80010000; c.A1 = 128;
        LibCd.CdGetSector(c, m);
        c.A0 = 0x80010200; c.A1 = 128;
        LibDs.DsGetSector(c, m);
        c.A0 = 0x80010400; c.A1 = 256;
        LibCd.CdGetSector(c, m);
    }));
    Dispatcher.Load("sector-test");
    cpu.A0 = 0x80100000; cpu.A1 = uint.MaxValue;
    LibDs.DsStartReadySystem(cpu, memory);
    memory.WriteU8(0x80008001, 2);
    cpu.A0 = mode; cpu.A1 = 0x80008000; cpu.A2 = 6; cpu.A3 = 0;
    LibDs.DsPacket(cpu, memory);
    cpu.A0 = cpu.V0; cpu.A1 = 0;
    LibDs.DsSync(cpu, memory);
    try
    {
        Require(called, "No sector-ready callback");
        for (var i = 0; i < 2048; i++)
            Require(memory.ReadU8(0x80010000u + (uint)i) == SyntheticDisc.Payload(i), $"Payload differs at byte {i}");
    }
    finally { LibDs.DsClose(cpu, memory); Dispatcher.Unload("sector-test"); }
});
Check("Pad state tracks Start release and re-press without presenting frames", () =>
{
    var memory = new PSMemory();
    var cpu = new CpuContext { A0 = 0x80008000, A1 = 0x80008020 };
    LibPad.PadInitDirect(cpu, memory);
    RecompOne.Runtime.Hardware.Controller.State = 0xFFF7;
    LibPad.PadStartCom(cpu, memory);
    foreach (ushort buttons in new ushort[] { 0xFFFF, 0xFFF7, 0xFFFF })
    {
        RecompOne.Runtime.Hardware.Controller.State = buttons;
        cpu.A0 = 0;
        LibPad.PadGetState(cpu, memory);
        Require(cpu.V0 == 6, "First controller became disconnected");
        Require(memory.ReadU16(0x80008002) == buttons, "Pause loop read stale controller buttons");
    }
});
return failures == 0 ? 0 : 1;

sealed class SectorOverlay(Action<CpuContext, IMemory> callback) : IOverlay
{
    public string Name => "sector-test";
    public IReadOnlyDictionary<uint, Action<CpuContext, IMemory>> Functions { get; } =
        new Dictionary<uint, Action<CpuContext, IMemory>> { [0x80100000] = callback };
}
sealed class SyntheticDisc : IDiscImage
{
    public string Format => "Synthetic";
    public int FirstTrack => 1;
    public int LastTrack => 1;
    public bool HasTracks => true;
    public int LeadoutLba => 100;
    public int DataSectors => 100;
    public IReadOnlyList<DiscTrack> Tracks => new[] { new DiscTrack(1, DiscTrackKind.Data, 0, 2352, 0) };
    public bool TrackStartLba(int track, out int lba) { lba = 0; return track == 1; }
    public static byte Payload(int i) => (byte)((i * 37 + i / 256) % 251);
    public byte[] ReadSectorData(int lba, int size)
    {
        var data = new byte[size];
        var head = size >= 2340 ? 12 : 0;
        Array.Fill(data, (byte)0xFE, 0, head);
        for (var i = head; i < data.Length; i++) data[i] = Payload(i - head);
        return data;
    }
    public byte[] ReadRawSector(int lba) => new byte[2352];
    public void Dispose() { }
}
