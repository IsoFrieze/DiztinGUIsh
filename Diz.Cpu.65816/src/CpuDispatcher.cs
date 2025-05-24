using Diz.Core.Interfaces;

namespace Diz.Cpu._65816;

// TODO: has a lot of memory allocation with this approach. if that becomes an issue, cache the creations or use lazy loading
// TODO: this requires all data sources for any CPU to implement all interfaces for all CPUs, which is not going to
//       work if we want to add more different CPU types later

public class CpuDispatcher
{
    public Cpu<SnesApi> Cpu(SnesApi data, int offset)
    {
        var arch = data.Data.GetArchitecture(offset);

        return arch switch
        {
            Architecture.Cpu65C816 => new Cpu65C816<SnesApi>(),
            Architecture.Apuspc700 => new CpuSpc700<SnesApi>(),
            Architecture.GpuSuperFx => new CpuSuperFx<SnesApi>(),
            _ => new Cpu<SnesApi>()
        };
    }
}