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

    /// <summary>
    /// Example type strings, surfaced in the "expected one of" error. A descriptor that owns a
    /// whole PREFIX family cannot list its members -- the suffix is open -- so it contributes one
    /// representative type instead (e.g. "blob.container" for the "blob." family). That keeps the
    /// message a list of things a user can literally type.
    /// </summary>
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
/// BrrRegionAssetExporter.Validate, TextRegionAssetExporter, BinaryRegionAssetExporter.Validate
/// and ContainerRegionAssetExporter.Parse in Diz.LogWriter.
///
/// EVERY family an exporter claims needs a descriptor here, even one with nothing to check: an
/// asset type no descriptor owns is reported as an unknown type, so a missing descriptor rejects
/// regions the build exports perfectly well.
/// </summary>
public static class RegionAssetTypeValidators
{
    public static IReadOnlyList<RegionAssetTypeValidator> All { get; } =
    [
        BuildGfxAssetValidator(),
        BuildBrrAssetValidator(),
        BuildTextAssetValidator(),
        BuildContainerAssetValidator(),
        BuildRawAssetValidator(),
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

    // Packed containers: the "blob." family, e.g. blob.container. One ROM region holding several
    // assets, usually behind a transform stage such as compression. Nothing in the ROM says what
    // is inside, so the decomposition -- which members, where, how long, what each hashes to --
    // is AUTHORED in Asset Options, and that authoring is what gets checked. Mirrors
    // ContainerRegionAssetExporter.Parse in Diz.LogWriter, minus the parts a row editor cannot
    // decide. Matched by PREFIX, like the exporter's own dispatch: the suffix selects nothing.
    //
    // NO REGION-LENGTH RULE, and that is deliberate. Members are offsets into the UNPACKED
    // buffer, which does not exist until the build has run the transform stage; when a stage such
    // as "lz" is declared, the region's byte count is the COMPRESSED length and bears no
    // arithmetic relationship to the members at all. So the tiling is checked here only RELATIVE
    // to the members themselves -- ascending, no hole, no overlap between neighbours -- and
    // whether those tiles add up to the whole buffer is checked at build time against the
    // buffer's real length. That is the half of the tiling check that cannot be done here, and
    // inventing a length rule in its place would reject every compressed container there is.
    //
    // Also deliberately unchecked: the transform-stage option key ("lz") and the member "type"
    // strings. Both are vocabularies owned by registries that live with the exporters, which this
    // layer cannot see; a hand-copied list of either would go stale and start rejecting a
    // legitimately registered stage or codec. The exporter names both precisely when the build
    // runs, and a check missed here merely halts a build, while a false one blocks editing.
    private static RegionAssetTypeValidator BuildContainerAssetValidator()
    {
        const string containerPrefix = "blob.";
        const string membersKey = "members";

        return new RegionAssetTypeValidator
        {
            Matches = t => t?.StartsWith(containerPrefix, StringComparison.Ordinal) == true,
            ExampleTypes = ["blob.container"],
            Validate = ctx =>
            {
                if (ctx.Options == null)
                    return "Container assets require Asset Options: a container is defined by the " +
                           "members tiling its unpacked buffer, and nothing in the ROM says what " +
                           "they are, e.g. {\"members\": [{\"name\": \"blob/thing\", \"at\": 0, " +
                           "\"len\": N, \"type\": \"raw.bin\", \"sha256\": \"<64 hex digits>\"}]}.";

                if (!ctx.Options.TryGetPropertyValue(membersKey, out var membersNode)
                    || membersNode is not JsonArray members)
                    return $"Asset Options: \"{membersKey}\" must be an array. That array IS the " +
                           "container -- without it there is nothing to unpack into.";

                if (members.Count == 0)
                    return $"Asset Options: \"{membersKey}\" is empty. Every byte of the buffer " +
                           "belongs to some member, so a container has at least one -- a buffer " +
                           "nobody has decomposed yet is ONE verbatim member covering all of it.";

                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                var cursor = 0;

                for (var i = 0; i < members.Count; ++i)
                {
                    var where = $"Asset Options: {membersKey}[{i}]";

                    if (members[i] is not JsonObject member)
                        return $"{where} must be an object with \"name\", \"at\", \"len\", " +
                               "\"type\" and \"sha256\".";

                    if (!TryGetNonEmptyStringOption(member, "name", out var name))
                        return $"{where}: \"name\" must be a non-empty string.";

                    // the exporter normalizes before comparing, so two spellings of one path
                    // collide here exactly as they would when the buffer is split into files.
                    name = name.Replace('\\', '/').Trim('/');
                    if (!seenNames.Add(name))
                        return $"{where}: member name '{name}' is declared twice. Names are file " +
                               "paths -- two members sharing one would overwrite each other.";

                    if (!TryGetIntOption(member, "at", out var at) || at < 0)
                        return $"{where}: \"at\" must be an integer >= 0 (a byte offset into the " +
                               "container's unpacked buffer).";

                    if (!TryGetIntOption(member, "len", out var length) || length < 1)
                        return $"{where}: \"len\" must be an integer >= 1. A zero-length member " +
                               "claims no bytes and cannot be told apart from a typo; drop it.";

                    if (at != cursor)
                    {
                        var problem = at > cursor
                            ? $"leaves a HOLE at bytes 0x{cursor:X}..0x{at:X} ({at - cursor} unclaimed)"
                            : $"OVERLAPS the member before it at bytes 0x{at:X}..0x{cursor:X} " +
                              $"({cursor - at} bytes claimed twice)";
                        return $"{where} ('{name}') {problem}. Members must tile the buffer in " +
                               "ascending order with no hole and no overlap -- declaration order " +
                               "is data, because reassembly concatenates in it -- so declare any " +
                               "gap as an explicit verbatim member rather than losing those bytes.";
                    }

                    cursor = at + length;

                    if (!TryGetNonEmptyStringOption(member, "type", out _))
                        return $"{where}: \"type\" must be a non-empty asset type string -- a " +
                               "member is an ordinary asset and needs a codec contract.";

                    if (!TryGetNonEmptyStringOption(member, "sha256", out var sha256)
                        || !IsSha256Hex(sha256))
                        return $"{where}: \"sha256\" must be 64 lowercase hex digits -- the " +
                               "sha256 of this member's bytes as they appear in the unpacked " +
                               "buffer. The build checks it, and it is the only thing that can " +
                               "catch a decomposition that is plausible but wrong.";
                }

                return null;
            },
        };
    }

    // Verbatim bytes: the "raw." family, e.g. raw.bin -- the type a plain-binary region is
    // exported as, and the type a container member carries when nothing decodes it further.
    // There is no structure to check, because any byte is a valid byte; the only thing
    // BinaryRegionAssetExporter.Validate in Diz.LogWriter refuses is an empty region, whose
    // manifest would describe a zero-length slice of the ROM. (Row-level bounds checks already
    // make that unreachable from here -- the end address is inclusive, so the shortest legal
    // region is one byte -- but the rule is stated anyway so the two copies read the same.)
    // Matched by PREFIX, mirroring the exporter's dispatch: the bytes are copied through
    // unchanged whatever the suffix says, so unlike the exactly-matched gfx/BRR/text types there
    // is no near-miss that would reach the build with no codec to handle it.
    private static RegionAssetTypeValidator BuildRawAssetValidator()
    {
        const string rawPrefix = "raw.";

        return new RegionAssetTypeValidator
        {
            Matches = t => t?.StartsWith(rawPrefix, StringComparison.Ordinal) == true,
            ExampleTypes = ["raw.bin"],
            Validate = ctx => ctx.RegionLength <= 0
                ? $"Region is empty; a binary asset needs at least one byte when Asset Type is " +
                  $"'{ctx.AssetType}'."
                : null,
        };
    }

    // 64 lowercase hex digits. Mirrors ContainerRegionAssetExporter.RequiredSha256.
    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

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
