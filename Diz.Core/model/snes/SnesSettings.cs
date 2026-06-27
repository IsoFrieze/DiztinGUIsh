#nullable enable

using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Core.model.snes;

public class SnesSettings : INotifyPropertyChanged
{
    // NOTE: snes specific stuff (rom map mode/speed) should eventually be removed from here.
    // this class should be a generic base class for all systems (snes, nes, sega, whatever).
    // for now we're in transition.
    // .. also, same thing with log generation stuff.

    // don't modify these directly, always go through the public properties so
    // other objects can subscribe to modification notifications
    private RomMapMode romMapMode;
    private RomSpeed romSpeed = RomSpeed.Unknown;

    // Note: order of these public properties matters for the load/save process. Keep 'RomBytes' LAST
    // TODO: should be a way in the XML serializer to control the order, remove this comment
    // when we figure it out.
    public RomMapMode RomMapMode
    {
        get => romMapMode;
        set => this.SetField(PropertyChanged, ref romMapMode, value);
    }

    public RomSpeed RomSpeed
    {
        get => romSpeed;
        set => this.SetField(PropertyChanged, ref romSpeed, value);
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
}