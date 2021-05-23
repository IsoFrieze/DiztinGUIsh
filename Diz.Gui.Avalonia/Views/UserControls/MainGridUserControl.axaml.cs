using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using Diz.Gui.Avalonia.ViewModels;
using ReactiveUI;

namespace Diz.Gui.Avalonia.Views.UserControls
{
    public class MainGridUserControl : ReactiveUserControl<ByteEntriesViewModel>
    {
        public DataGrid MainGrid => this.FindControl<DataGrid>("MainGrid");
        
        public MainGridUserControl()
        {
            ViewModel = new ByteEntriesViewModel();

            this.WhenActivated(disposables =>
            {
                // prob how we should do it with commands to edit
                this.OneWayBind(ViewModel,
                    viewmodel => viewmodel.ByteEntries,
                    view => view.MainGrid.Items
                ).DisposeWith(disposables);
                //
                // two way binding (try this?)
                // this.Bind(ViewModel,
                //     viewmodel => viewmodel.ByteEntries,
                //     view => view.MainGrid.Items
                // ).DisposeWith(disposables);

                this.WhenAnyValue(x => x.MainGrid.SelectedItem)
                    .BindTo(this, x => x.ViewModel.SelectedItem);

                this.Bind(ViewModel, 
                    vm => vm.SelectedItem, 
                    v => v.MainGrid.SelectedItem
                    );
                
                // MainGrid.LoadingRow += MainGridOnLoadingRow;
                
                // var x = new Style(
                //     x => x.OfType<DataGridCell>()
                //         .PropertyEquals(DataGridCell.NameProperty, true));

                //
                // this.
                // // Observable.FromEventPattern<DataGridCellEditEndedEventArgs>()
                //
                // MainGrid.CellEditEnded
                
                // this.BindCommand(ViewModel, vm=>vm.)
                
                // this.BindCommand(
                //     ViewModel,
                //     vm => vm.SetSelectedItem,
                //     v => v.MainGrid,
                //     nameof(MainGrid.CellEditEnded));
                
                // this.BindCommand(
                //     ViewModel,
                //     vm => vm.SetSelectedItem,
                //     v => v.MainGrid.SelectedItem,
                //     nameof(MainGrid.CellEditEnded));
                
//                this.BindCommand(ViewModel,
  //                  vm => vm.ByteEntries,
    //                view=>view.MainGrid.Items[0]
            });

            InitializeComponent();
        }

        private void MainGridOnLoadingRow(object? sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is not ByteEntryDetailsViewModel byteEntryDetailsViewModel)
                return;
            
            // set colors/etc
            // better ways to do this. HACK

            // var x = new Setter();
            // x.

            // dataObject.ByteEntry.
            // e.Row.Background = Brushes.Red;


            // e.Row.Background = new SolidColorBrush(Color)
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        }
}