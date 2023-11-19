namespace Diz.Core.commands;

public class MarkCommand
{
    public enum MarkManyProperty
    {
        Flag = 0,
        DataBank = 1,
        DirectPage = 2,
        MFlag = 3,
        XFlag = 4,
        CpuArch = 5,
    };
        
    public MarkManyProperty Property { get; set; }
    public int Start { get; set; }
    public int Count { get; set; }
    public object Value { get; set; }
}