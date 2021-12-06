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

        public TextBox TxtOffsetSearch => this.FindControl<TextBox>("TxtOffsetSearch");

        public LabelsListUserControl()
        {
            InitializeComponent();
            
            ViewModel = new LabelsViewModel();

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel,
                    vm => vm.SearchResults,
                    v => v.LabelGrid.Items
                ).DisposeWith(disposables);

                // this.Bind(ViewModel,
                //     viewmodel => viewmodel.Labels,
                //     view => view.LabelGrid.Items
                // ).DisposeWith(disposables);

                this.WhenAnyValue(v => v.LabelGrid.SelectedItem)
                    .BindTo(this, vm => vm.ViewModel.SelectedItem);

                this.WhenAnyValue(v => v.TxtOffsetSearch.Text)
                    .BindTo(this, vm => vm.ViewModel.SearchText);

                this.Bind(ViewModel,
                    vm => vm.SelectedItem,
                    v => v.LabelGrid.SelectedItem
                );
            });
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}