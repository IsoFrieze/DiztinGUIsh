using System;
using System.IO;
using System.Linq;
using Diz.Core.arch;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using Xunit;

namespace Diz.Test.tests
{
    public sealed class RomByteTests
    {
        // old-style
        private static ByteEntry SampleRomByte1()
        {
            return new() {
                Arch = Architecture.Apuspc700,
                DataBank = 90,
                DirectPage = 3,
                MFlag = true,
                XFlag = false,
                TypeFlag = FlagType.Graphics,
                Point = InOutPoint.InPoint | InOutPoint.ReadPoint,
                Byte = 0x78,
            };
        }
        
        // new-style
        private static ByteEntry SampleRomByte3()
        {
            return new(new AnnotationCollection
            {
                new BranchAnnotation
                {
                    Point = InOutPoint.InPoint | InOutPoint.ReadPoint
                },
                new MarkAnnotation
                {
                    TypeFlag = FlagType.Graphics,
                },
                new OpcodeAnnotation
                {
                    Arch = Architecture.Apuspc700,
                    DataBank = 90,
                    DirectPage = 3,
                    MFlag = true,
                    XFlag = false,
                }
            })
            {
                Byte = 0x78,
            };
        }

        [Fact]
        public void OldVsNewStyle()
        {
            var rb1 = SampleRomByte1();
            var rb3 = SampleRomByte3();

            Assert.Equal(rb1.GetOneAnnotation<MarkAnnotation>(), rb3.GetOneAnnotation<MarkAnnotation>());
            Assert.Equal(rb1.GetOneAnnotation<OpcodeAnnotation>(), rb3.GetOneAnnotation<OpcodeAnnotation>());
            Assert.Equal(rb1.GetOneAnnotation<BranchAnnotation>(), rb3.GetOneAnnotation<BranchAnnotation>());
        }
        
        [Fact]
        public void RB_1vs3()
        {
            var rb1 = SampleRomByte1();
            var rb3 = SampleRomByte3();
            
            Assert.Equal(rb1, rb3);

            rb1.Arch = Architecture.Cpu65C816;
            Assert.NotEqual(rb1, rb3);

            rb3.Arch = rb1.Arch;
            Assert.Equal(rb1, rb3);
        }

        [Fact]
        public void UnorderedListEquality1()
        {
            var rb1 = SampleRomByte1();
            var rb3 = SampleRomByte3();

            // create lists and tell it not to enforce no-duplicate annotation policy
            rb1.GetOrCreateAnnotationsList().EnforcePolicy = false;
            rb3.GetOrCreateAnnotationsList().EnforcePolicy = false;

            rb1.Annotations.Add(new Comment {Text="asdf2"});
            rb1.Annotations.Add(new Comment {Text="asdf1"});
            
            rb1.Annotations.Add(new Label {Name="asdf"});
            Assert.NotEqual(rb1, rb3);
            
            rb3.Annotations.Add(new Comment {Text="asdf1"}); // note: order reversed
            rb3.Annotations.Add(new Comment {Text="asdf2"}); // note: order reversed
            
            rb3.Annotations.Add(new Label {Name="asdf"});
            
            Assert.Equal(rb1, rb3); // even with reverse order, this needs to be ok
        }
        
        
        [Fact]
        public void AnnotationAccess1()
        {
            var rb3 = SampleRomByte3();
            Assert.Null(rb3.GetOneAnnotation<Comment>());
            
            rb3.Annotations.Add(new Comment {Text="1"});
            Assert.NotNull(rb3.GetOneAnnotation<Comment>());
            
            Assert.Throws<InvalidDataException>(()=>rb3.Annotations.Add(new Comment {Text="2"}));
        }

        [Fact]
        public void TestMoreParentFixing()
        {
            var label1 = new Label {Name = "test11", Comment = "asdf"};
            var mark1 = new MarkAnnotation {TypeFlag = FlagType.Graphics};

            Assert.Null(mark1.Parent);
            Assert.Null(label1.Parent);
            var unused = new AnnotationCollection {label1, mark1};
            
            Assert.Null(mark1.Parent);
            Assert.Null(label1.Parent);
        }

        [Fact]
        public void TestParentFixing()
        {
            var label1 = new Label {Name = "test11", Comment = "asdf"};
            var mark1 = new MarkAnnotation {TypeFlag = FlagType.Graphics};
            
            var ac = new AnnotationCollection {label1, mark1};
            var b1 = new ByteEntry();
            Assert.Throws<InvalidOperationException>(() => b1.ReplaceAnnotationsWith(ac));

            var b2 = new ByteEntry {DontSetParentOnCollectionItems = true};
            ac.Parent = b2;
            b2.ReplaceAnnotationsWith(ac);
            Assert.Equal(b2, mark1.Parent);
            Assert.Equal(b2, label1.Parent);

            b2.ReplaceAnnotationsWith(null);
            Assert.Equal(b2, mark1.Parent);
            Assert.Equal(b2, label1.Parent);
            
            ac.Clear();
            Assert.Null(mark1.Parent);
            Assert.Null(label1.Parent);
        }
        
        [Fact]
        public void TestParentFixing3()
        {
            var label1 = new Label {Name = "test11", Comment = "asdf"};
            var mark1 = new MarkAnnotation {TypeFlag = FlagType.Graphics};

            var b2 = new ByteEntry(new AnnotationCollection {label1, mark1});
            Assert.Equal(b2, mark1.Parent);
            Assert.Equal(b2, label1.Parent);

            Assert.NotNull(b2.Annotations);
            b2.Annotations.Clear();
            Assert.Null(mark1.Parent);
            Assert.Null(label1.Parent);
        }

        [Fact]
        public void TestNoAnnotation()
        {
            static void TestNullAnnotations(ByteEntry entry)
            {
                #if EXPECTING_NULL_ANNOTATIONS
                Assert.Null(entry.Annotations);
                #endif    
            }
            
            var entry = new ByteEntry();
            TestNullAnnotations(entry);
            var nothing = entry.GetOneAnnotation<Comment>();
            Assert.Null(nothing);
            TestNullAnnotations(entry); // looking up something that doesn't exist should NOT auto-recreate this. save memory.
            Assert.Throws<InvalidOperationException>(() => entry.AppendAnnotationsFrom(null));
            Assert.Throws<InvalidOperationException>(() => entry.ReplaceAnnotationsWith(null));
            TestNullAnnotations(entry);
        }

        [Fact]
        public void TestVariousPolicyViolations()
        {
            var entry = new ByteEntry {DataBank = 0x55};
            Assert.Single(entry.Annotations);
            Assert.True(entry.Annotations[0].GetType() == typeof(OpcodeAnnotation));

            // replacing the only annotation should always work.
            entry.Annotations[0] = new Comment();
            entry.Annotations[0] = new Comment();
            entry.Annotations[0] = new OpcodeAnnotation();
            
            // adding a different type than the last one, should work.
            entry.Annotations.Add(new Comment());
            
            // replacing the first one with the same type as [1] should violate policy and throw an error
            Assert.Throws<InvalidDataException>(() => entry.Annotations[0] = new Comment());
            Assert.Equal(2, entry.Annotations.Count);

            // replacing the first one with an entry of a different type should be ok.
            entry.Annotations[0] = new Label();
            
            // adding a third entry of different type should be ok.
            entry.Annotations.Add(new MarkAnnotation());
            Assert.Equal(3, entry.Annotations.Count);
        }

        [Fact]
        public void TestByteAnnotations()
        {
            var entry = new ByteEntry {Byte = 0x55};
            Assert.Single(entry.Annotations);
            Assert.True(entry.Annotations[0].GetType() == typeof(ByteAnnotation));
            Assert.Throws<InvalidDataException>(() => entry.Annotations.Add(new ByteAnnotation()));
            Assert.Single(entry.Annotations);
        }
        
        
        [Fact]
        public void TestAddNullAnnotationFails()
        {
            var entry = new ByteEntry {Byte = 0xFF};
            Assert.Throws<InvalidDataException>(() => entry.Annotations.Add(null));
        }

        [Fact]
        public static void TestGetAddressMode()
        {
            var sampleData = SampleRomData.SampleData;
            const int romOffset1 = 0xEB;
            var mode1 = Cpu65C816.GetAddressMode(sampleData, romOffset1);
            Assert.Equal(Cpu65C816.AddressMode.Constant8, mode1);
            
            var mode2 = Cpu65C816.GetAddressMode(sampleData, 0x0A);
            Assert.Equal(Cpu65C816.AddressMode.Constant8, mode2);
        }

        [Fact]
        public static void TestGetFlatByteNonPadded()
        {
            // Get a byte from the sample data that is a real (i.e. non-padded) byte
            
            var sampleData = SampleRomData.SampleData;
            const int romOffset = 0x0A;
            const int snesAddress = 0x808000 + romOffset;
            
            var flatByte = ByteGraphUtil.BuildFlatDataFrom(sampleData.SnesAddressSpace, snesAddress);
            Assert.NotNull(flatByte);
            Assert.NotNull(flatByte.Byte);
            Assert.Equal(0xC2, flatByte.Byte.Value);
        }
        
        [Fact]
        public static void TestGetFlatByteInRange()
        {
            // Get a byte from the sample data that is a padded (i.e. for sample ROMs we can create them with a different
            // size than their source data. in this one, we pad the ROM from a few hundred bytes and add zero'd bytes
            // until we reach 32k bytes). This test is mostly testing that we built the sample data correctly, in real
            // world scenarios, this would never fail because we're not doing padding.
            
            var sampleData = SampleRomData.SampleData;
            const int romOffset = 0xEB;
            const int snesAddress = 0x808000 + romOffset;

            Assert.True(romOffset >= sampleData.OriginalRomSizeBeforePadding);
            
            var flatByte = ByteGraphUtil.BuildFlatDataFrom(sampleData.SnesAddressSpace, snesAddress);
            Assert.NotNull(flatByte);
            Assert.NotNull(flatByte.Byte);
            Assert.Equal(0x00, flatByte.Byte.Value);
        }

        [Fact]
        public void TestEquals()
        {
            static void AssertImplementsComparable<TAnnotation>(AnnotationCollection annotations)
            {
                var annotation = (Annotation)annotations.SingleOrDefaultOfType(typeof(TAnnotation));
                Assert.NotNull(annotation);
                Assert.True(annotation is IComparable<TAnnotation>);
            }

            var rb1 = SampleRomByte1();
            var rb2 = SampleRomByte1();
            
            foreach (var item in rb1.Annotations)
            {
                Assert.True(item is IComparable);
            }

            AssertImplementsComparable<OpcodeAnnotation>(rb1.Annotations);
            AssertImplementsComparable<ByteAnnotation>(rb1.Annotations);
            AssertImplementsComparable<MarkAnnotation>(rb1.Annotations);
            AssertImplementsComparable<BranchAnnotation>(rb1.Annotations);

            rb1.Annotations.OrderBy(x => x);

            Assert.True(rb1.Annotations.EffectivelyEquals(rb2.Annotations));

            Assert.True(rb1.Equals(rb2));
            //Assert.True(rb1.EqualsButNoRomByte(rb2));
        }
    }
}
