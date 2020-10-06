using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using IX.Observable;

namespace DiztinGUIsh.core.util
{
    // wrap an ObservableDictionary so we can implement non-generic IDictionary
    // this basically exists to work around ExtendedXmlSerializer trying to cast us to IDictionary and failing.
    // there's probably settings we can tweak in ExtendedXmlSerializer (particularly, Interceptor), and then
    // we can remove the need for this wrapper.
    public class OdWrapper<TKey, TValue>
    {
        public ObservableDictionary<TKey, TValue> ObservableDict { get; set; }
            = new ObservableDictionary<TKey, TValue>();

        public IDictionary DictToSave
        {
            get => new Dictionary<TKey, TValue>(ObservableDict);
            set
            {
                ObservableDict.Clear();
                foreach (DictionaryEntry item in value)
                {
                    ObservableDict.Add((TKey)item.Key, (TValue)item.Value);
                }
            }
        }

        #region Equality

        protected bool Equals(OdWrapper<TKey, TValue> other)
        {
            return ObservableDict.SequenceEqual(other.ObservableDict);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((OdWrapper<TKey, TValue>)obj);
        }

        public override int GetHashCode()
        {
            return (ObservableDict != null ? ObservableDict.GetHashCode() : 0);
        }

        #endregion
    }

    // this is pretty hacky.
    // anytime you use OdWrapper, you need to use this to tell the xml serialize to ignore the ObservableDict property
    // on OdWrapper.
    //
    // This really needs to go away and be solved more elegantly with some better use of ExtendedXmlSerializer.
    //
    // or... just move away from dictionaries for data storage. which would be a shame but probably easier
    public static class ObservedDictionaryHelper { 
        public static IConfigurationContainer AppendDisablingType<TKey, TValue>(this IConfigurationContainer @this)
            => @this.EnableImplicitTyping(typeof(OdWrapper<TKey, TValue>))
                .Type<OdWrapper<int, string>>()
                .Member(x => x.ObservableDict)
                .Ignore();
    }





    // older junkier attempts below. leaving for now, I'm still messing with this -Dom

    /*public class ObservableDictionaryAdaptor<TKey, TValue> : IDictionary, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly ObservableDictionary<TKey, TValue> dict = new ObservableDictionary<TKey, TValue>();
        public bool Contains(object key)
        {
            return dict.Keys.Contains((TKey)key);
        }

        public void Add(object key, object value)
        {
            dict.Add((TKey)key, (TValue)value);
        }

        public void Clear()
        {
            dict.Clear();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return new ODEnumerator<TKey, TValue>(dict);
        }

        public void Remove(object key)
        {
            dict.Remove((TKey)key);
        }

        public object this[object key]
        {
            get => dict[(TKey)key];
            set => dict[(TKey)key] = (TValue)value;
        }

        public ICollection Keys => (ICollection)dict.Keys;
        public ICollection Values => (ICollection)dict.Values;
        public bool IsReadOnly => dict.IsReadOnly;
        public bool IsFixedSize => false;

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)dict).GetEnumerator();
        }
        public void CopyTo(Array array, int index)
        {
            ((ICollection)dict).CopyTo(array, index);
        }

        public int Count => dict.Count;

#pragma warning disable CS0618
        public object SyncRoot => dict.SyncRoot;

#pragma warning disable CS0618
        public bool IsSynchronized => dict.IsSynchronized;
        public ObservableDictionary<TKey, TValue> Dict => dict;

        public bool TryGetValue(TKey key, out TValue value) => dict.TryGetValue(key, out value);

        public bool ContainsKey(TKey key)
        {
            return dict.ContainsKey(key);
        }
    }*/

    /*
    public class OdWrapper<TKey, TValue> : IDictionary
    {
        public ObservableDictionary<TKey, TValue> ObservableDict { get; set; }
            = new ObservableDictionary<TKey, TValue>();

        public IDictionary DictToSave
        {
            get => new Dictionary<TKey, TValue>(ObservableDict);
            set
            {
                ObservableDict.Clear();
                foreach (DictionaryEntry item in value)
                {
                    ObservableDict.Add((TKey)item.Key, (TValue)item.Value);
                }
            }
        }

        #region Equality

        protected bool Equals(OdWrapper<TKey, TValue> other)
        {
            return ObservableDict.SequenceEqual(other.ObservableDict);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((OdWrapper<TKey, TValue>)obj);
        }

        public override int GetHashCode()
        {
            return (ObservableDict != null ? ObservableDict.GetHashCode() : 0);
        }

        #endregion
    }

    /*
    public class ODEnumerator_Full_But_Not_Working<TKey, TValue> : IDictionaryEnumerator, IDisposable
    {
        private readonly IEnumerator<KeyValuePair<TKey, TValue>> impl;

        public void Dispose()
        {
            impl.Dispose();
        }

        public ODEnumerator(IDictionary<TKey, TValue> value)
        {
            impl = value.GetEnumerator();
        }

        public void Reset()
        {
            impl.Reset();
        }

        public bool MoveNext()
        {
            return impl.MoveNext();
        }

        public DictionaryEntry Entry
        {
            get
            {
                var pair = impl.Current;
                return new DictionaryEntry(pair.Key, pair.Value);
            }
        }

        public object Key => impl.Current.Key;
        public object Value => impl.Current.Value;
        public object Current => Entry;
    }
    */
}