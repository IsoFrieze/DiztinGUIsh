using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Cpu._65816;

// namespace is intentionally NOT under Diz.Ui.* -- inside a `Diz.Ui.Avalonia.*` namespace the
// bare identifier `Avalonia` resolves to the Diz namespace, not the framework (see the gotcha
// note in Diz.Ui.Avalonia/DizAvaloniaApp.cs). The assembly name stays Diz.Ui.Avalonia.Preview,
// which is what the InternalsVisibleTo grant matches.
namespace DizPreview;

/// <summary>
/// A dummy, project-free <see cref="ILabelProvider"/> for the headless preview harness.
///
/// Modeled on what the real WinForms label editor shows against the CT US project: a block
/// of WRAM ($7E14xx-$7E1Bxx) working/animation variables with human names and (some)
/// multi-line comments, a couple carrying an alternate CONTEXT mapping (context "battle" ->
/// a name override, exactly how Diz.Core models per-mode label aliasing -- see
/// <see cref="Label.ContextMappings"/> / <see cref="ContextMapping"/> and IRegion.ContextToApply),
/// plus a few ROM-space ($C0xxxx) code labels for variety.
///
/// Nothing here touches Data, a ROM, or a real project. The provider is a bare
/// <see cref="LabelsServiceWithTemp"/> (the same class Diz.Test's NewProvider(...) helper uses),
/// constructed with a null Data -- valid because the label editor path only ever calls the
/// label CRUD surface, never Data.
/// </summary>
internal static class PreviewFixture
{
    public static LabelsServiceWithTemp Build()
    {
        // null Data: the editor VM never dereferences it (Diz.Test's NewProvider does the same).
        var provider = new LabelsServiceWithTemp(null!);

        foreach (var (addr, label) in Labels())
            provider.AddLabel(addr, label, overwrite: true);

        return provider;
    }

    /// <summary>How many labels the fixture defines (for the harness report / sanity check).</summary>
    public static int Count => Labels().Count;

    /// <summary>Bytes in the throwaway ROM the ROM-dependent scenes are rendered against.</summary>
    public const int PreviewRomSize = 0x1000;

    /// <summary>
    /// A tiny in-memory HiROM for the windows that -- unlike the label editor -- genuinely need
    /// a ROM. The mark-many ViewModel converts addresses, clamps the range to the ROM size, and
    /// reads the data bank / direct page already recorded at the range start; the goto ViewModel
    /// converts between SNES addresses and ROM file offsets and rejects anything outside the
    /// ROM. Byte contents are arbitrary; only the size and map mode matter to either window.
    /// </summary>
    public static ISnesData BuildSnesData()
    {
        var romBytes = new RomBytes();
        for (var i = 0; i < PreviewRomSize; ++i)
            romBytes.Add(new RomByte { Rom = (byte)i });

        var data = new Data
        {
            RomMapMode = RomMapMode.HiRom,
            RomSpeed = RomSpeed.FastRom,
            RomBytes = romBytes,
        };
        data.Apis.AddIfDoesntExist(new SnesApi(data));
        return data.GetSnesApi()!;
    }

    private static List<(int addr, Label label)> Labels()
    {
        var list = new List<(int, Label)>();

        void Add(int addr, string name, string comment = "", params (string ctx, string over)[] contexts)
        {
            var label = new Label { Name = name, Comment = comment };
            foreach (var (ctx, over) in contexts)
                label.ContextMappings.Add(new ContextMapping { Context = ctx, NameOverride = over });
            list.Add((addr, label));
        }

        // ------------------------------------------------------------------ WRAM working vars
        // $7E1400-$7E1B39 range; a mix of plain, multi-line-comment, and context-carrying labels.

        Add(0x7E1400, "location_object_anim_mode",
            "Indicates animation mode.\n3 = static frame\n2 = loops a set number of times\n1 = loops forever\n0 = disabled");
        Add(0x7E1401, "location_object_anim_frame",
            "Current frame index within the active animation.");
        Add(0x7E1402, "location_object_anim_timer",
            "Down-counter until the next frame advance.\nReloaded from anim_speed on wrap.");
        Add(0x7E1403, "location_object_anim_speed");
        Add(0x7E1404, "location_object_facing",
            "Facing direction.\n0 = up\n1 = down\n2 = left\n3 = right");
        Add(0x7E1405, "location_object_x_lo");
        Add(0x7E1406, "location_object_x_hi");
        Add(0x7E1407, "location_object_y_lo");
        Add(0x7E1408, "location_object_y_hi");
        Add(0x7E1409, "location_object_priority",
            "Sprite/BG priority bits copied to OAM during the next VBlank.");

        Add(0x7E1410, "location_object_speed",
            "Movement speed in subpixels/frame.");
        Add(0x7E1412, "location_object_dx",
            "Per-frame X delta (signed 8.8 fixed point).");
        Add(0x7E1414, "location_object_dy",
            "Per-frame Y delta (signed 8.8 fixed point).");
        Add(0x7E1418, "location_object_flags",
            "Bit flags:\nbit7 = visible\nbit6 = collidable\nbit0 = active");

        // scratch RAM reused per game mode -> the alternate-context case
        Add(0x7E1420, "scratch_tbl42",
            "General scratch table.\nAliased per game mode via region ContextToApply.",
            ("battle", "battle_combatant_tbl42"));
        Add(0x7E1422, "scratch_tbl44",
            "General scratch table (second slot).",
            ("battle", "battle_target_tbl44"),
            ("menu", "menu_cursor_tbl44"));

        Add(0x7E1500, "party_member_0_hp_cur");
        Add(0x7E1502, "party_member_0_hp_max");
        Add(0x7E1504, "party_member_0_mp_cur");
        Add(0x7E1506, "party_member_0_mp_max");
        Add(0x7E1508, "party_member_0_level",
            "Current level (1-99). Drives the stat-growth table lookup.");

        Add(0x7E1600, "menu_cursor_index",
            "Highlighted entry in the active menu.");
        Add(0x7E1602, "menu_page",
            "Current page in a multi-page menu.\nWraps at page_count.");
        Add(0x7E1604, "menu_open_flags",
            "Which sub-menus are currently open (bitfield).");

        Add(0x7E1700, "battle_turn_owner",
            "Index of the combatant whose turn is resolving.",
            ("battle", "battle_active_actor"));
        Add(0x7E1702, "battle_atb_tick",
            "ATB accumulator tick for the current combatant.");
        Add(0x7E1710, "battle_damage_accum",
            "Running damage total for the current hit.\nComposed lo/hi across two frames.");

        Add(0x7E1800, "field_scroll_x",
            "Camera scroll X (pixels) for the field map.");
        Add(0x7E1802, "field_scroll_y",
            "Camera scroll Y (pixels) for the field map.");
        Add(0x7E1B39, "misc_frame_counter",
            "Global frame counter; wraps at 0xFFFF.");

        // ------------------------------------------------------------------ ROM-space labels
        Add(0xC00000, "reset",
            "Cold-boot entry point (emulation-mode reset vector target).");
        Add(0xC08000, "main_loop",
            "Top of the main game loop.");
        Add(0xC0A1F0, "load_location_gfx",
            "Copies the active location's tile/sprite gfx to VRAM.");

        return list;
    }
}
