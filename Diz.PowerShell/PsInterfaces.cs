#nullable enable

namespace Diz.PowerShell;

public interface IPowershellLogger
{
    void WriteObject(object objectToSend);
    void WriteDebug(string text);
    void WriteCommandDetail(string text);
}

public interface IDizLogger
{
    void Info(string msg);
    void Warn(string msg);
    void Error(string s);
    void Debug(string msg);
}