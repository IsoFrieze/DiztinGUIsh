using System.Diagnostics.CodeAnalysis;
using Diz.Core.model;

namespace DiztinGUIsh.window2
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class BytesViewerController : IBytesViewerController
    {
        // private BindingListView<RomByteData> bindingList;
        public Data Data { get; set; }
        
        // option 2: set bindingList from an existing. we'll inherit all filters/etc automatically
        /*public BindingListView<RomByteData> BindingList
        {
            get => bindingList;
            set
            {
                bindingList = value;
                Data = bindingList?.DataSource as Data;
            }
        }*/

        // option 1: create new binding unique to us by making a new bindingList that is looking at the source data 
        /*public void CreateDataBindingTo(Data data)
        {
            CreateBindingListFrom(data);
            UpdateFilters();
        }

        private void CreateBindingListFrom(Data data)
        {
            bindingList = new BindingListView<RomByteData>(data.RomBytes);
            Data = data;
        }

        private void UpdateFilters()
        {
            bindingList.Filter = new PredicateItemFilter<RomByteData>(IsRomByteOpcode);
        }

        private static bool IsRomByteOpcode(RomByteData romByte)
        {
            return romByte.TypeFlag == FlagType.Opcode;
        }*/
    }
}