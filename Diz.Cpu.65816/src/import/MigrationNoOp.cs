using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

// A migration for savedata format that "does nothing"
// used when you don't actually need to change anything, but you do need to pull in a step that upgrades from a previous version
// i.e. if v100 didn't save WidgetFooBars, and v101 optionally has WidgetFooBars in it,
// then, we need something that bumps the version# from 100 to 101 (i.e. this class), but,
// v100 will never have any WidgetFooBars, so we never need to look for them.  we simply just bump the ver#
[UsedImplicitly]
public sealed class MigrationNoOp : IMigration
{
    // you MUST set this when instantiating though
    public int AppliesToSaveVersion { get; init; } = -1;

    public void OnLoadingBeforeAddLinkedRom(IAddRomDataCommand romAddCmd) {}

    public void OnLoadingAfterAddLinkedRom(IAddRomDataCommand romAddCmd) {}
}