using System;
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
            }) {
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
            var ac = new AnnotationCollection {label1, mark1};
            
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
            var entry = new ByteEntry();
            Assert.Null(entry.Annotations);
            var nothing = entry.GetOneAnnotation<Comment>();
            Assert.Null(nothing);
            Assert.Null(entry.Annotations); // looking up something that doesn't exist should NOT auto-recreate this. save memory.
            Assert.Throws<InvalidOperationException>(() => entry.AppendAnnotationsFrom(null));
            Assert.Throws<InvalidOperationException>(() => entry.ReplaceAnnotationsWith(null));
            Assert.Null(entry.Annotations);
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
        public void TestEquals()
        {
            var rb1 = SampleRomByte1();
            var rb2 = SampleRomByte1();

            Assert.True(rb1.Equals(rb2));
            //Assert.True(rb1.EqualsButNoRomByte(rb2));
        }
    }
}
