using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public class Label
    {
        public string name = "";        // name of the label
        public string comment = "";     // user-generated text, comment only
        public void CleanUp()
        {
            if (comment == null) comment = "";
            if (name == null) name = "";
        }

        #region Equality

        protected bool Equals(Label other)
        {
            return name == other.name && comment == other.comment;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Label)obj);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return ((name != null ? name.GetHashCode() : 0) * 397) ^ (comment != null ? comment.GetHashCode() : 0);
            }
        }

        #endregion
    }
}
