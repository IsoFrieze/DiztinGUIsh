﻿using System;

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
    public class Label : IEquatable<Label>
    {
        public static bool operator ==(Label left, Label right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Label left, Label right)
        {
            return !Equals(left, right);
        }

        public int Offset { get; set; } = -1;
        public string Name { get; set; }
        public string Comment { get; set; }     // user-generated text, comment only
        public void CleanUp()
        {
            Comment ??= "";
            Name ??= "";
        }

        #region Equality

        public bool Equals(Label other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Offset == other.Offset && Name == other.Name && Comment == other.Comment;
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
                var hashCode = Offset;
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Comment != null ? Comment.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }
}
