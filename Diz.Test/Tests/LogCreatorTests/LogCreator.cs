﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Test.Utils;
using IX.Observable;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test
{
    public sealed class LogCreatorTests
    {
        private const string ExpectedRaw =
            //          label:       instructions                         ;PC    |rawbytes|ia
            "                        lorom                                ;      |        |      ;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "                        ORG $808000                          ;      |        |      ;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "           CODE_808000: LDA.W Test_Data,X                    ;808000|BD5B80  |80805B;  \r\n" +
            "                        STA.W $0100,X                        ;808003|9D0001  |800100;  \r\n" +
            "           Test22:      DEX                                  ;808006|CA      |      ;  \r\n" +
            "                        BPL CODE_808000                      ;808007|10F7    |808000;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "                        Test_Data = $80805B                  ;      |        |      ;  \r\n";

        readonly Data InputRom = new Data
            {
                Labels = new ObservableDictionary<int, Label>
                {
                    {0x808000 + 0x06, new Label {Name = "Test22"}},
                    {0x808000 + 0x5B, new Label {Name = "Test_Data", Comment = "Pretty cool huh?"}},
                    // the CODE_XXXXXX labels are autogenerated
                },
                RomMapMode = RomMapMode.LoRom,
                RomSpeed = RomSpeed.FastRom,
                RomBytes =
                {
                    // --------------------------
                    // highlighting a particular section here
                    // we will use this for unit tests as well.

                    // CODE_808000: LDA.W Test_Data,X
                    new RomByte {Rom = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                
                    // STA.W $0100,X
                    new RomByte {Rom = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                
                    // DEX
                    new RomByte {Rom = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},

                    // BPL CODE_808000
                    new RomByte {Rom = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                
                    // ------------------------------------
                }
            };
        
        [Fact(Skip = "need to reset the .asm file")]
        public void TestAFewLines()
        {
            LogWriterHelper.AssertAssemblyOutputEquals(ExpectedRaw, LogWriterHelper.ExportAssembly(InputRom, creator =>
            {
                var settings = creator.Settings;
                settings.OutputExtraWhitespace = false;
                creator.Settings = settings;
            }), debugWriter);
        }
        
        [Fact]
        public void TestLabelCount()
        {
            // should give us "Test22" and "Test_Data"
            Assert.Equal(2, InputRom.Labels.Count);
        }
        
        [Fact(Skip = "need to reset the .asm file")]
        public void TestOneLine()
        {
            var exportAssembly = LogWriterHelper.ExportAssembly(new Data());
            LogWriterHelper.AssertAssemblyOutputEquals(ExpectedRaw, exportAssembly);
        }
        
        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/emptyrom.asm")]
        public void TestEmptyRom(string expectedAsm)
        {
            var result = LogWriterHelper.ExportAssembly(new Data());
            LogWriterHelper.AssertAssemblyOutputEquals(expectedAsm, result, debugWriter);
        }
        
        
        private readonly ITestOutputHelper debugWriter;
        public LogCreatorTests(ITestOutputHelper debugWriter)
        {
            this.debugWriter = debugWriter;
        }
    }
}