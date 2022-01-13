namespace Diz.PowerShell;

public class DizPowershellLogger : IDizLogger
{
    private readonly IPowershellLogger powershellLogger;

    public DizPowershellLogger(IPowershellLogger powershellLogger)
    {
        this.powershellLogger = powershellLogger;
    }

    public void Info(string msg) => 
        powershellLogger.WriteObject(msg);

    public void Warn(string msg) => 
        powershellLogger.WriteCommandDetail(msg);

    public void Error(string msg) =>
        powershellLogger.WriteObject(msg);

    public void Debug(string msg) => 
        powershellLogger.WriteDebug(msg);
}