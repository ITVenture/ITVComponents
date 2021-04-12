using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Threading;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Logging;

namespace ITVComponents.UserInterface.Collections
{
    /// <summary>
    /// Enables Cross-Thread notifications for observable collections
    /// </summary>
    /// <typeparam name="T">the element-type of this collection</typeparam>
    public class CascadingObservableUICollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// the dispatcher object that is used to invoke cross-thrad notifications
        /// </summary>
        private Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        /// <summary>
        /// The notify action for the collectionchanged event
        /// </summary>
        private Action<NotifyCollectionChangedEventArgs> notifyCollectionAction;

        /// <summary>
        /// the notify action for the propertychanged event
        /// </summary>
        private Action<PropertyChangedEventArgs> notifyPropertyAction;

        /// <summary>
        /// indicates whether to suppress notifications such as propertychanged or collectionchanged
        /// </summary>
        private bool suppressNotifications = false;

        /// <summary>
        /// Initializes a new instance of the ObservableUICollection class
        /// </summary>
        public CascadingObservableUICollection()
        {
            notifyCollectionAction = new Action<NotifyCollectionChangedEventArgs>(NotifyCollectionChanged);
            notifyPropertyAction = new Action<PropertyChangedEventArgs>(NotifyPropertyChanged);
        }

        /// <summary>
        /// Initializes a new instance of the ObservableUICollection class
        /// </summary>
        /// <param name="collection">an ienumerable that contains the initial items</param>
        public CascadingObservableUICollection(IEnumerable<T> collection)
            : base(collection)
        {
            notifyCollectionAction = new Action<NotifyCollectionChangedEventArgs>(NotifyCollectionChanged);
            notifyPropertyAction = new Action<PropertyChangedEventArgs>(NotifyPropertyChanged);
        }

        /// <summary>
        /// Initializes a new instance of the ObservableUICollection class
        /// </summary>
        /// <param name="list">the list that contains the initial items of this collection</param>
        public CascadingObservableUICollection(List<T> list)
            : base(list)
        {
            notifyCollectionAction = new Action<NotifyCollectionChangedEventArgs>(NotifyCollectionChanged);
            notifyPropertyAction = new Action<PropertyChangedEventArgs>(NotifyPropertyChanged);
        }

        /// <summary>
        /// Adds a range of items to this collection
        /// </summary>
        /// <param name="source">the enumerable source of items</param>
        public void AddRange(IEnumerable<T> source)
        {
            IList<T> ls = source.ToList();
            suppressNotifications = true;
            try
            {
                ls.ForEach(Add);
            }
            finally
            {
                suppressNotifications = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Ersetzt das Element am angegebenen Index.
        /// </summary>
        /// <param name="index">Der nullbasierte Index des zu ersetzenden Elements.</param><param name="item">Der neue Wert für das Element am angegebenen Index.</param>
        protected override void SetItem(int index, T item)
        {
            INotifyPropertyChanged tmp = Items[index] as INotifyPropertyChanged;
            if (tmp != null)
            {
                tmp.PropertyChanged -= Cascade;
            }

            base.SetItem(index, item);
        }

        /// <summary>
        /// Entfernt das Element am angegebenen Index aus der Auflistung.
        /// </summary>
        /// <param name="index">Der nullbasierte Index des zu entfernenden Elements.</param>
        protected override void RemoveItem(int index)
        {
            INotifyPropertyChanged tmp = Items[index] as INotifyPropertyChanged;
            if (tmp != null)
            {
                tmp.PropertyChanged -= Cascade;
            }

            base.RemoveItem(index);
        }

        /// <summary>
        /// Fügt ein Element am angegebenen Index in die Auflistung ein.
        /// </summary>
        /// <param name="index">Der nullbasierte Index, an dem <paramref name="item"/> eingefügt werden soll.</param><param name="item">Das einzufügende Objekt.</param>
        protected override void InsertItem(int index, T item)
        {
            INotifyPropertyChanged tmp = item as INotifyPropertyChanged;
            if (tmp != null)
            {
                tmp.PropertyChanged += Cascade;
            }

            base.InsertItem(index, item);
        }

        /// <summary>
        /// Entfernt alle Elemente aus der Auflistung.
        /// </summary>
        protected override void ClearItems()
        {
            foreach (INotifyPropertyChanged item in Items.Where(n => n is INotifyPropertyChanged))
            {
                item.PropertyChanged -= Cascade;
            }

            base.ClearItems();
        }

        /// <summary>
        /// Löst das <see cref="E:System.Collections.ObjectModel.ObservableCollection`1.CollectionChanged"/>-Ereignis mit den angegebenen Argumenten aus.
        /// </summary>
        /// <param name="e">Argumente des ausgelösten Ereignisses.</param>
        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!suppressNotifications)
            {
                if (dispatcher.CheckAccess())
                {
                    base.OnCollectionChanged(e);
                    return;
                }

                dispatcher.Invoke(notifyCollectionAction, e);
            }
        }

        /// <summary>
        /// Löst das <see cref="E:System.Collections.ObjectModel.ObservableCollection`1.PropertyChanged"/>-Ereignis mit den angegebenen Argumenten aus.
        /// </summary>
        /// <param name="e">Argumente des ausgelösten Ereignisses.</param>
        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!suppressNotifications)
            {
                if (dispatcher.CheckAccess())
                {
                    base.OnPropertyChanged(e);
                    return;
                }

                dispatcher.Invoke(notifyPropertyAction, e);
            }
        }

        /// <summary>
        /// Raises the NotifyCollectionChanged event
        /// </summary>
        /// <param name="e">the event arguments</param>
        private void NotifyCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            OnCollectionChanged(e);
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="e">the event arguments</param>
        private void NotifyPropertyChanged(PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e);
        }

        /// <summary>
        /// Causes this collection to cascade changes of items as collection-changed events
        /// </summary>
        /// <param name="sender">the item that has changed</param>
        /// <param name="e">the changed property</param>
        private void Cascade(object sender, PropertyChangedEventArgs e)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
