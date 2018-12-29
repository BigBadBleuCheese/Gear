using Gear.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace Gear.ActiveQuery
{
    public class ActiveLookup<TKey, TValue> : SyncDisposablePropertyChangeNotifier, INotifyDictionaryChanged<TKey, TValue>, INotifyElementFaultChanges, IReadOnlyDictionary<TKey, TValue>, ISynchronized
    {
        internal ActiveLookup(IReadOnlyDictionary<TKey, TValue> readOnlyDictionary, Action onDispose = null)
        {
            synchronized = readOnlyDictionary as ISynchronized ?? throw new ArgumentException($"{nameof(readOnlyDictionary)} must implement {nameof(ISynchronized)}", nameof(readOnlyDictionary));
            if (readOnlyDictionary is ActiveLookup<TKey, TValue> activeLookup)
                this.readOnlyDictionary = activeLookup.readOnlyDictionary;
            else
                this.readOnlyDictionary = readOnlyDictionary;
            if (this.readOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryNotifier)
                dictionaryNotifier.DictionaryChanged += DictionaryChangedHandler;
            if (this.readOnlyDictionary is INotifyPropertyChanged propertyChangedNotifier)
                propertyChangedNotifier.PropertyChanged += PropertyChangedHandler;
            if (this.readOnlyDictionary is INotifyPropertyChanging propertyChangingNotifier)
                propertyChangingNotifier.PropertyChanging += PropertyChangingHandler;
            this.onDispose = onDispose;
        }

        internal ActiveLookup(IReadOnlyDictionary<TKey, TValue> readOnlyDictionary, INotifyElementFaultChanges faultNotifier, Action onDispose = null) : this(readOnlyDictionary, onDispose)
        {
            this.faultNotifier = faultNotifier ?? (readOnlyDictionary as INotifyElementFaultChanges);
            if (this.faultNotifier != null)
            {
                this.faultNotifier.ElementFaultChanged += FaultNotifierElementFaultChanged;
                this.faultNotifier.ElementFaultChanging += FaultNotifierElementFaultChanging;
            }
        }

        internal ActiveLookup(IReadOnlyDictionary<TKey, TValue> readOnlyDictionary, out Action<Exception> setOperationFault, Action onDispose = null) : this(readOnlyDictionary, out setOperationFault, null, onDispose)
        {
        }

        internal ActiveLookup(IReadOnlyDictionary<TKey, TValue> readOnlyDictionary, out Action<Exception> setOperationFault, INotifyElementFaultChanges faultNotifier = null, Action onDispose = null) : this(readOnlyDictionary, faultNotifier, onDispose) =>
            setOperationFault = SetOperationFault;

        readonly INotifyElementFaultChanges faultNotifier;
        readonly Action onDispose;
        Exception operationFault;
        readonly IReadOnlyDictionary<TKey, TValue> readOnlyDictionary;
        readonly ISynchronized synchronized;

        public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>> DictionaryChanged;
        public event EventHandler<ElementFaultChangeEventArgs> ElementFaultChanged;
        public event EventHandler<ElementFaultChangeEventArgs> ElementFaultChanging;

        public bool ContainsKey(TKey key) => readOnlyDictionary.ContainsKey(key);

        void DictionaryChangedHandler(object sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e) => DictionaryChanged?.Invoke(this, e);

        protected override void Dispose(bool disposing)
        {
            onDispose?.Invoke();
            if (disposing)
            {
                if (readOnlyDictionary is INotifyDictionaryChanged<TKey, TValue> dictionaryNotifier)
                    dictionaryNotifier.DictionaryChanged -= DictionaryChangedHandler;
                if (readOnlyDictionary is INotifyPropertyChanged propertyChangedNotifier)
                    propertyChangedNotifier.PropertyChanged -= PropertyChangedHandler;
                if (readOnlyDictionary is INotifyPropertyChanging propertyChangingNotifier)
                    propertyChangingNotifier.PropertyChanging -= PropertyChangingHandler;
            }
        }

        void FaultNotifierElementFaultChanged(object sender, ElementFaultChangeEventArgs e) => ElementFaultChanged?.Invoke(this, e);

        void FaultNotifierElementFaultChanging(object sender, ElementFaultChangeEventArgs e) => ElementFaultChanging?.Invoke(this, e);

        public IReadOnlyList<(object element, Exception fault)> GetElementFaults() => faultNotifier?.GetElementFaults();

        void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Count))
                OnPropertyChanged(e);
        }

        void PropertyChangingHandler(object sender, PropertyChangingEventArgs e)
        {
            if (e.PropertyName == nameof(Count))
                OnPropertyChanging(e);
        }

        public bool TryGetValue(TKey key, out TValue value) => readOnlyDictionary.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => readOnlyDictionary.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)readOnlyDictionary).GetEnumerator();

        void SetOperationFault(Exception operationFault) => OperationFault = operationFault;

        public TValue this[TKey key] => readOnlyDictionary[key];

        public int Count => readOnlyDictionary.Count;

        public IEnumerable<TKey> Keys => readOnlyDictionary.Keys;

        public Exception OperationFault
        {
            get => operationFault;
            private set => SetBackedProperty(ref operationFault, in value);
        }

        public SynchronizationContext SynchronizationContext => synchronized.SynchronizationContext;

        public IEnumerable<TValue> Values => readOnlyDictionary.Values;
    }
}
