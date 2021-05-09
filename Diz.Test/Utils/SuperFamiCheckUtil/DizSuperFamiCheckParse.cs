using System;
using Sprache;

namespace Diz.Test.Utils.SuperFamiCheckUtil
{
    public static class DizSuperFamiCheckParse
    {
        public static readonly Parser<(string key, uint value)> Kvp =
            from leading in Parse.WhiteSpace.Many()
            from key in Identifier.Token()
            from value in HexNumber.Token()
            select (key, value);

        public static readonly Parser<string> Identifier =
            from leading in Parse.WhiteSpace.Many()
            from id in Parse.Letter.Many().Text()
            from trailing in Parse.WhiteSpace.Many()
            select id;

        public static readonly Parser<uint> HexNumber =
            from prefix in Parse.String("0x")
                .Once().Optional()
            from numericString in Parse.Digit
                .Or(Parse.Chars("abcdefABCDEF"))
                .Repeat(1, 8).Text()
            select Convert.ToUInt32(numericString, 16);

        public static (string key, uint value) ParseKvpLine(string input) =>
            Kvp.End().Parse(input);
    }
}