//KeyedCollection keeps an internal lookup dictionary for better performance.
//With normal implementations of an observable KeyedCollection, when used for
//data bindings in WPF, once the key of the item is changed with bindings, the
//corresponding key internal dictionary is not changed, which leads to hidden
//problems when the dictionary is used.
//Below is a custom implementation of KeyedCollection to solve that problem.

using System;
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
        private void NotifyChanges(NotifyCollectionChangedEventArgs e) {
            CollectionChanged?.Invoke(this, e);
        }

        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (!Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(() => NotifyChanges(e));
            else NotifyChanges(e);
        }
    }
}