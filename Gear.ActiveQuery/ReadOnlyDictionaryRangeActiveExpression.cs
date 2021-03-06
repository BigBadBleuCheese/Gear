using Gear.ActiveExpressions;
using Gear.Components;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace Gear.ActiveQuery
{
    /// <summary>
    /// Represents the dictionary of results derived from creating an active expression for each key-value pair in a dictionary
    /// </summary>
    /// <typeparam name="TKey">The type of the keys</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary</typeparam>
    /// <typeparam name="TResult">The type of the result of the active expression</typeparam>
    class ReadOnlyDictionaryRangeActiveExpression<TKey, TValue, TResult> : OverridableSyncDisposablePropertyChangeNotifier, INotifyDictionaryChanged<TKey, TResult>, INotifyElementFaultChanges
    {
        static readonly object rangeActiveExpressionsAccess = new object();
        static readonly Dictionary<(IReadOnlyDictionary<TKey, TValue> source, Expression<Func<TKey, TValue, TResult>> expression, ActiveExpressionOptions options), ReadOnlyDictionaryRangeActiveExpression<TKey, TValue, TResult>> rangeActiveExpressions = new Dictionary<(IReadOnlyDictionary<TKey, TValue> source, Expression<Func<TKey, TValue, TResult>> expression, ActiveExpressionOptions options), ReadOnlyDictionaryRangeActiveExpression<TKey, TValue, TResult>>(CachedRangeActiveExpressionKeyEqualityComparer<TKey, TValue, TResult>.Default);

        public static ReadOnlyDictionaryRangeActiveExpression<TKey, TValue, TResult> Create(IReadOnlyDictionary<TKey, TValue> source, Expression<Func<TKey, TValue, TResult>> expression, ActiveExpressionOptions options = null)
        {
            ReadOnlyDictionaryRangeActiveExpression<TKey, TValue, TResult> rangeActiveExpression;
            bool monitorCreated;
            var key = (source, expression, options);
            lock (rangeActiveExpressionsAccess)
            {
                if (monitorCreated = !rangeActiveExpressions.TryGetValue(key, out rangeActiveExpression))
                {
                    rangeActiveExpression = new ReadOnlyDictionaryRangeActiveExpression<TKey, TValue, TResult>(source, expression, options);
                    rangeActiveExpressions.Add(key, rangeActiveExpression);
                }
                ++rangeActiveExpression.disposalCount;
            }
            if (monitorCreated)
                rangeActiveExpression.Initialize();
            return rangeActiveExpression;
        }

        ReadOnlyDictionaryRangeActiveExpression(IReadOnlyDictionary<TKey, TValue> source, Expression<Func<TKey, TValue, TResult>> expression, ActiveExpressionOptions options)
        {
            this.source = source;
            this.expression = expression;
            Options = options;
            activeExpressions = this.source.CreateSimilarDictionary<TKey, TValue, IActiveExpression<TKey, TValue, TResult>>();
        }

        readonly IDictionary<TKey, IActiveExpression<TKey, TValue, TResult>> activeExpressions;
        readonly ReaderWriterLockSlim activeExpressionsAccess = new ReaderWriterLockSlim();
        int disposalCount;
        readonly Expression<Func<TKey, TValue, TResult>> expression;
        readonly IReadOnlyDictionary<TKey, TValue> source;

        public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TResult>> DictionaryChanged;
        public event EventHandler<ElementFaultChangeEventArgs> ElementFaultChanged;
        public event EventHandler<ElementFaultChangeEventArgs> ElementFaultChanging;
        public event EventHandler<RangeActiveExpressionResultChangeEventArgs<TKey, TResult>> ValueResultChanged;
        public event EventHandler<RangeActiveExpressionResultChangeEventArgs<TKey, TResult>> ValueResultChanging;

        void ActiveExpressionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var activeExpression = (IActiveExpression<TKey, TValue, TResult>)sender;
            if (e.PropertyName == nameof(IActiveExpression<TKey, TValue, TResult>.Fault))
            {
                activeExpressionsAccess.EnterReadLock();
                try
                {
                    OnElementFaultChanged(activeExpression.Arg1, activeExpression.Fault);
                }
                finally
                {
                    activeExpressionsAccess.ExitReadLock();
                }
            }
            else if (e.PropertyName == nameof(IActiveExpression<TKey, TValue, TResult>.Value))
            {
                activeExpressionsAccess.EnterReadLock();
                try
                {
                    OnValueResultChanged(activeExpression.Arg1, activeExpression.Value);
                }
                finally
                {
                    activeExpressionsAccess.ExitReadLock();
                }
            }
        }

        void ActiveExpressionPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            var activeExpression = (IActiveExpression<TKey, TValue, TResult>)sender;
            if (e.PropertyName == nameof(IActiveExpression<TKey, TValue, TResult>.Fault))
            {
                activeExpressionsAccess.EnterReadLock();
                try
                {
                    OnElementFaultChanging(activeExpression.Arg1, activeExpression.Fault);
                }
                finally
                {
                    activeExpressionsAccess.ExitReadLock();
                }
            }
            else if (e.PropertyName == nameof(IActiveExpression<TKey, TValue, TResult>.Value))
            {
                activeExpressionsAccess.EnterReadLock();
                try
                {
                    OnValueResultChanging(activeExpression.Arg1, activeExpression.Value);
                }
                finally
                {
                    activeExpressionsAccess.ExitReadLock();
                }
            }
        }

        IReadOnlyList<KeyValuePair<TKey, TResult>> AddActiveExpressions(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            if (keyValuePairs.Any())
            {
                List<IActiveExpression<TKey, TValue, TResult>> addedActiveExpressions;
                activeExpressionsAccess.EnterWriteLock();
                try
                {
                    addedActiveExpressions = AddActiveExpressionsUnderLock(keyValuePairs);
                }
                finally
                {
                    activeExpressionsAccess.ExitWriteLock();
                }
                return addedActiveExpressions.Select(ae => new KeyValuePair<TKey, TResult>(ae.Arg1, ae.Value)).ToImmutableArray();
            }
            return null;
        }

        private List<IActiveExpression<TKey, TValue, TResult>> AddActiveExpressionsUnderLock(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            var addedActiveExpressions = new List<IActiveExpression<TKey, TValue, TResult>>();
            foreach (var keyValuePair in keyValuePairs)
            {
                var activeExpression = ActiveExpression.Create(expression, keyValuePair.Key, keyValuePair.Value, Options);
                activeExpression.PropertyChanging += ActiveExpressionPropertyChanging;
                activeExpression.PropertyChanged += ActiveExpressionPropertyChanged;
                addedActiveExpressions.Add(activeExpression);
                activeExpressions.Add(keyValuePair.Key, activeExpression);
            }
            return addedActiveExpressions;
        }

        protected override bool Dispose(bool disposing)
        {
            lock (rangeActiveExpressionsAccess)
            {
                if (--disposalCount > 0)
                    return false;
                rangeActiveExpressions.Remove((source, expression, Options));
            }
            RemoveActiveExpressions(activeExpressions.Keys.ToImmutableArray());
            if (source is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
                dictionaryChangedNotifier.DictionaryChanged -= SourceDictionaryChanged;
            if (source is INotifyElementFaultChanges faultNotifier)
            {
                faultNotifier.ElementFaultChanged -= SourceFaultChanged;
                faultNotifier.ElementFaultChanging -= SourceFaultChanging;
            }
            return true;
        }

        public IReadOnlyList<(object element, Exception fault)> GetElementFaults()
        {
            activeExpressionsAccess.EnterReadLock();
            try
            {
                return GetElementFaultsUnderLock();
            }
            finally
            {
                activeExpressionsAccess.ExitReadLock();
            }
        }

        internal IReadOnlyList<(object element, Exception fault)> GetElementFaultsUnderLock() => activeExpressions.Select(ae => (element: (object)ae.Key, fault: ae.Value.Fault)).Where(ef => ef.fault != null).ToImmutableArray();

        public IReadOnlyList<(TKey key, TResult result)> GetResults()
        {
            activeExpressionsAccess.EnterReadLock();
            try
            {
                return GetResultsUnderLock();
            }
            finally
            {
                activeExpressionsAccess.ExitReadLock();
            }
        }

        internal IReadOnlyList<(TKey key, TResult result)> GetResultsUnderLock() => activeExpressions.Select(ae => (ae.Key, ae.Value.Value)).ToImmutableArray();

        public IReadOnlyList<(TKey key, TResult result, Exception fault)> GetResultsAndFaults()
        {
            activeExpressionsAccess.EnterReadLock();
            try
            {
                return GetResultsAndFaultsUnderLock();
            }
            finally
            {
                activeExpressionsAccess.ExitReadLock();
            }
        }

        internal IReadOnlyList<(TKey key, TResult result, Exception fault)> GetResultsAndFaultsUnderLock() => activeExpressions.Select(ae => (ae.Key, ae.Value.Value, ae.Value.Fault)).ToImmutableArray();

        void Initialize()
        {
            AddActiveExpressions(source);
            if (source is INotifyDictionaryChanged<TKey, TValue> dictionaryChangedNotifier)
                dictionaryChangedNotifier.DictionaryChanged += SourceDictionaryChanged;
            if (source is INotifyElementFaultChanges faultNotifier)
            {
                faultNotifier.ElementFaultChanged += SourceFaultChanged;
                faultNotifier.ElementFaultChanging += SourceFaultChanging;
            }
        }

        protected virtual void OnDictionaryChanged(NotifyDictionaryChangedEventArgs<TKey, TResult> e) =>
            DictionaryChanged?.Invoke(this, e);

        protected virtual void OnElementFaultChanged(ElementFaultChangeEventArgs e) =>
            ElementFaultChanged?.Invoke(this, e);

        protected void OnElementFaultChanged(TKey key, Exception fault) =>
            OnElementFaultChanged(new ElementFaultChangeEventArgs(key, fault));

        protected virtual void OnElementFaultChanging(ElementFaultChangeEventArgs e) =>
            ElementFaultChanging?.Invoke(this, e);

        protected void OnElementFaultChanging(TKey key, Exception fault) =>
            OnElementFaultChanging(new ElementFaultChangeEventArgs(key, fault));

        protected virtual void OnValueResultChanged(RangeActiveExpressionResultChangeEventArgs<TKey, TResult> e) =>
            ValueResultChanged?.Invoke(this, e);

        protected void OnValueResultChanged(TKey key, TResult result) =>
            OnValueResultChanged(new RangeActiveExpressionResultChangeEventArgs<TKey, TResult>(key, result));

        protected virtual void OnValueResultChanging(RangeActiveExpressionResultChangeEventArgs<TKey, TResult> e) =>
            ValueResultChanging?.Invoke(this, e);

        protected void OnValueResultChanging(TKey key, TResult result) =>
            OnValueResultChanging(new RangeActiveExpressionResultChangeEventArgs<TKey, TResult>(key, result));

        IReadOnlyList<KeyValuePair<TKey, TResult>> RemoveActiveExpressions(IReadOnlyList<TKey> keys)
        {
            List<KeyValuePair<TKey, TResult>> result = null;
            if ((keys?.Count ?? 0) > 0)
            {
                activeExpressionsAccess.EnterWriteLock();
                try
                {
                    result = RemoveActiveExpressionsUnderLock(keys);
                }
                finally
                {
                    activeExpressionsAccess.ExitWriteLock();
                }
            }
            return (result ?? Enumerable.Empty<KeyValuePair<TKey, TResult>>()).ToImmutableArray();
        }

        List<KeyValuePair<TKey, TResult>> RemoveActiveExpressionsUnderLock(IReadOnlyList<TKey> keys)
        {
            var result = new List<KeyValuePair<TKey, TResult>>();
            foreach (var key in keys)
            {
                var activeExpression = activeExpressions[key];
                result.Add(new KeyValuePair<TKey, TResult>(key, activeExpression.Value));
                activeExpressions.Remove(key);
                activeExpression.PropertyChanging -= ActiveExpressionPropertyChanging;
                activeExpression.PropertyChanged -= ActiveExpressionPropertyChanged;
                activeExpression.Dispose();
            }
            return result;
        }

        void SourceDictionaryChanged(object sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
        {
            switch (e.Action)
            {
                case NotifyDictionaryChangedAction.Add:
                    OnDictionaryChanged(new NotifyDictionaryChangedEventArgs<TKey, TResult>(NotifyDictionaryChangedAction.Add, AddActiveExpressions(e.NewItems)));
                    break;
                case NotifyDictionaryChangedAction.Remove:
                    OnDictionaryChanged(new NotifyDictionaryChangedEventArgs<TKey, TResult>(NotifyDictionaryChangedAction.Remove, RemoveActiveExpressions(e.OldItems.Select(oldItem => oldItem.Key).ToImmutableArray())));
                    break;
                case NotifyDictionaryChangedAction.Replace:
                    IReadOnlyList<KeyValuePair<TKey, TResult>> removed, added;
                    activeExpressionsAccess.EnterWriteLock();
                    try
                    {
                        removed = RemoveActiveExpressionsUnderLock(e.OldItems.Select(oldItem => oldItem.Key).ToImmutableArray());
                        added = AddActiveExpressionsUnderLock(e.NewItems).Select(ae => new KeyValuePair<TKey, TResult>(ae.Arg1, ae.Value)).ToImmutableArray();
                    }
                    finally
                    {
                        activeExpressionsAccess.ExitWriteLock();
                    }
                    OnDictionaryChanged(new NotifyDictionaryChangedEventArgs<TKey, TResult>(NotifyDictionaryChangedAction.Replace, added, removed));
                    break;
                default:
                    activeExpressionsAccess.EnterWriteLock();
                    try
                    {
                        RemoveActiveExpressionsUnderLock(activeExpressions.Keys.ToImmutableArray());
                        AddActiveExpressionsUnderLock(source);
                    }
                    finally
                    {
                        activeExpressionsAccess.ExitWriteLock();
                    }
                    OnDictionaryChanged(new NotifyDictionaryChangedEventArgs<TKey, TResult>(NotifyDictionaryChangedAction.Reset));
                    break;
            }
        }

        void SourceFaultChanged(object sender, ElementFaultChangeEventArgs e) => ElementFaultChanged?.Invoke(sender, e);

        void SourceFaultChanging(object sender, ElementFaultChangeEventArgs e) => ElementFaultChanging?.Invoke(sender, e);

        public ActiveExpressionOptions Options { get; }
    }
}
