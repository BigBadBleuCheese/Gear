using Gear.Components;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;

namespace Gear.ActiveQuery.MSTest
{
    public class TestSimpleObservableCollection<T> : PropertyChangeNotifier, IEnumerable<T>, INotifyCollectionChanged, ISynchronized
    {
        public TestSimpleObservableCollection(SynchronizationContext synchronizationContext, ObservableCollection<T> collection)
        {
            SynchronizationContext = synchronizationContext;
            this.collection = collection;
        }

        ObservableCollection<T> collection;
        bool isSynchronized = true;

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => collection.CollectionChanged += value;
            remove => collection.CollectionChanged -= value;
        }

        public IEnumerator<T> GetEnumerator() => collection.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)collection).GetEnumerator();

        public bool IsSynchronized
        {
            get => isSynchronized;
            set => SetBackedProperty(ref isSynchronized, in value);
        }

        public SynchronizationContext SynchronizationContext { get; }
    }
}
