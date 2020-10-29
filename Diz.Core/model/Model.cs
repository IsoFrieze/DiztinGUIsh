using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace DiztinGUIsh
{
    public class DizDataModel : PropertyNotifyChanged
    {

    }
    public class PropertyNotifyChanged : INotifyPropertyChanged
    {
        // this stuff lets other parts of code subscribe to events that get fired anytime
        // properties of our class change.
        //
        // Just hook up SetField() to the 'set' param of any property you would like to 
        // expose to outside classes.
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetField<T>(ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
        {
            if (compareRefOnly)
            {
                if (ReferenceEquals(field, value))
                    return false;
            } 
            else if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
