using System.Reactive.Disposables;
using System.Reactive.Linq;
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
            ViewModel = new LabelsViewModel();

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel,
                    viewmodel => viewmodel.Labels,
                    view => view.LabelGrid.Items
                ).DisposeWith(disposables);

                // this.Bind(ViewModel,
                //     viewmodel => viewmodel.Labels,
                //     view => view.LabelGrid.Items
                // ).DisposeWith(disposables);

                this.WhenAnyValue(x => x.LabelGrid.SelectedItem)
                    .BindTo(this, x => x.ViewModel.SelectedItem);

                this.WhenAnyValue(x => x.TxtOffsetSearch.Text)
                    .BindTo(this, x => x.ViewModel.OffsetFilter);

                this.Bind(ViewModel,
                    vm => vm.SelectedItem,
                    v => v.LabelGrid.SelectedItem
                );
            });

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}