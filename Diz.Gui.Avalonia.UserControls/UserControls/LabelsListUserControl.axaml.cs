using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Diz.Gui.ViewModels.ViewModels;
using ReactiveUI;

namespace Diz.Gui.Avalonia.UserControls.UserControls
{
    public class LabelsListUserControl : ReactiveUserControl<LabelsViewModel>
    {
        public DataGrid LabelGrid => this.FindControl<DataGrid>("LabelGrid");

        public LabelsListUserControl()
        {
            ViewModel = new LabelsViewModel();

            this.WhenActivated(disposables =>
            {
                // prob how we should do it with commands to edit
                this.OneWayBind(ViewModel,
                    viewmodel => viewmodel.Labels,
                    view => view.LabelGrid.Items
                ).DisposeWith(disposables);
                //
                // two way binding (try this?)
                // this.Bind(ViewModel,
                //     viewmodel => viewmodel.ByteEntries,
                //     view => view.LabelGrid.Items
                // ).DisposeWith(disposables);

                this.WhenAnyValue(x => x.LabelGrid.SelectedItem)
                    .BindTo(this, x => x.ViewModel.SelectedItem);

                this.Bind(ViewModel,
                    vm => vm.SelectedItem,
                    v => v.LabelGrid.SelectedItem
                );

                // LabelGrid.LoadingRow += LabelGridOnLoadingRow;

                // var x = new Style(
                //     x => x.OfType<DataGridCell>()
                //         .PropertyEquals(DataGridCell.NameProperty, true));

                //
                // this.
                // // Observable.FromEventPattern<DataGridCellEditEndedEventArgs>()
                //
                // LabelGrid.CellEditEnded

                // this.BindCommand(ViewModel, vm=>vm.)

                // this.BindCommand(
                //     ViewModel,
                //     vm => vm.SetSelectedItem,
                //     v => v.LabelGrid,
                //     nameof(LabelGrid.CellEditEnded));

                // this.BindCommand(
                //     ViewModel,
                //     vm => vm.SetSelectedItem,
                //     v => v.LabelGrid.SelectedItem,
                //     nameof(LabelGrid.CellEditEnded));

//                this.BindCommand(ViewModel,
                //                  vm => vm.ByteEntries,
                //                view=>view.LabelGrid.Items[0]
            });

            InitializeComponent();
        }

        // private void LabelGridOnLoadingRow(object sender, DataGridRowEventArgs e)
        // {
        //     if (e.Row.DataContext is not LabelsViewModel byteEntryDetailsViewModel)
        //         return;
        //
        //     // set colors/etc
        //     // better ways to do this. HACK
        //
        //     // var x = new Setter();
        //     // x.
        //
        //     // dataObject.ByteEntry.
        //     // e.Row.Background = Brushes.Red;
        //
        //
        //     // e.Row.Background = new SolidColorBrush(Color)
        // }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}