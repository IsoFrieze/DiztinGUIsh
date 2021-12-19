using Diz.Core.model;
using Diz.Core.model.snes;

namespace Diz.Core.arch;

public class CpuDispatcher
{
    private Cpu cpuDefault;
    private Cpu65C816 cpu65C816;
    private CpuSpc700 cpuSpc700;
    private CpuSuperFx cpuSuperFx;

    public Cpu Cpu(Data data, int offset)
    {
        var arch = data.GetArchitecture(offset);

        return arch switch
        {
            Architecture.Cpu65C816 => cpu65C816 ??= new Cpu65C816(),
            Architecture.Apuspc700 => cpuSpc700 ??= new CpuSpc700(),
            Architecture.GpuSuperFx => cpuSuperFx ??= new CpuSuperFx(),
            _ => cpuDefault ??= new Cpu()
        };
    }
}