using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Diz.Core.interfaces;
using JetBrains.Annotations;

namespace Diz.Core.model
{
    /// <summary>
    /// represent a label at a particular address
    /// NOTE: you can have labels at addresses in:
    /// 1) ROM (they will show up in the main table view)
    /// 2) anything else, like RAM (they will only show up in the label editor, not in the main table)
    ///
    /// Comments here are for the LABEL itself, and not so much about where they're used.
    /// i.e. a label for 0x7E0020 might store a character's HP in RAM. It would look like:
    /// - address: 0x7E0020 (0x7EXXXX means it's a RAM address)
    /// - label:   "character_3_hp"
    /// - comment: "this address is only used in RAM during battle sequences"
    ///            ^^^^---- will not show up in the main table, just the editor 
    /// </summary>
    public class Label : IEquatable<Label>, INotifyPropertyChangedExt
    {
        private string name;
        private string comment;

        public string Name
        {
            get => name;
            set => this.SetField(ref name, value);
        }

        public string Comment
        {
            get => comment;
            set => this.SetField(ref comment, value);
        } // user-generated text, comment only

        public void CleanUp()
        {
            Comment ??= "";
            Name ??= "";
        }

        public static bool operator ==(Label left, Label right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Label left, Label right)
        {
            return !Equals(left, right);
        }

        #region Equality

        public bool Equals(Label other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Comment == other.Comment;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Label) obj);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Comment != null ? Comment.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        
        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
