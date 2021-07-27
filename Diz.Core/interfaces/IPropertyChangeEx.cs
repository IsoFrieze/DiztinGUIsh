using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diz.Core.interfaces
{
    
    // makes it a little easier to deal with INotifyPropertyChanged in derived classes
    public interface INotifyPropertyChangedExt : INotifyPropertyChanged
    {
        // would be great if this didn't have to be public. :shrug:
        void OnPropertyChanged(string propertyName);
    }
    
    public static class NotifyPropertyChangedExtensions
    {
        // returns true if we set property to a new value
        public static bool SetField<T>(this INotifyPropertyChanged sender, PropertyChangedEventHandler handler, ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
        {
            if (FieldIsEqual(field, value, compareRefOnly)) 
                return false;
            
            field = value;
            
            handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        
        // returns true if we set property to a new value
        public static bool SetField<T>(this INotifyPropertyChangedExt sender, ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
        {
            if (FieldIsEqual(field, value, compareRefOnly)) 
                return false;
            
            field = value;
    
            sender.OnPropertyChanged(propertyName);
            return true;
        }

        public static bool FieldIsEqual<T>(T field, T value, bool compareRefOnly = false)
        {
            if (compareRefOnly)
            {
                if (ReferenceEquals(field, value))
                    return true;
            }
            else if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return true;
            }

            return false;
        }
    }
}