#nullable enable

namespace Diz.PowerShell;

public interface IDizLogger
{
    void Info(string msg);
    void Warn(string msg);
    void Error(string s);
    void Debug(string msg);
}
