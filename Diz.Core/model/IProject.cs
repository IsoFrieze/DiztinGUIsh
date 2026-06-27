#nullable enable
using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;

namespace Diz.Core.model;

public interface IProject : 
    INotifyPropertyChanged, 
    IProjectWithSession,
    ISnesCachedVerificationInfo // see if we can get rid of this eventually. only needed for now for serialization
{
    public Data Data { get; }
}