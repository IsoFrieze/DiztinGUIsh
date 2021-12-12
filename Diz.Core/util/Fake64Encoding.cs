using System.Collections.Generic;

namespace Diz.Core.util
{
    // this is base64 EXCEPT:
    // 1) char "A" and "0" swapped (my data format compresses things with "0" better)
    // 2) only supports 6 bits of input data to encode on ONE BYTE.
    //
    // there's better ways to do this but I'm going for blinding speed for multi-threaded access.
    // just, hardcode it.  there are system Base64 functions we could use but, they're also way too slow.
    //
    // This is ... kind of a silly implementation... but, it's fast and works well with our compression so...
    // if you don't like it that's just like... your opinion and stuff. man.♦
    //
    // [don't judge me] -Dom
    public static class Fake64Encoding
    {
        public static byte DecodeHackyBase64(char input)
        {
            return Fake64CharToByte[input];
        }

        public static char EncodeHackyBase64(byte input)
        {
            return Fake64ByteToChar[input];
        }
        
        // char to byte
        private static readonly Dictionary<char, byte> Fake64CharToByte = new()
        {
            {'A', 208},
            {'B', 4},
            {'C', 8},
            {'D', 12},
            {'E', 16},
            {'F', 20},
            {'G', 24},
            {'H', 28},
            {'I', 32},
            {'J', 36},
            {'K', 40},
            {'L', 44},
            {'M', 48},
            {'N', 52},
            {'O', 56},
            {'P', 60},
            {'Q', 64},
            {'R', 68},
            {'S', 72},
            {'T', 76},
            {'U', 80},
            {'V', 84},
            {'W', 88},
            {'X', 92},
            {'Y', 96},
            {'Z', 100},
            {'a', 104},
            {'b', 108},
            {'c', 112},
            {'d', 116},
            {'e', 120},
            {'f', 124},
            {'g', 128},
            {'h', 132},
            {'i', 136},
            {'j', 140},
            {'k', 144},
            {'l', 148},
            {'m', 152},
            {'n', 156},
            {'o', 160},
            {'p', 164},
            {'q', 168},
            {'r', 172},
            {'s', 176},
            {'t', 180},
            {'u', 184},
            {'v', 188},
            {'w', 192},
            {'x', 196},
            {'y', 200},
            {'z', 204},
            {'0', 0},
            {'1', 212},
            {'2', 216},
            {'3', 220},
            {'4', 224},
            {'5', 228},
            {'6', 232},
            {'7', 236},
            {'8', 240},
            {'9', 244},
            {'+', 248},
            {'/', 252},
        };

        private static readonly Dictionary<byte, char> Fake64ByteToChar = new()
        {
            {208, 'A'},
            {4, 'B'},
            {8, 'C'},
            {12, 'D'},
            {16, 'E'},
            {20, 'F'},
            {24, 'G'},
            {28, 'H'},
            {32, 'I'},
            {36, 'J'},
            {40, 'K'},
            {44, 'L'},
            {48, 'M'},
            {52, 'N'},
            {56, 'O'},
            {60, 'P'},
            {64, 'Q'},
            {68, 'R'},
            {72, 'S'},
            {76, 'T'},
            {80, 'U'},
            {84, 'V'},
            {88, 'W'},
            {92, 'X'},
            {96, 'Y'},
            {100, 'Z'},
            {104, 'a'},
            {108, 'b'},
            {112, 'c'},
            {116, 'd'},
            {120, 'e'},
            {124, 'f'},
            {128, 'g'},
            {132, 'h'},
            {136, 'i'},
            {140, 'j'},
            {144, 'k'},
            {148, 'l'},
            {152, 'm'},
            {156, 'n'},
            {160, 'o'},
            {164, 'p'},
            {168, 'q'},
            {172, 'r'},
            {176, 's'},
            {180, 't'},
            {184, 'u'},
            {188, 'v'},
            {192, 'w'},
            {196, 'x'},
            {200, 'y'},
            {204, 'z'},
            {0, '0'},
            {212, '1'},
            {216, '2'},
            {220, '3'},
            {224, '4'},
            {228, '5'},
            {232, '6'},
            {236, '7'},
            {240, '8'},
            {244, '9'},
            {248, '+'},
            {252, '/'},
        };
    }
}
