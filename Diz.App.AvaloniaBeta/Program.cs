#nullable enable

using System;
using Diz.App.Winforms;

namespace Diz.App.AvaloniaBeta;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args) =>
        Diz.App.Winforms.Program.Run(args, LabelEditorBackendKind.Avalonia);
}
