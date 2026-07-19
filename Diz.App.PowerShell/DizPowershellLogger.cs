using System;
using System.Management.Automation;

namespace Diz.PowerShell;

/// <summary>
/// IDizLogger that writes to the owning cmdlet's PowerShell streams. Only safe to use
/// synchronously from within the cmdlet's Begin/Process/EndProcessing (PowerShell
/// rejects stream writes from other threads, and export is synchronous anyway).
/// </summary>
public class CmdletDizLogger : IDizLogger
{
    private readonly Cmdlet cmdlet;

    public CmdletDizLogger(Cmdlet cmdlet) => this.cmdlet = cmdlet;

    public void Info(string msg) =>
        cmdlet.WriteObject(msg);

    public void Warn(string msg) =>
        cmdlet.WriteWarning(msg);

    // non-terminating: report and let the cmdlet keep processing remaining projects
    public void Error(string msg) =>
        cmdlet.WriteError(new ErrorRecord(
            new InvalidOperationException(msg), "DizExportError", ErrorCategory.InvalidOperation, null));

    public void Debug(string msg) =>
        cmdlet.WriteVerbose(msg);
}
