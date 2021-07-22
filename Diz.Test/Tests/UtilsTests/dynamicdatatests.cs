using System;
using System.ComponentModel;
using System.Windows.Forms;
using DynamicData;
using FluentAssertions;
using Xunit;
using Label = Diz.Core.model.Label;

namespace Diz.Test
{
    public class Dynamicdatatests
    {
        [Fact]
        public static void Test1()
        {
            var sourceCache = new SourceCache<Label, int>(label=>label.Offset);

            // var sourceLabels = 
            //     sourceCache
            //     // .Filter(t => t.Status == "Something")
            //     .to();

            var dataBindingList = new BindingList<Label>()
            {
                AllowNew = true, AllowRemove = true, AllowEdit = true,
                RaiseListChangedEvents = true,
            };

            var observable = sourceCache.Connect();
            
            var disposable = observable
                // .Filter(Filter)
                .Bind(dataBindingList)
                // .DisposeMany()
                .Subscribe();

            // labelGrid.Columns.Clear();
            // labelGrid.Rows.Clear();
            // labelGrid.AutoGenerateColumns = true;

            // var bs = new BindingSource(dataBindingList, null);

            // labelGrid.DataSource = bs;

            sourceCache.AddOrUpdate(new Label
            {
                Comment = "test2",
                Name = "name2", Offset = 2,
            });
            
            sourceCache.AddOrUpdate(new Label
            {
                Comment = "test1",
                Name = "name1", Offset = 1,
            });

            sourceCache.Count.Should().Be(2);
            dataBindingList.Count.Should().Be(2);

            dataBindingList.Add(new Label() {Offset=3, Name="asdf3"});

            var dbl = new BindingSource()
            {
                DataSource = sourceCache,
                AllowNew = true,
            };

            //var x = new DataGridView();
            //x.DataSource

            // dbl.AllowEdit = true;
            
            // var x = dbl.AddNew() as Label;
            dbl.List.Add(new Label());
            
            // x.Should().NotBeNull();
            
            dbl.Count.Should().Be(3);
            sourceCache.Count.Should().Be(3);
        }
    }
}