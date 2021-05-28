using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Diz.Core.model;
using Diz.ViewModels;
using FluentAssertions;
using Moq;
using ReactiveUI;
using Xunit;

namespace Diz.Test.Tests.ViewModels
{
    public class ViewModelTests
    {
        public interface IDummy
        {
            void DummyMethod(ObservableCollection<ByteEntryDetailsViewModel> observable);
        }
        
        [Fact(Skip="This test is correct, but, fails because the underlying data model doesn't support this year. We should fix that so this test passes.")]
        public void TestByteEntriesViewModelTwoCopies()
        {
            var vm1 = new ByteEntriesViewModel();
            var vm2 = new ByteEntriesViewModel();
            vm1.ByteEntries.Count.Should().BeGreaterThan(1);
            vm2.ByteEntries.Count.Should().BeGreaterThan(1);

            var byteAnnotation1 = vm1.ByteEntries[0].ByteEntry.ByteEntry.Annotations.GetOne<ByteAnnotation>();
            byteAnnotation1.Should().NotBeNull();
            
            byteAnnotation1.Val = 77;
            
            var byteAnnotation2 = vm1.ByteEntries[0].ByteEntry.ByteEntry.Annotations.GetOne<ByteAnnotation>();
            byteAnnotation2.Val.Should().Be(77);

            var m = new Mock<IDummy>();
            
            m.Setup(x => x
                .DummyMethod(
                    It.IsAny<ObservableCollection<ByteEntryDetailsViewModel>>())
            );

            vm1.WhenAnyValue(x => x.ByteEntries)
                .Subscribe(m.Object.DummyMethod);

            byteAnnotation1.Val = 22;

            m.Verify(
                mock => mock.DummyMethod(null),
                Times.Once
            );
        }
    }
}