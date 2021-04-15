using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Xunit;

namespace Diz.Test
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
            return new() {
                Annotations = new AnnotationCollection
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
                },
                Byte = 0x78,
            };
        }
        
        private static ByteEntry SampleRomByte4()
        {
            // same as above, but just change .Rom
            var rb = SampleRomByte2();
            rb.GetOrCreateAnnotationsList();
            
            rb.Annotations.Add(new Comment {Text="CommentText"});
            rb.Annotations.Add(new Label {Name="fn_stuff", Comment = "some_comment"});
            return rb;
        }

        private static ByteEntry SampleRomByte2()
        {
            // same as above, but just change .Rom
            var rb = SampleRomByte1();
            rb.Byte = 0x99;
            return rb;
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

            rb1.GetOrCreateAnnotationsList();
            rb3.GetOrCreateAnnotationsList();

            rb1.Annotations.Add(new Comment() {Text="asdf2"});
            rb1.Annotations.Add(new Comment() {Text="asdf1"});
            
            rb1.Annotations.Add(new Label() {Name="asdf"});
            Assert.NotEqual(rb1, rb3);
            
            rb3.Annotations.Add(new Comment() {Text="asdf1"}); // note: order reversed
            rb3.Annotations.Add(new Comment() {Text="asdf2"}); // note: order reversed
            
            rb3.Annotations.Add(new Label() {Name="asdf"});
            Assert.Equal(rb1, rb3); // even with reverse order, should still be true
        }
        
        
        [Fact]
        public void AnnotationAccess1()
        {
            var rb3 = SampleRomByte3();
            Assert.Null(rb3.GetOneAnnotation<Comment>());
            
            rb3.Annotations.Add(new Comment() {Text="1"});
            Assert.NotNull(rb3.GetOneAnnotation<Comment>());
            
            rb3.Annotations.Add(new Comment() {Text="2"});
            Assert.Throws<InvalidDataException>(() => rb3.GetOneAnnotation<Comment>());
        }

        [Fact]
        public void AnnotationCollectionTests()
        {
            var b = new ByteEntry();
            var label1 = new Label {Name = "test11", Comment = "asdf"};
            var mark1 = new MarkAnnotation {TypeFlag = FlagType.Graphics};

            Assert.Null(mark1.Parent);
            Assert.Null(label1.Parent);
            var ac = new AnnotationCollection() {label1, mark1};
            
            Assert.Null(mark1.Parent);
            Assert.Null(label1.Parent);
            b.Annotations = ac;
            
            Assert.Equal(b, mark1.Parent);
            Assert.Equal(b, label1.Parent);

            b.Annotations = null;
            Assert.Null(mark1.Parent);
            Assert.Null(label1.Parent);
        }
        

        [Fact]
        public void TestEquals()
        {
            var rb1 = SampleRomByte1();
            var rb2 = SampleRomByte1();

            Assert.True(rb1.Equals(rb2));
            //Assert.True(rb1.EqualsButNoRomByte(rb2));
        }
    }
}
