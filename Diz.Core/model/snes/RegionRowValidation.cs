using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Core.model.snes;

/// <summary>
/// The values of one region as a plain snapshot, so a rule set can be run against a PROPOSED
/// edit without writing it to the model first. Only fields a rule could reach are carried;
/// everything else on IRegion (Priority, ContextToApply, AssetVersion) has no rule.
///
/// ExportSeparateFile is carried even though no PER-ROW rule reads it: whether a region emits
/// its own file is decided against the OTHER regions (the file-producing regions have to nest,
/// never partially overlap), which is a whole-collection question. Keeping it in the snapshot
/// means a row-level caller does not have to know that, and a future row rule that does need it
/// has it to hand.
/// </summary>
public readonly record struct RegionRowValues(
    string RegionName,
    int StartSnesAddress,
    int EndSnesAddress,
    bool ExportSeparateFile,
    RegionExportType ExportType,
    string AssetType,
    string AssetName,
    string AssetOptions)
{
    public static RegionRowValues From(IRegion region) =>
        new(
            region.RegionName,
            region.StartSnesAddress,
            region.EndSnesAddress,
            region.ExportSeparateFile,
            region.ExportType,
            region.AssetType,
            region.AssetName,
            region.AssetOptions);

    /// <summary>
    /// Byte count the exporter will extract for this region. EndSnesAddress is INCLUSIVE
    /// everywhere in the codebase (the last byte IN the region), so it is end - start + 1.
    /// </summary>
    public int RegionLength => EndSnesAddress - StartSnesAddress + 1;
}

/// <summary>
/// Per-region rules: everything that can be decided by looking at ONE region in isolation.
/// The companion whole-collection rules (laminar file-producing family, non-overlapping asset
/// regions, role exclusivity) live in <see cref="RegionValidation"/> in this same folder.
///
/// Every problem is returned as a value; nothing throws and nothing is presented to a user
/// here, so the same rules can be run from any UI backend or from a batch/headless check.
///
/// ZERO-LENGTH: a region whose start equals its end is a legal ONE-BYTE region, because the
/// end address is inclusive -- the byte count is end - start + 1, so start == end means 1.
/// Only start &gt; end is rejected. (An earlier grid-side copy of these rules refused
/// start == end as "zero-length", which contradicted its own Length column: typing a length of
/// 1 produced exactly that row and then refused to let the user leave it.)
/// </summary>
public static class RegionRowValidation
{
    public const int MaxSnesAddress = 0xFFFFFF;

    /// <summary>
    /// Run every per-region rule, in the historical order, and return the FIRST failure --
    /// one message at a time is what a row-level editor can usefully show.
    /// </summary>
    public static ValidationResult ValidateRow(RegionRowValues values)
    {
        if (string.IsNullOrWhiteSpace(values.RegionName))
            return ValidationResult.Fail("Region Name is required.");

        // see the zero-length note on this class: equality is a 1-byte region, not an error.
        if (values.StartSnesAddress > values.EndSnesAddress)
            return ValidationResult.Fail("Start address must not be greater than end address.");

        if (values.StartSnesAddress < 0 || values.EndSnesAddress < 0)
            return ValidationResult.Fail("Negative numbers not allowed in SNES addresses");

        if (values.StartSnesAddress > MaxSnesAddress || values.EndSnesAddress > MaxSnesAddress)
            return ValidationResult.Fail("SNES address too large (max allowed: 24-bits: 0xFFFFFF)");

        var assetNameResult = ValidateAssetName(values.AssetName);
        if (!assetNameResult.IsValid)
            return assetNameResult;

        return values.ExportType == RegionExportType.Asset
            ? ValidateAssetFields(values)
            : ValidationResult.Ok;
    }

    /// <summary>
    /// The asset name is used as a relative path under the asset root, so it must not escape it.
    /// Empty is fine: the exporter falls back to RegionName.
    /// </summary>
    public static ValidationResult ValidateAssetName(string assetName)
    {
        var name = assetName ?? "";
        if (string.IsNullOrWhiteSpace(name))
            return ValidationResult.Ok;

        return name.Contains('\\') || name.Contains("..") || name.StartsWith('/')
            ? ValidationResult.Fail(
                "Asset Name must be a relative path: no backslashes, no '..', and no leading '/'.")
            : ValidationResult.Ok;
    }

    /// <summary>
    /// Rules that only apply when the region is exported as a typed asset: the asset type must
    /// be one a registered descriptor owns, the free-form options must parse as a JSON object,
    /// and the descriptor's own length rule must pass.
    /// </summary>
    public static ValidationResult ValidateAssetFields(RegionRowValues values)
    {
        var assetType = values.AssetType ?? "";

        // Route to the descriptor that owns this AssetType family, the same way the codec
        // dispatch does downstream when the bytes are actually written out.
        var descriptor = RegionAssetTypeValidators.All.FirstOrDefault(d => d.Matches(assetType));
        if (descriptor == null)
        {
            var known = string.Join(", ", RegionAssetTypeValidators.All.SelectMany(d => d.ExampleTypes));
            return ValidationResult.Fail(
                $"Asset Type is required when Export Type is 'Asset'. Expected one of: {known}.");
        }

        if (!TryParseAssetOptions(values.AssetOptions, out var options, out var optionsError))
            return ValidationResult.Fail(optionsError);

        var context = new RegionAssetValidationContext(assetType, values.RegionLength, options);
        var error = descriptor.Validate(context);

        return error != null ? ValidationResult.Fail(error) : ValidationResult.Ok;
    }

    /// <summary>
    /// Asset Options is free-form and Diz does not own its vocabulary, so at this generic layer
    /// only "it parses as a JSON object" is checked; a descriptor reads whatever type-specific
    /// keys (e.g. cell_h) it needs. Blank is normal and yields a null object.
    /// </summary>
    public static bool TryParseAssetOptions(string optionsText, out JsonObject options, out string error)
    {
        options = null;
        error = null;

        if (string.IsNullOrWhiteSpace(optionsText))
            return true;

        JsonNode parsed;
        try
        {
            parsed = JsonNode.Parse(optionsText);
        }
        catch (JsonException ex)
        {
            error = $"Asset Options is not valid JSON: {ex.Message}";
            return false;
        }

        if (parsed is not JsonObject optionsObj)
        {
            error = "Asset Options must be a JSON object, e.g. {\"cell_h\": 12}.";
            return false;
        }

        options = optionsObj;
        return true;
    }
}

/// <summary>
/// What a per-asset-type rule gets to look at.
/// </summary>
/// <param name="AssetType">the region's asset type string, e.g. "gfx.snes.4bpp".</param>
/// <param name="RegionLength">
/// Inclusive byte count (end - start + 1) -- the number of bytes the exporter will actually
/// extract for this region.
/// </param>
/// <param name="Options">parsed Asset Options, or null when the field was blank.</param>
public sealed record RegionAssetValidationContext(string AssetType, int RegionLength, JsonObject Options);

/// <summary>
/// One descriptor owns a family of AssetType strings and knows how to sanity-check a region's
/// length + options for that family. Adding a new asset type means registering another
/// descriptor rather than editing the row rules.
/// </summary>
public sealed class RegionAssetTypeValidator
{
    /// <summary>Does this descriptor own the given AssetType string?</summary>
    public Func<string, bool> Matches { get; init; } = _ => false;

    /// <summary>Example type strings, surfaced in the "expected one of" error.</summary>
    public IReadOnlyList<string> ExampleTypes { get; init; } = [];

    /// <summary>Returns null when valid, else a user-facing error message.</summary>
    public Func<RegionAssetValidationContext, string> Validate { get; init; } = _ => null;
}

/// <summary>
/// The registered asset-type descriptors.
///
/// These rules are a DELIBERATE SECOND COPY of the ones the asset exporters enforce when the
/// bytes are written out. They are kept in sync by hand so an editor cannot accept a region the
/// build will reject, or reject one the build accepts. If a rule changes on one side, change it
/// on both. The counterparts are RegionAssetUtil.ParseSnesGfxBpp,
/// BrrRegionAssetExporter.Validate and TextRegionAssetExporter in Diz.LogWriter.
/// </summary>
public static class RegionAssetTypeValidators
{
    public static IReadOnlyList<RegionAssetTypeValidator> All { get; } =
    [
        BuildGfxAssetValidator(),
        BuildBrrAssetValidator(),
        BuildTextAssetValidator(),
    ];

    // SNES BRR audio: audio.snes.brr. The stream is 9-byte ADPCM blocks (1 header + 8 data),
    // so its length must be a whole multiple of 9. Mirrors BrrRegionAssetExporter.Validate in
    // Diz.LogWriter. NOTE: the region must cover ONLY the BRR
    // stream; if the sample has a length/header prefix before the stream, that prefix stays in
    // the parent region's assembly.
    private static RegionAssetTypeValidator BuildBrrAssetValidator()
    {
        const string brrType = "audio.snes.brr";
        const int brrBlock = 9;

        return new RegionAssetTypeValidator
        {
            Matches = t => string.Equals(t, brrType, StringComparison.Ordinal),
            ExampleTypes = [brrType],
            Validate = ctx =>
            {
                if (ctx.RegionLength <= 0 || ctx.RegionLength % brrBlock != 0)
                {
                    return $"Region length ({ctx.RegionLength} bytes) must be a whole multiple of " +
                           $"{brrBlock} bytes (one BRR ADPCM block) when Asset Type is '{brrType}'. " +
                           "The region must cover ONLY the BRR stream -- if the sample has a " +
                           "length/header prefix before the stream, exclude it.";
                }

                return null;
            },
        };
    }

    // SNES graphics: gfx.snes.{2,4,8}bpp. bpp/2 bitplane pairs, 2 bytes per row per pair,
    // cell_h rows; a partial cell at the end would silently produce garbage graphics, so reject.
    // Kept in sync (by shape) with RegionAssetUtil.ParseSnesGfxBpp in Diz.LogWriter.
    private static RegionAssetTypeValidator BuildGfxAssetValidator()
    {
        var validBpp = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "gfx.snes.2bpp", 2 },
            { "gfx.snes.4bpp", 4 },
            { "gfx.snes.8bpp", 8 },
        };

        return new RegionAssetTypeValidator
        {
            Matches = validBpp.ContainsKey,
            ExampleTypes = validBpp.Keys.ToList(),
            Validate = ctx =>
            {
                var bpp = validBpp[ctx.AssetType];

                var cellHeight = 8;
                if (ctx.Options != null
                    && ctx.Options.TryGetPropertyValue("cell_h", out var cellHeightNode)
                    && cellHeightNode != null)
                {
                    if (cellHeightNode.GetValueKind() != JsonValueKind.Number
                        || !cellHeightNode.AsValue().TryGetValue(out cellHeight)
                        || cellHeight < 1)
                    {
                        return "Asset Options: cell_h must be an integer >= 1.";
                    }
                }

                var cellSizeInBytes = bpp * cellHeight;
                if (ctx.RegionLength <= 0 || ctx.RegionLength % cellSizeInBytes != 0)
                {
                    var what = cellHeight == 8 ? $"one {bpp}bpp tile" : $"one {bpp}bpp 8x{cellHeight} cell";
                    return $"Region length ({ctx.RegionLength} bytes) must be a whole multiple of " +
                           $"{cellSizeInBytes} bytes ({what}) when Asset Type is '{ctx.AssetType}'.";
                }

                return null;
            },
        };
    }

    // Fixed-width name tables: text.ct.mapped. Mirrors TextRegionAssetExporter in Diz.LogWriter:
    // text assets REQUIRE options (tbl/record_width/pad have no defaults Diz could invent), and the
    // region must be a whole number of record_width-byte records -- the records carry no terminator,
    // so a ragged tail mis-frames every later record. Matched EXACTLY (like the gfx/brr validators),
    // not by "text." prefix: a near-miss such as "text.ct.mapped2" has no codec downstream and must
    // be rejected here, not accepted and then failed at build.
    private static RegionAssetTypeValidator BuildTextAssetValidator()
    {
        const string mappedType = "text.ct.mapped";
        return new RegionAssetTypeValidator
        {
            Matches = t => string.Equals(t, mappedType, StringComparison.Ordinal),
            ExampleTypes = [mappedType],
            Validate = ctx =>
            {
                if (ctx.Options == null)
                    return "Text assets require Asset Options, e.g. " +
                           "{\"tbl\": \"text/<table>.tbl\", \"record_width\": N, \"pad\": \"0xNN\"} " +
                           "(plus an optional \"tokens\" map).";

                if (!TryGetIntOption(ctx.Options, "record_width", out var recordWidth) || recordWidth < 1)
                    return "Asset Options: \"record_width\" must be an integer >= 1.";

                if (!TryGetNonEmptyStringOption(ctx.Options, "tbl", out _))
                    return "Asset Options: \"tbl\" must be a non-empty string path to the .tbl font map.";

                if (!TryGetNonEmptyStringOption(ctx.Options, "pad", out var pad) || !TryParseByteLiteral(pad, out _))
                    return "Asset Options: \"pad\" must be a byte literal like \"0xEF\" (0..255).";

                if (ctx.RegionLength <= 0 || ctx.RegionLength % recordWidth != 0)
                    return $"Region length ({ctx.RegionLength} bytes) must be a whole multiple of " +
                           $"record_width ({recordWidth}) when Asset Type is '{ctx.AssetType}'. " +
                           "Fixed-width records have no terminator, so a ragged tail mis-frames " +
                           "every later record -- adjust the bounds or record_width.";

                return null;
            },
        };
    }

    private static bool TryGetIntOption(JsonObject options, string key, out int value)
    {
        value = 0;
        return options.TryGetPropertyValue(key, out var node) && node != null
            && node.GetValueKind() == JsonValueKind.Number
            && node.AsValue().TryGetValue(out value);
    }

    private static bool TryGetNonEmptyStringOption(JsonObject options, string key, out string value)
    {
        value = "";
        if (!options.TryGetPropertyValue(key, out var node) || node == null
            || node.GetValueKind() != JsonValueKind.String)
            return false;
        value = node.GetValue<string>();
        return !string.IsNullOrWhiteSpace(value);
    }

    // A byte literal like "0xEF" or "239" (0..255). Mirrors TextRegionAssetExporter.TryParseByteLiteral.
    private static bool TryParseByteLiteral(string s, out int value)
    {
        s = s.Trim();
        var ok = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        return ok && value is >= 0 and <= 0xFF;
    }
}
