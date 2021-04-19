using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Xunit;

namespace Diz.Test.tests.byteEntry
{
    public static class ByteCombineTests
    {
        [Fact]
        public static void TestCombining()
        {
            var byteEntry1 = CreateByteOneLabel("test1111");
            Assert.False(byteEntry1.DontSetParentOnCollectionItems);

            Assert.Throws<InvalidDataException>(() => byteEntry1.Annotations.VerifyAllowedToAppend(new Label()));
            Assert.True(byteEntry1.Annotations.VerifyAllowedToAppend(new Comment()));
            Assert.True(byteEntry1.Annotations.VerifyAllowedToAppend(new ByteAnnotation()));
            Assert.True(byteEntry1.Annotations.VerifyAllowedToAppend(new OpcodeAnnotation()));
        }

        [Fact]
        public static void TestCombineTwoLabelsFails()
        {
            var byteEntry1 = CreateByteOneLabel("test1111");
            var byteEntry2 = CreateByteOneLabel("test2222");
            Assert.False(byteEntry1.DontSetParentOnCollectionItems);
            var ex = Assert.Throws<InvalidOperationException>(() => byteEntry1.AppendAnnotationsFrom(byteEntry2));
            Assert.Contains("DontSetParentOnCollectionItems", ex.Message);
            
            Assert.Single(byteEntry1.Annotations);
            Assert.Single(byteEntry2.Annotations);
        }

        [Fact]
        public static void TestAllowedCombineButTriedTwoIncombinableItems()
        {
            var byteEntry1 = CreateByteOneLabel("test1111", true);
            var byteEntry2 = CreateByteOneLabel("test2222");
            Assert.True(byteEntry1.DontSetParentOnCollectionItems);

            var ex = Assert.Throws<InvalidDataException>(() => byteEntry1.AppendAnnotationsFrom(byteEntry2));
            Assert.Contains("Found multiple annotations", ex.Message);
            
            Assert.Single(byteEntry1.Annotations);
            Assert.Single(byteEntry2.Annotations);
        }

        [Fact]
        public static void TestSuccessfulUnrelatedCombine()
        {
            var byteEntry1 = CreateByteOneLabel("test1111", true);
            var byteEntry2 = CreateByteOneAnnotation(new Comment {Text = "9999"});
            Assert.True(byteEntry1.DontSetParentOnCollectionItems);

            Assert.Null(Record.Exception(() => byteEntry1.AppendAnnotationsFrom(byteEntry2)));
            Assert.Equal(2, byteEntry1.Annotations.Count);
            
            Assert.Equal("9999", byteEntry1.GetOneAnnotation<Comment>().Text);
            Assert.Equal("test1111", byteEntry1.GetOneAnnotation<Label>().Name);
        }
        
        [Fact]
        public static void TestByteAnnotationCombine()
        {
            var byteEntry1 = CreateByteOneAnnotation(new ByteAnnotation {Byte = 0xEE}, true);
            var byteEntry2 = CreateByteOneAnnotation(new ByteAnnotation {Byte = 0xFF});
            Assert.True(byteEntry1.DontSetParentOnCollectionItems);
            Assert.Single(byteEntry1.Annotations);

            Assert.Null(Record.Exception(() => byteEntry1.AppendAnnotationsFrom(byteEntry2)));
            
            // a little counter-intuitive. the combination process will reject duplicates of anything,
            // EXCEPT ByteAnnotation.  with ByteAnnotation, if there are two, it'll pick only the one from the container
            // being used as the combination base.
            Assert.Single(byteEntry1.Annotations);
            Assert.Equal(0xEE, byteEntry1.GetOneAnnotation<ByteAnnotation>().Byte);
        }

        [Fact]
        public static void TestSuccessfulUnrelatedMultipleCombine()
        {
            var entry1 = new ByteEntry(new AnnotationCollection
            {
                new Label {Name = "test1111"}, new ByteAnnotation(), new Comment()
            }) { DontSetParentOnCollectionItems = true };

            var entry2 = new ByteEntry(new AnnotationCollection
            {
                new OpcodeAnnotation(), new ByteAnnotation(), new MarkAnnotation()
            });

            Assert.True(entry1.DontSetParentOnCollectionItems);
            Assert.Null(Record.Exception(() => entry1.AppendAnnotationsFrom(entry2)));

            Assert.Equal(5, entry1.Annotations.Count);

            var expectedItemsCount = new List<Type>
            {
                typeof(Label), typeof(ByteAnnotation), typeof(Comment),
                typeof(OpcodeAnnotation), typeof(MarkAnnotation)
            }.ToDictionary(item => item, _ => 0);

            foreach (var item in entry1.Annotations)
            {
                var currentTotal = ++expectedItemsCount[item.GetType()];
                Assert.True(currentTotal <= 1);
            }

            var anyNotOne = expectedItemsCount.Any(pair => pair.Value != 1);
            Assert.False(anyNotOne);
        }

        private static ByteEntry CreateByteOneLabel(string labelName, bool dontSetParentOnCollectionItems = false)
        {
            return CreateByteOneAnnotation(new Label {Name = labelName}, dontSetParentOnCollectionItems);
        }

        private static ByteEntry CreateByteOneAnnotation(Annotation annotation, bool dontSetParentOnCollectionItems = false)
        {
            var entry = new ByteEntry(new AnnotationCollection {annotation})
            {
                DontSetParentOnCollectionItems = dontSetParentOnCollectionItems,
            };
            Assert.Single(entry.Annotations);
            Assert.Equal(dontSetParentOnCollectionItems, dontSetParentOnCollectionItems);
            return entry;
        }
    }
}