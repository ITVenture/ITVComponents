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
    public class ObservableUICollection<T> : ObservableCollection<T>
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
        public ObservableUICollection()
        {
            notifyCollectionAction = new Action<NotifyCollectionChangedEventArgs>(NotifyCollectionChanged);
            notifyPropertyAction = new Action<PropertyChangedEventArgs>(NotifyPropertyChanged);
        }

        /// <summary>
        /// Initializes a new instance of the ObservableUICollection class
        /// </summary>
        /// <param name="collection">an ienumerable that contains the initial items</param>
        public ObservableUICollection(IEnumerable<T> collection) : base(collection)
        {
            notifyCollectionAction = new Action<NotifyCollectionChangedEventArgs>(NotifyCollectionChanged);
            notifyPropertyAction = new Action<PropertyChangedEventArgs>(NotifyPropertyChanged);
        }

        /// <summary>
        /// Initializes a new instance of the ObservableUICollection class
        /// </summary>
        /// <param name="list">the list that contains the initial items of this collection</param>
        public ObservableUICollection(List<T> list) : base(list)
        {
            notifyCollectionAction = new Action<NotifyCollectionChangedEventArgs>(NotifyCollectionChanged);
            notifyPropertyAction = new Action<PropertyChangedEventArgs>(NotifyPropertyChanged);
        }

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
    }
}
