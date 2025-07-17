using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Diz.Core.Interfaces;

namespace Diz.Core.util;

public class LabelSearchTerms
{
    private readonly string[] mustMatchAllStrings = [];
    private bool filterToRamOnly;
    private readonly List<AddressComparison> addressFilters = [];
    private class AddressComparison
    {
        public enum ComparisonType
        {
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual
        }

        public ComparisonType Type { get; init; }
        public int Address { get; init; }
    }

    public LabelSearchTerms(string searchInput)
    {
        if (string.IsNullOrWhiteSpace(searchInput))
            return;

        var terms = searchInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stringTerms = new List<string>();

        foreach (var term in terms)
        {
            if (TryParseSpecialTerm(term))
                continue;

            // If it's not a special term, add it to the string search terms
            stringTerms.Add(term);
        }

        mustMatchAllStrings = stringTerms.ToArray();
    }

    private bool TryParseSpecialTerm(string term)
    {
        // Handle "is:ram" filter
        if (term.Equals("is:ram", StringComparison.OrdinalIgnoreCase))
        {
            filterToRamOnly = true;
            return true;
        }

        // Handle address comparisons (>, >=, <, <=)
        var addressComparison = TryParseAddressComparison(term);
        if (addressComparison != null)
        {
            addressFilters.Add(addressComparison);
            return true;
        }

        // nothing special about it
        return false;
    }

    private AddressComparison? TryParseAddressComparison(string term)
    {
        if (term.Length < 2)
            return null;

        AddressComparison.ComparisonType comparisonType;
        string addressPart;

        // Check for two-character operators first
        if (term.StartsWith(">="))
        {
            comparisonType = AddressComparison.ComparisonType.GreaterThanOrEqual;
            addressPart = term.Substring(2);
        }
        else if (term.StartsWith("<="))
        {
            comparisonType = AddressComparison.ComparisonType.LessThanOrEqual;
            addressPart = term.Substring(2);
        }
        // Then check single-character operators
        else if (term.StartsWith(">"))
        {
            comparisonType = AddressComparison.ComparisonType.GreaterThan;
            addressPart = term.Substring(1);
        }
        else if (term.StartsWith("<"))
        {
            comparisonType = AddressComparison.ComparisonType.LessThan;
            addressPart = term.Substring(1);
        }
        else
        {
            return null;
        }

        // Try to parse the address part
        var parsedAddress = TryParseHexAddress(addressPart) ?? -1;
        if (parsedAddress == -1)
            return null;
        
        return new AddressComparison
        {
            Type = comparisonType,
            Address = parsedAddress
        };
    }

    private static int? TryParseHexAddress(string addressString)
    {
        if (string.IsNullOrEmpty(addressString))
            return null;

        // Remove $ prefix if present
        if (addressString.StartsWith("$"))
            addressString = addressString.Substring(1);

        // Try to parse as hexadecimal
        if (int.TryParse(addressString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address))
            return address;

        return null;
    }
    
    public bool DoesLabelMatch(int snesAddress, IAnnotationLabel label)
    {
        // check that our label satisfies all of various conditions to count as a match
        
        if (filterToRamOnly)
        {
            // condition: we are required to be a RAM address or, no match
            if (RomUtil.GetWramAddressFromSnesAddress(snesAddress) == -1)
                return false;
        }

        // condition: address in range specified
        if (addressFilters.Select(filter => filter.Type switch
            {
                AddressComparison.ComparisonType.GreaterThan => snesAddress > filter.Address,
                AddressComparison.ComparisonType.GreaterThanOrEqual => snesAddress >= filter.Address,
                AddressComparison.ComparisonType.LessThan => snesAddress < filter.Address,
                AddressComparison.ComparisonType.LessThanOrEqual => snesAddress <= filter.Address,
                _ => true
            }).Any(addressMatches => !addressMatches))
        {
            return false;
        }

        // condition: we must match all required strings
        // ReSharper disable once InvertIf
        if (mustMatchAllStrings.Length > 0)
        {
            var allText = $"{Util.ToHexString6(snesAddress)} {label.Name} {label.Comment}";

            // All string terms must be found somewhere in the combined text
            return mustMatchAllStrings.All(term => allText.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            );
        }

        return true;
    }
}