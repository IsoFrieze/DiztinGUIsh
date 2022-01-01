using Diz.Core.model;

namespace Diz.Core.Interfaces;

// wonder if we could ditch this one by using extension methods
public interface ISteppable
{
    int Step(int offset, bool branch, bool force, int prevOffset);
}

public interface IAutoSteppable
{
    public int AutoStepSafe(int offset);
    public int AutoStepHarsh(int offset, int count);
}