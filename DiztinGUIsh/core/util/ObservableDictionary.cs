using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace DiztinGUIsh.core.util
{
	// based on: kzu/ObservableDictionary.cs    https://gist.github.com/kzu/cfe3cb6e4fe3efea6d24
	// adds implementation of IDictionary, which we need.

	/// <summary>
	/// Provides a dictionary for use with data binding.
	/// </summary>
	/// <typeparam name="TKey">Specifies the type of the keys in this collection.</typeparam>
	/// <typeparam name="TValue">Specifies the type of the values in this collection.</typeparam>
	[DebuggerDisplay("Count={" + nameof(Count) + "}")]
	public class ObservableDictionary<TKey, TValue> :
		// ICollection<KeyValuePair<TKey, TValue>>,
        IDictionary<TKey, TValue>,
		IDictionary,
		INotifyCollectionChanged, INotifyPropertyChanged
	{
        private readonly IDictionary<TKey, TValue> dictionary;

        /// <summary>Event raised when the collection changes.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged = (sender, args) => { };

		/// <summary>Event raised when a property on the collection changes.</summary>
		public event PropertyChangedEventHandler PropertyChanged = (sender, args) => { };

		/// <summary>
		/// Initializes an instance of the class.
		/// </summary>
		public ObservableDictionary()
			: this(new Dictionary<TKey, TValue>())
		{
		}

		/// <summary>
		/// Initializes an instance of the class using another dictionary as 
		/// the key/value store.
		/// </summary>
		public ObservableDictionary(IDictionary<TKey, TValue> dictionary)
		{
			this.dictionary = dictionary;
		}

        private void AddWithNotification(KeyValuePair<TKey, TValue> item)
		{
			AddWithNotification(item.Key, item.Value);
		}

        private void AddWithNotification(TKey key, TValue value)
		{
			dictionary.Add(key, value);

			CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
				new KeyValuePair<TKey, TValue>(key, value)));
			PropertyChanged(this, new PropertyChangedEventArgs("Count"));
			PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
			PropertyChanged(this, new PropertyChangedEventArgs("Values"));
		}

        private bool RemoveWithNotification(TKey key)
		{
            if (!dictionary.TryGetValue(key, out var value) || !dictionary.Remove(key)) 
                return false;

            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                new KeyValuePair<TKey, TValue>(key, value)));
            PropertyChanged(this, new PropertyChangedEventArgs("Count"));
            PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
            PropertyChanged(this, new PropertyChangedEventArgs("Values"));

            return true;
        }

        private void UpdateWithNotification(TKey key, TValue value)
        {
            if (!dictionary.TryGetValue(key, out var existing))
            {
                AddWithNotification(key, value);
                return;
            }

            dictionary[key] = value;

            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                new KeyValuePair<TKey, TValue>(key, value),
                new KeyValuePair<TKey, TValue>(key, existing)));
            PropertyChanged(this, new PropertyChangedEventArgs("Values"));
        }

		/// <summary>
		/// Allows derived classes to raise custom property changed events.
		/// </summary>
		protected void RaisePropertyChanged(PropertyChangedEventArgs args)
		{
			PropertyChanged(this, args);
		}

		#region IDictionary<TKey,TValue> Members

		/// <summary>
		/// Adds an element with the provided key and value to the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <param name="key">The object to use as the key of the element to add.</param>
		/// <param name="value">The object to use as the value of the element to add.</param>
		public void Add(TKey key, TValue value)
		{
			AddWithNotification(key, value);
		}

		/// <summary>
		/// Determines whether the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key.
		/// </summary>
		/// <param name="key">The key to locate in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</param>
		/// <returns>
		/// true if the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the key; otherwise, false.
		/// </returns>
		public bool ContainsKey(TKey key)
		{
			return dictionary.ContainsKey(key);
		}

		/// <summary>
		/// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
		public ICollection<TKey> Keys => dictionary.Keys;

        // ICollection IDictionary.Values => (ICollection) dictionary.Values;

        /// <summary>
		/// Removes the element with the specified key from the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <returns>
		/// true if the element is successfully removed; otherwise, false.  This method also returns false if <paramref name="key" /> was not found in the original <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </returns>
		public bool Remove(TKey key)
		{
			return RemoveWithNotification(key);
		}

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key whose value to get.</param>
		/// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value" /> parameter. This parameter is passed uninitialized.</param>
		/// <returns>
		/// true if the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key; otherwise, false.
		/// </returns>
		public bool TryGetValue(TKey key, out TValue value)
		{
			return dictionary.TryGetValue(key, out value);
		}

        /// <summary>
		/// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
		public ICollection<TValue> Values => dictionary.Values;

        /// <summary>
		/// Gets or sets the element with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public TValue this[TKey key]
		{
			get => dictionary[key];
            set => UpdateWithNotification(key, value);
        }

		#endregion

		#region ICollection<KeyValuePair<TKey,TValue>> Members

        public void Add(KeyValuePair<TKey, TValue> item)
		{
			AddWithNotification(item);
		}



		public void Clear()
		{
			dictionary.Clear();

			CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			PropertyChanged(this, new PropertyChangedEventArgs("Count"));
			PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
			PropertyChanged(this, new PropertyChangedEventArgs("Values"));
		}

        public bool Contains(KeyValuePair<TKey, TValue> item) => dictionary.Contains(item);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			dictionary.CopyTo(array, arrayIndex);
		}

        public int Count => dictionary.Count;

        public bool IsReadOnly => dictionary.IsReadOnly;

        public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			return RemoveWithNotification(item.Key);
		}

		#endregion

		#region IEnumerable<KeyValuePair<TKey,TValue>> Members

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		{
			return dictionary.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return dictionary.GetEnumerator();
		}

		#endregion


		#region IDictionary

        ICollection IDictionary.Keys => (ICollection)dictionary.Keys;
        ICollection IDictionary.Values => (ICollection)dictionary.Values;
		public void Add(object key, object value)
        {
            AddWithNotification((TKey)key, (TValue)value);
        }
        public IDictionaryEnumerator GetEnumerator()
        {
            return ((IDictionary)(dictionary)).GetEnumerator();
        }

        public void Remove(object key)
        {
            RemoveWithNotification((TKey)key);
        }

        public object this[object key]
        {
            get => dictionary[(TKey)key];
            set => UpdateWithNotification((TKey)key, (TValue)value);
        }
        public bool Contains(object key) => dictionary.Contains((KeyValuePair<TKey, TValue>)key);
        public void CopyTo(Array array, int index)
        {
            CopyTo((KeyValuePair<TKey, TValue>[])(array), index);
        }

        public object SyncRoot => null; // TODO?
        public bool IsSynchronized => false; // TODO?
        public bool IsFixedSize => false; // TODO?

        #endregion
    }
}
