using System.ComponentModel;
using Diz.Controllers.interfaces;

namespace Diz.Ui.Eto;

// TODO: implement property changed notification. also,
// TODO: combine this with the other classes that do the same thing, don't rely on
//       winforms/etc for this
// ReSharper disable once ClassNeverInstantiated.Global
public class DizEtoAppSettingsProvider : IDizAppSettings
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string? LastProjectFilename { get; set; }
    public bool OpenLastFileAutomatically { get; set; }
    public string? LastOpenedFile { get; set; }
}