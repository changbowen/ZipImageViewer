//KeyedCollection keeps an internal lookup dictionary for better performance.
//With normal implementations of an observable KeyedCollection, when used for
//data bindings in WPF, once the key of the item is changed with bindings, the
//corresponding key internal dictionary is not changed, which leads to hidden
//problems when the dictionary is used.
//Below is a custom implementation of KeyedCollection to solve that problem.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;

namespace ZipImageViewer {
    public class ObservableKeyedCollection<TKey, TItem> : KeyedCollection<TKey, TItem>, INotifyCollectionChanged, INotifyPropertyChanged {
        private readonly Func<TItem, TKey> _getKeyForItemDelegate;
        private readonly PropertyChangingEventHandler ChildPropertyChanging;
        private readonly PropertyChangedEventHandler ChildPropertyChanged;
        private readonly string keyPropertyName;

        /// <summary>
        /// Needs either a delegate or a property name to get the key from the child item.
        /// Using a delegate should be faster than the property name which will use reflection.
        /// If TItem implements both INotifyCollectionChanged and INotifyCollectionChanging, the name of the key property is also needed for key updating to work.
        /// </summary>
        public ObservableKeyedCollection(Func<TItem, TKey> getKeyForItemDelegate = null, string keyPropName = null) {
            if (getKeyForItemDelegate == null && keyPropName == null)
                throw new ArgumentException(@"getKeyForItemDelegate and KeyPropertyName cannot both be null.");
            keyPropertyName = keyPropName;
            _getKeyForItemDelegate = getKeyForItemDelegate;

            if (keyPropertyName != null &&
                typeof(TItem).GetInterface(nameof(INotifyPropertyChanged)) != null &&
                typeof(TItem).GetInterface(nameof(INotifyPropertyChanging)) != null) {
                ChildPropertyChanging = (o, e) => {
                    if (e.PropertyName != keyPropertyName) return;
                    var item = (TItem)o;
                    Dictionary?.Remove(GetKeyForItem(item));
                };
                ChildPropertyChanged = (o, e) => {
                    if (e.PropertyName != keyPropertyName) return;
                    var item = (TItem)o;
                    Dictionary?.Add(GetKeyForItem(item), item);
                };
            }
        }

        protected override TKey GetKeyForItem(TItem item) {
            if (_getKeyForItemDelegate != null)//delegate is faster than reflection.
                return _getKeyForItemDelegate(item);
            if (keyPropertyName != null)
                return (TKey)item.GetType().GetProperty(keyPropertyName).GetValue(item);
            throw new ArgumentException(@"getKeyForItemDelegate and KeyPropertyName cannot both be null.");
        }

        protected override void SetItem(int index, TItem newitem) {
            //need old item to use Replace action below.
            TItem olditem = base[index];
            UpdatePropChangeHandlers(olditem, false);
            UpdatePropChangeHandlers(newitem, true);
            base.SetItem(index, newitem);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newitem, olditem, index));
        }

        //Add method calls this internally. Lookup dictionary is updated automatically.
        protected override void InsertItem(int index, TItem item) {
            UpdatePropChangeHandlers(item, true);
            base.InsertItem(index, item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        protected override void ClearItems() {
            UpdatePropChangeHandlers(Items, null);
            base.ClearItems();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void RemoveItem(int index) {
            TItem item = this[index];
            UpdatePropChangeHandlers(item, false);
            base.RemoveItem(index);
            //using the overload without index causes binding problem when used in CompositeCollection.
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        private bool _deferNotifyCollectionChanged = false;
        public void AddRange(IEnumerable<TItem> items) {
            _deferNotifyCollectionChanged = true;
            foreach (var item in items) Add(item);//Add will call Insert internally.
            _deferNotifyCollectionChanged = false;

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (_deferNotifyCollectionChanged) return;

            //if you get InvalidOperation here and the collection is on the UI thread,
            //verify if there is any reference to the item to be deleted.
            if (!Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(() => NotifyChanges(e));
            else NotifyChanges(e);
        }

        private void UpdatePropChangeHandlers(TItem item, bool addOrRemove) {
            if (item == null || keyPropertyName == null || ChildPropertyChanging == null || ChildPropertyChanged == null) return;

            if (addOrRemove) {
                ((INotifyPropertyChanging)item).PropertyChanging += ChildPropertyChanging;
                ((INotifyPropertyChanged)item).PropertyChanged += ChildPropertyChanged;
            }
            else {
                ((INotifyPropertyChanging)item).PropertyChanging -= ChildPropertyChanging;
                ((INotifyPropertyChanged)item).PropertyChanged -= ChildPropertyChanged;
            }
        }

        private void UpdatePropChangeHandlers(IEnumerable<TItem> olditems, IEnumerable<TItem> newitems) {
            if (keyPropertyName == null || ChildPropertyChanging == null || ChildPropertyChanged == null) return;

            if (olditems != null) {
                foreach (var olditem in olditems) {
                    ((INotifyPropertyChanging)olditem).PropertyChanging -= ChildPropertyChanging;
                    ((INotifyPropertyChanged)olditem).PropertyChanged -= ChildPropertyChanged;
                }
            }
            if (newitems != null) {
                foreach (var newitem in newitems) {
                    ((INotifyPropertyChanging)newitem).PropertyChanging += ChildPropertyChanging;
                    ((INotifyPropertyChanged)newitem).PropertyChanged += ChildPropertyChanged;
                }
            }
        }

        private void NotifyChanges(NotifyCollectionChangedEventArgs e) {
            CollectionChanged?.Invoke(this, e);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        #region INotifyCollectionChanged Members

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }


    public class ObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
		public ObservableCollection() { }

		public ObservableCollection(IEnumerable<T> collection) {
			IList<T> items = Items;
			if (collection != null && items != null) {
				using (IEnumerator<T> enumerator = collection.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						items.Add(enumerator.Current);
					}
				}
			}
		}

		private void NotifyChanges(NotifyCollectionChangedEventArgs e) {
            CollectionChanged?.Invoke(this, e);
        }

        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
			if (_deferNotifyCollectionChanged) return;

			if (!Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(() => NotifyChanges(e));
            else NotifyChanges(e);
        }

		private bool _deferNotifyCollectionChanged = false;
		public void AddRange(IEnumerable<T> items) {
			_deferNotifyCollectionChanged = true;
			foreach (var item in items) Add(item);//Add will call Insert internally.
			_deferNotifyCollectionChanged = false;

			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}


    public class ObservablePair<TItem1, TItem2> : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private TItem1 item1;
        public TItem1 Item1 {
            get => item1;
            set {
                if (item1.Equals(value)) return;
                item1 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Item1)));
            }
        }

        private TItem2 item2;
        public TItem2 Item2 {
            get => item2;
            set {
                if (item2.Equals(value)) return;
                item2 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Item2)));
            }
        }

        //public static implicit operator System.Drawing.Size(ObservablePair<TItem1, TItem2> pair) {
        //    return new System.Drawing.Size(Convert.ToInt32(pair.Item1), Convert.ToInt32(pair.Item2));
        //}

        public ObservablePair(TItem1 i1, TItem2 i2) {
            item1 = i1;
            item2 = i2;
        }
    }


	/// <summary>
	/// Provides a dictionary for use with data binding.
	/// </summary>
	/// <typeparam name="TKey">Specifies the type of the keys in this collection.</typeparam>
	/// <typeparam name="TValue">Specifies the type of the values in this collection.</typeparam>
	[System.Diagnostics.DebuggerDisplay("Count={Count}")]
	public class ObservableDictionary<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>>, IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
	{
		readonly IDictionary<TKey, TValue> dictionary;

		/// <summary>Event raised when the collection changes.</summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged = (sender, args) => { };

		/// <summary>Event raised when a property on the collection changes.</summary>
		public event PropertyChangedEventHandler PropertyChanged = (sender, args) => { };

		/// <summary>
		/// Initializes an instance of the class.
		/// </summary>
		public ObservableDictionary() : this(new Dictionary<TKey, TValue>()) {
		}

		/// <summary>
		/// Initializes an instance of the class using another dictionary as 
		/// the key/value store.
		/// </summary>
		public ObservableDictionary(IDictionary<TKey, TValue> dictionary) {
			this.dictionary = dictionary;
		}

		void AddWithNotification(KeyValuePair<TKey, TValue> item) {
			AddWithNotification(item.Key, item.Value);
		}

		void AddWithNotification(TKey key, TValue value) {
			dictionary.Add(key, value);

			CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
				new KeyValuePair<TKey, TValue>(key, value)));
			PropertyChanged(this, new PropertyChangedEventArgs("Count"));
			PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
			PropertyChanged(this, new PropertyChangedEventArgs("Values"));
		}

		bool RemoveWithNotification(TKey key) {
			TValue value;
			if (dictionary.TryGetValue(key, out value) && dictionary.Remove(key)) {
				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
					new KeyValuePair<TKey, TValue>(key, value)));
				PropertyChanged(this, new PropertyChangedEventArgs("Count"));
				PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
				PropertyChanged(this, new PropertyChangedEventArgs("Values"));

				return true;
			}

			return false;
		}

		void UpdateWithNotification(TKey key, TValue value) {
			TValue existing;
			if (dictionary.TryGetValue(key, out existing)) {
				dictionary[key] = value;

				CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
					new KeyValuePair<TKey, TValue>(key, value),
					new KeyValuePair<TKey, TValue>(key, existing)));
				PropertyChanged(this, new PropertyChangedEventArgs("Values"));
			}
			else {
				AddWithNotification(key, value);
			}
		}

		/// <summary>
		/// Allows derived classes to raise custom property changed events.
		/// </summary>
		protected void RaisePropertyChanged(PropertyChangedEventArgs args) {
			PropertyChanged(this, args);
		}

		#region IDictionary<TKey,TValue> Members

		/// <summary>
		/// Adds an element with the provided key and value to the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <param name="key">The object to use as the key of the element to add.</param>
		/// <param name="value">The object to use as the value of the element to add.</param>
		public void Add(TKey key, TValue value) {
			AddWithNotification(key, value);
		}

		/// <summary>
		/// Determines whether the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key.
		/// </summary>
		/// <param name="key">The key to locate in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</param>
		/// <returns>
		/// true if the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the key; otherwise, false.
		/// </returns>
		public bool ContainsKey(TKey key) {
			return dictionary.ContainsKey(key);
		}

		/// <summary>
		/// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
		public ICollection<TKey> Keys {
			get { return dictionary.Keys; }
		}

		/// <summary>
		/// Removes the element with the specified key from the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <returns>
		/// true if the element is successfully removed; otherwise, false.  This method also returns false if <paramref name="key" /> was not found in the original <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </returns>
		public bool Remove(TKey key) {
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
		public bool TryGetValue(TKey key, out TValue value) {
			return dictionary.TryGetValue(key, out value);
		}

		/// <summary>
		/// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.
		/// </summary>
		/// <returns>An <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" />.</returns>
		public ICollection<TValue> Values {
			get { return dictionary.Values; }
		}

		/// <summary>
		/// Gets or sets the element with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public TValue this[TKey key] {
			get { return dictionary[key]; }
			set { UpdateWithNotification(key, value); }
		}

		#endregion

		#region ICollection<KeyValuePair<TKey,TValue>> Members

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) {
			AddWithNotification(item);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Clear() {
			((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Clear();

			CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			PropertyChanged(this, new PropertyChangedEventArgs("Count"));
			PropertyChanged(this, new PropertyChangedEventArgs("Keys"));
			PropertyChanged(this, new PropertyChangedEventArgs("Values"));
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) {
			return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Contains(item);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
			((ICollection<KeyValuePair<TKey, TValue>>)dictionary).CopyTo(array, arrayIndex);
		}

		int ICollection<KeyValuePair<TKey, TValue>>.Count {
			get { return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Count; }
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly {
			get { return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).IsReadOnly; }
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) {
			return RemoveWithNotification(item.Key);
		}

		#endregion

		#region IEnumerable<KeyValuePair<TKey,TValue>> Members

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
			return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).GetEnumerator();
		}

		#endregion
	}
}