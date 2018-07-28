using Gear.Components;
using Nito.AsyncEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;

namespace Gear.ActiveQuery
{
    public static class ActiveQueryExtensions
    {
        public static ActiveEnumerable<TResult> ActiveCast<TResult>(this IEnumerable source) =>
            ActiveCast<TResult>(source, (source as ISynchronizable)?.SynchronizationContext);

        public static ActiveEnumerable<TResult> ActiveCast<TResult>(this IEnumerable source, SynchronizationContext synchronizationContext)
        {
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TResult>(synchronizationContext, source.Cast<TResult>(), false);
            var rangeObservableCollectionAccess = new AsyncLock();

            async void notifyCollectionChangedEventHandler(object sender, NotifyCollectionChangedEventArgs e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    if (e.Action == NotifyCollectionChangedAction.Reset)
                        await rangeObservableCollection.ReplaceAllAsync(source.Cast<TResult>()).ConfigureAwait(false);
                    else if (e.Action == NotifyCollectionChangedAction.Move)
                        await rangeObservableCollection.MoveRangeAsync(e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count).ConfigureAwait(false);
                    else
                    {
                        if (e.OldItems != null && e.NewItems != null && e.OldStartingIndex >= 0 && e.OldStartingIndex == e.NewStartingIndex)
                        {
                            if (e.OldItems.Count == 1 && e.NewItems.Count == 1)
                                await rangeObservableCollection.ReplaceAsync(e.OldStartingIndex, (TResult)e.NewItems[0]).ConfigureAwait(false);
                            else
                                await rangeObservableCollection.ReplaceRangeAsync(e.OldStartingIndex, e.OldItems.Count, e.NewItems.Cast<TResult>()).ConfigureAwait(false);
                        }
                        else
                        {
                            if (e.OldItems != null && e.OldStartingIndex >= 0)
                                await rangeObservableCollection.RemoveRangeAsync(e.OldStartingIndex, e.OldItems.Count).ConfigureAwait(false);
                            if (e.NewItems != null && e.NewStartingIndex >= 0)
                                await rangeObservableCollection.InsertRangeAsync(e.NewStartingIndex, e.NewItems.Cast<TResult>()).ConfigureAwait(false);
                        }
                    }
                }
            }

            {
                if (source is INotifyCollectionChanged notifyingSource)
                    notifyingSource.CollectionChanged += notifyCollectionChangedEventHandler;
            }
            var result = new ActiveEnumerable<TResult>(rangeObservableCollection, disposing =>
            {
                if (source is INotifyCollectionChanged notifyingSource)
                    notifyingSource.CollectionChanged -= notifyCollectionChangedEventHandler;
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveEnumerable<TSource> ActiveConcat<TSource>(this IReadOnlyList<TSource> first, IReadOnlyList<TSource> second) =>
            ActiveConcat(first, second, (first as ISynchronizable)?.SynchronizationContext ?? (second as ISynchronizable)?.SynchronizationContext);

        public static ActiveEnumerable<TSource> ActiveConcat<TSource>(this IReadOnlyList<TSource> first, IReadOnlyList<TSource> second, SynchronizationContext synchronizationContext)
        {
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TSource>(synchronizationContext, first.Concat(second), false);
            var rangeObservableCollectionAccess = new AsyncLock();

            async void firstNotifyCollectionChangedEventHandler(object sender, NotifyCollectionChangedEventArgs e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    if (e.Action == NotifyCollectionChangedAction.Reset)
                        await rangeObservableCollection.ReplaceRangeAsync(0, (await rangeObservableCollection.CountAsync.ConfigureAwait(false)) - second.Count, first).ConfigureAwait(false);
                    else
                    {
                        if (e.OldItems != null && e.NewItems != null && e.OldStartingIndex >= 0 && e.OldStartingIndex == e.NewStartingIndex)
                        {
                            if (e.OldItems.Count == 1 && e.NewItems.Count == 1)
                                await rangeObservableCollection.ReplaceAsync(e.OldStartingIndex, (TSource)e.NewItems[0]).ConfigureAwait(false);
                            else
                                await rangeObservableCollection.ReplaceRangeAsync(e.OldStartingIndex, e.OldItems.Count, e.NewItems.Cast<TSource>()).ConfigureAwait(false);
                        }
                        else
                        {
                            if (e.OldItems != null && e.OldStartingIndex >= 0)
                                await rangeObservableCollection.RemoveRangeAsync(e.OldStartingIndex, e.OldItems.Count).ConfigureAwait(false);
                            if (e.NewItems != null && e.NewStartingIndex >= 0)
                                await rangeObservableCollection.InsertRangeAsync(e.NewStartingIndex, e.NewItems.Cast<TSource>()).ConfigureAwait(false);
                        }
                    }
                }
            }

            async void secondNotifyCollectionChangedEventHandler(object sender, NotifyCollectionChangedEventArgs e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    if (e.Action == NotifyCollectionChangedAction.Reset)
                        await rangeObservableCollection.ReplaceRangeAsync(first.Count, (await rangeObservableCollection.CountAsync.ConfigureAwait(false)) - first.Count, second).ConfigureAwait(false);
                    else
                    {
                        if (e.OldItems != null && e.NewItems != null && e.OldStartingIndex >= 0 && e.OldStartingIndex == e.NewStartingIndex)
                        {
                            if (e.OldItems.Count == 1 && e.NewItems.Count == 1)
                                await rangeObservableCollection.ReplaceAsync(first.Count + e.OldStartingIndex, (TSource)e.NewItems[0]).ConfigureAwait(false);
                            else
                                await rangeObservableCollection.ReplaceRangeAsync(first.Count + e.OldStartingIndex, e.OldItems.Count, e.NewItems.Cast<TSource>()).ConfigureAwait(false);
                        }
                        else
                        {
                            if (e.OldItems != null && e.OldStartingIndex >= 0)
                                await rangeObservableCollection.RemoveRangeAsync(first.Count + e.OldStartingIndex, e.OldItems.Count).ConfigureAwait(false);
                            if (e.NewItems != null && e.NewStartingIndex >= 0)
                                await rangeObservableCollection.InsertRangeAsync(first.Count + e.NewStartingIndex, e.NewItems.Cast<TSource>()).ConfigureAwait(false);
                        }
                    }
                }
            }

            {
                if (first is INotifyCollectionChanged notifyingFirst)
                    notifyingFirst.CollectionChanged += firstNotifyCollectionChangedEventHandler;
                if (second is INotifyCollectionChanged notifyingSecond)
                    notifyingSecond.CollectionChanged += secondNotifyCollectionChangedEventHandler;
            }
            var result = new ActiveEnumerable<TSource>(rangeObservableCollection, disposing =>
            {
                if (first is INotifyCollectionChanged notifyingFirst)
                    notifyingFirst.CollectionChanged -= firstNotifyCollectionChangedEventHandler;
                if (second is INotifyCollectionChanged notifyingSecond)
                    notifyingSecond.CollectionChanged -= secondNotifyCollectionChangedEventHandler;
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveEnumerable<TSource> ActiveDistinct<TSource>(this IReadOnlyList<TSource> source) =>
            ActiveDistinct(source, (source as ISynchronizable)?.SynchronizationContext);

        public static ActiveEnumerable<TSource> ActiveDistinct<TSource>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext)
        {
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TSource>(synchronizationContext, false);
            var rangeObservableCollectionAccess = new AsyncLock();
            var distinctCounts = new Dictionary<TSource, int>();
            foreach (var element in source)
            {
                if (distinctCounts.TryGetValue(element, out var distinctCount))
                    distinctCounts[element] = ++distinctCount;
                else
                {
                    distinctCounts.Add(element, 1);
                    rangeObservableCollection.Add(element);
                }
            }

            async void collectionChangedHandler(object sender, NotifyCollectionChangedEventArgs e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    if (e.Action == NotifyCollectionChangedAction.Reset)
                    {
                        distinctCounts.Clear();
                        await rangeObservableCollection.ClearAsync().ConfigureAwait(false);
                    }
                    else if (e.Action != NotifyCollectionChangedAction.Move)
                    {
                        if (e.OldItems != null && e.OldStartingIndex >= 0)
                        {
                            var removingResults = new List<TSource>();
                            foreach (TSource oldItem in e.OldItems)
                            {
                                if (--distinctCounts[oldItem] == 0)
                                {
                                    distinctCounts.Remove(oldItem);
                                    removingResults.Add(oldItem);
                                }
                            }
                            if (removingResults.Count > 0)
                                await rangeObservableCollection.RemoveRangeAsync(removingResults).ConfigureAwait(false);
                        }
                        if (e.NewItems != null && e.NewStartingIndex >= 0)
                        {
                            var addingResults = new List<TSource>();
                            foreach (TSource newItem in e.NewItems)
                            {
                                if (distinctCounts.TryGetValue(newItem, out var distinctCount))
                                    distinctCounts[newItem] = ++distinctCount;
                                else
                                {
                                    distinctCounts.Add(newItem, 1);
                                    addingResults.Add(newItem);
                                }
                            }
                            if (addingResults.Count > 0)
                                await rangeObservableCollection.AddRangeAsync(addingResults).ConfigureAwait(false);
                        }
                    }
                }
            }

            {
                if (source is INotifyCollectionChanged notifyingSource)
                    notifyingSource.CollectionChanged += collectionChangedHandler;
            }
            var result = new ActiveEnumerable<TSource>(rangeObservableCollection, disposing =>
            {
                if (source is INotifyCollectionChanged notifyingSource)
                    notifyingSource.CollectionChanged -= collectionChangedHandler;
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveEnumerable<ActiveGrouping<TKey, TSource>> ActiveGroupBy<TKey, TSource>(this IReadOnlyList<TSource> source, Func<TSource, TKey> keySelector, params string[] keySelectorProperties) where TKey : IEquatable<TKey> where TSource : class =>
            ActiveGroupBy(source, (source as ISynchronizable)?.SynchronizationContext, keySelector, keySelectorProperties);

        public static ActiveEnumerable<ActiveGrouping<TKey, TSource>> ActiveGroupBy<TKey, TSource>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, Func<TSource, TKey> keySelector, params string[] keySelectorProperties) where TKey : IEquatable<TKey> where TSource : class
        {
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<ActiveGrouping<TKey, TSource>>(synchronizationContext, false);
            var rangeObservableCollectionAccess = new AsyncLock();
            var collectionAndGroupingDictionary = new Dictionary<TKey, (SynchronizedRangeObservableCollection<TSource> groupingObservableCollection, ActiveGrouping<TKey, TSource> grouping)>();
            var keyDictionary = new Dictionary<TSource, TKey>();

            var monitor = ActiveListMonitor<TSource>.Monitor(source, keySelectorProperties);

            void addElement(TSource element)
            {
                var key = keySelector(element);
                if (!monitor.ElementsNotifyChanging)
                    keyDictionary.Add(element, key);
                SynchronizedRangeObservableCollection<TSource> groupingObservableCollection;
                if (!collectionAndGroupingDictionary.TryGetValue(key, out var collectionAndGrouping))
                {
                    groupingObservableCollection = new SynchronizedRangeObservableCollection<TSource>(synchronizationContext);
                    var grouping = new ActiveGrouping<TKey, TSource>(key, groupingObservableCollection);
                    collectionAndGrouping = (groupingObservableCollection, grouping);
                    rangeObservableCollection.Add(grouping);
                }
                else
                    groupingObservableCollection = collectionAndGrouping.groupingObservableCollection;
                groupingObservableCollection.Add(element);
            }

            foreach (var element in source)
                addElement(element);

            async void elementPropertyChangedHandler(object sender, ElementPropertyChangeEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var element = e.Element;
                    var oldKey = keyDictionary[element];
                    var newKey = keySelector(element);
                    if (!oldKey.Equals(newKey))
                    {
                        if (!monitor.ElementsNotifyChanging)
                            keyDictionary[element] = newKey;
                        var oldCollectionAndGrouping = collectionAndGroupingDictionary[oldKey];
                        await oldCollectionAndGrouping.groupingObservableCollection.RemoveAsync(element).ConfigureAwait(false);
                        if ((await oldCollectionAndGrouping.groupingObservableCollection.CountAsync.ConfigureAwait(false)) == 0)
                            collectionAndGroupingDictionary.Remove(oldKey);
                        SynchronizedRangeObservableCollection<TSource> groupingObservableCollection;
                        if (!collectionAndGroupingDictionary.TryGetValue(newKey, out var collectionAndGrouping))
                        {
                            groupingObservableCollection = new SynchronizedRangeObservableCollection<TSource>(synchronizationContext);
                            var grouping = new ActiveGrouping<TKey, TSource>(newKey, groupingObservableCollection);
                            collectionAndGrouping = (groupingObservableCollection, grouping);
                            await rangeObservableCollection.AddAsync(grouping).ConfigureAwait(false);
                        }
                        else
                            groupingObservableCollection = collectionAndGrouping.groupingObservableCollection;
                        await groupingObservableCollection.AddAsync(element).ConfigureAwait(false);
                    }
                    if (monitor.ElementsNotifyChanging)
                        keyDictionary.Remove(element);
                }
            }

            void elementPropertyChangingHandler(object sender, ElementPropertyChangeEventArgs<TSource> e)
            {
                var element = e.Element;
                var key = keySelector(element);
                if (keyDictionary.ContainsKey(element))
                    keyDictionary[element] = key;
                else
                    keyDictionary.Add(element, key);
            }

            async void elementsAddedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    foreach (var element in e.Elements)
                        addElement(element);
                }
            }

            async void elementsRemovedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                    foreach (var element in e.Elements)
                    {
                        var key = keyDictionary[element];
                        keyDictionary.Remove(element);
                        var (groupingObservableCollection, grouping) = collectionAndGroupingDictionary[key];
                        await groupingObservableCollection.RemoveAsync(element).ConfigureAwait(false);
                        if ((await groupingObservableCollection.CountAsync.ConfigureAwait(false)) == 0)
                            collectionAndGroupingDictionary.Remove(key);
                    }
            }

            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            var result = new ActiveEnumerable<ActiveGrouping<TKey, TSource>>(rangeObservableCollection, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveAggregateValue<TResult> ActiveMax<TKey, TValue, TResult>(this IReadOnlyDictionary<TKey, TValue> source, Func<TKey, TValue, TResult> selector, params string[] selectorProperties) where TResult : IComparable<TResult>
        {
            var selectorValues = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, TResult>)new SortedDictionary<TKey, TResult>() : new Dictionary<TKey, TResult>();
            var firstIsValid = false;
            TResult firstMax = default;
            if (source.Count > 0)
            {
                firstIsValid = true;
                var firstKeyValuePair = source.First();
                var firstKey = firstKeyValuePair.Key;
                firstMax = selector(firstKey, firstKeyValuePair.Value);
                selectorValues.Add(firstKey, firstMax);
                foreach (var keyValuePair in source.Skip(1))
                {
                    var key = keyValuePair.Key;
                    var selectorValue = selector(key, keyValuePair.Value);
                    if (selectorValue.CompareTo(firstMax) > 0)
                        firstMax = selectorValue;
                    selectorValues.Add(key, selectorValue);
                }
            }
            var monitor = ActiveDictionaryMonitor<TKey, TValue>.Monitor(source, selectorProperties);
            EventHandler<NotifyDictionaryValueEventArgs<TKey, TValue>> valueAdded = null;
            EventHandler<ValuePropertyChangeEventArgs<TKey, TValue>> valuePropertyChanged = null;
            EventHandler<NotifyDictionaryValueEventArgs<TKey, TValue>> valueRemoved = null;
            EventHandler<NotifyDictionaryValueReplacedEventArgs<TKey, TValue>> valueReplaced = null;
            EventHandler<NotifyDictionaryValuesEventArgs<TKey, TValue>> valuesAdded = null;
            EventHandler<NotifyDictionaryValuesEventArgs<TKey, TValue>> valuesRemoved = null;
            var result = new ActiveAggregateValue<TResult>(firstIsValid, firstMax, out var setValidity, out var setValue, disposing =>
            {
                monitor.ValueAdded -= valueAdded;
                monitor.ValuePropertyChanged -= valuePropertyChanged;
                monitor.ValueRemoved -= valueRemoved;
                monitor.ValueReplaced -= valueReplaced;
                monitor.ValuesAdded -= valuesAdded;
                monitor.ValuesRemoved -= valuesRemoved;
                if (disposing)
                    monitor.Dispose();
            });
            valueAdded = (sender, e) =>
            {
                var key = e.Key;
                if (selectorValues.Count == 0)
                {
                    var currentMax = selector(key, e.Value);
                    selectorValues.Add(key, currentMax);
                    setValidity(true);
                    setValue(currentMax);
                }
                else
                {
                    var selectorValue = selector(key, e.Value);
                    selectorValues.Add(key, selectorValue);
                    if (selectorValue.CompareTo(result.Value) > 0)
                        setValue(selectorValue);
                }
            };
            valuePropertyChanged = (sender, e) =>
            {
                var key = e.Key;
                var previousSelectorValue = selectorValues[key];
                var newSelectorValue = selector(key, e.Value);
                selectorValues[key] = newSelectorValue;
                var currentMax = result.Value;
                var comparison = newSelectorValue.CompareTo(currentMax);
                if (comparison > 0)
                    setValue(newSelectorValue);
                else if (comparison < 0 && previousSelectorValue.CompareTo(currentMax) == 0)
                    setValue(selectorValues.Values.Max());
            };
            valueRemoved = (sender, e) =>
            {
                if (selectorValues.Count == 1)
                {
                    selectorValues = source is SortedDictionary<TKey, TResult> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, TResult>)new SortedDictionary<TKey, TResult>() : (IDictionary<TKey, TResult>)new Dictionary<TKey, TResult>();
                    setValidity(false);
                }
                else
                {
                    var key = e.Key;
                    if (selectorValues.TryGetValue(key, out var selectorValue))
                    {
                        selectorValues.Remove(key);
                        if (selectorValue.CompareTo(result.Value) == 0)
                            setValue(selectorValues.Values.Max());
                    }
                }
            };
            valueReplaced = (sender, e) =>
            {
                var key = e.Key;
                var previousSelectorValue = selectorValues[key];
                var currentSelectorValue = selector(key, e.NewValue);
                selectorValues[key] = currentSelectorValue;
                var previousToCurrent = previousSelectorValue.CompareTo(currentSelectorValue);
                if (previousToCurrent < 0 && currentSelectorValue.CompareTo(result.Value) > 0)
                    setValue(currentSelectorValue);
                else if (previousToCurrent > 0 && previousSelectorValue.CompareTo(result.Value) == 0)
                    setValue(selectorValues.Values.Max());
            };
            valuesAdded = (sender, e) =>
            {
                if (selectorValues.Count == 0)
                {
                    var firstKeyValuePair = e.KeyValuePairs.First();
                    var currentMax = selector(firstKeyValuePair.Key, firstKeyValuePair.Value);
                    selectorValues.Add(firstKeyValuePair.Key, currentMax);
                    foreach (var keyValuePair in e.KeyValuePairs.Skip(1))
                    {
                        var key = keyValuePair.Key;
                        var selectorValue = selector(key, keyValuePair.Value);
                        if (selectorValue.CompareTo(currentMax) > 0)
                            currentMax = selectorValue;
                        selectorValues.Add(key, selectorValue);
                    }
                    setValidity(true);
                    setValue(currentMax);
                }
                else
                {
                    var currentMax = result.Value;
                    foreach (var keyValuePair in e.KeyValuePairs)
                    {
                        var key = keyValuePair.Key;
                        var selectorValue = selector(key, keyValuePair.Value);
                        selectorValues.Add(key, selectorValue);
                        if (selectorValue.CompareTo(currentMax) > 0)
                            currentMax = selectorValue;
                    }
                    if (currentMax.CompareTo(result.Value) > 0)
                        setValue(currentMax);
                }
            };
            valuesRemoved = (sender, e) =>
            {
                if (selectorValues.Count == e.KeyValuePairs.Count)
                {
                    selectorValues = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, TResult>)new SortedDictionary<TKey, TResult>() : (IDictionary<TKey, TResult>)new Dictionary<TKey, TResult>();
                    setValidity(false);
                }
                else
                {
                    var currentMax = result.Value;
                    var maxRemoved = false;
                    foreach (var keyValuePair in e.KeyValuePairs)
                    {
                        var key = keyValuePair.Key;
                        if (selectorValues.TryGetValue(key, out var selectorValue))
                        {
                            if (selectorValue.CompareTo(currentMax) == 0)
                                maxRemoved = true;
                            selectorValues.Remove(key);
                        }
                    }
                    if (maxRemoved)
                        setValue(selectorValues.Values.Max());
                }
            };
            monitor.ValueAdded += valueAdded;
            monitor.ValuePropertyChanged += valuePropertyChanged;
            monitor.ValueRemoved += valueRemoved;
            monitor.ValueReplaced += valueReplaced;
            monitor.ValuesAdded += valuesAdded;
            monitor.ValuesRemoved += valuesRemoved;
            return result;
        }

        public static ActiveAggregateValue<TResult?> ActiveMax<TKey, TValue, TResult>(this IReadOnlyDictionary<TKey, TValue> source, Func<TKey, TValue, TResult?> selector, params string[] selectorProperties) where TResult : struct, IComparable<TResult>
        {
            var selectorValues = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, TResult?>)new SortedDictionary<TKey, TResult?>() : new Dictionary<TKey, TResult?>();
            var firstIsValid = false;
            TResult? firstMax = default;
            if (source.Count > 0)
            {
                firstIsValid = true;
                var firstKeyValuePair = source.First();
                var firstKey = firstKeyValuePair.Key;
                firstMax = selector(firstKey, firstKeyValuePair.Value);
                selectorValues.Add(firstKey, firstMax);
                foreach (var keyValuePair in source.Skip(1))
                {
                    var key = keyValuePair.Key;
                    var selectorValue = selector(key, keyValuePair.Value);
                    if (selectorValue != null && (firstMax == null || selectorValue.Value.CompareTo(firstMax.Value) > 0))
                        firstMax = selectorValue;
                    selectorValues.Add(key, selectorValue);
                }
            }
            var monitor = ActiveDictionaryMonitor<TKey, TValue>.Monitor(source, selectorProperties);
            EventHandler<NotifyDictionaryValueEventArgs<TKey, TValue>> valueAdded = null;
            EventHandler<ValuePropertyChangeEventArgs<TKey, TValue>> valuePropertyChanged = null;
            EventHandler<NotifyDictionaryValueEventArgs<TKey, TValue>> valueRemoved = null;
            EventHandler<NotifyDictionaryValueReplacedEventArgs<TKey, TValue>> valueReplaced = null;
            EventHandler<NotifyDictionaryValuesEventArgs<TKey, TValue>> valuesAdded = null;
            EventHandler<NotifyDictionaryValuesEventArgs<TKey, TValue>> valuesRemoved = null;
            var result = new ActiveAggregateValue<TResult?>(firstIsValid, firstMax, out var setValidity, out var setValue, disposing =>
            {
                monitor.ValueAdded -= valueAdded;
                monitor.ValuePropertyChanged -= valuePropertyChanged;
                monitor.ValueRemoved -= valueRemoved;
                monitor.ValueReplaced -= valueReplaced;
                monitor.ValuesAdded -= valuesAdded;
                monitor.ValuesRemoved -= valuesRemoved;
                if (disposing)
                    monitor.Dispose();
            });
            valueAdded = (sender, e) =>
            {
                var key = e.Key;
                if (selectorValues.Count == 0)
                {
                    var selectorValue = selector(key, e.Value);
                    selectorValues.Add(key, selectorValue);
                    setValidity(true);
                    setValue(selectorValue);
                }
                else
                {
                    var currentMax = result.Value;
                    var selectorValue = selector(key, e.Value);
                    selectorValues.Add(key, selectorValue);
                    if (selectorValue != null && (currentMax == null || selectorValue.Value.CompareTo(currentMax.Value) > 0))
                        currentMax = selectorValue;
                    if (((currentMax == null) != (result.Value == null)) || (currentMax != null && result.Value != null && currentMax.Value.CompareTo(result.Value.Value) > 0))
                        setValue(currentMax);
                }
            };
            valuePropertyChanged = (sender, e) =>
            {
                var key = e.Key;
                var previousSelectorValue = selectorValues[key];
                var newSelectorValue = selector(key, e.Value);
                selectorValues[key] = newSelectorValue;
                var currentMax = result.Value;
                if (newSelectorValue != null && (currentMax == null || newSelectorValue.Value.CompareTo(currentMax.Value) > 0))
                    setValue(newSelectorValue);
                else if (previousSelectorValue != null && currentMax != null && previousSelectorValue.Value.CompareTo(currentMax.Value) == 0 && (newSelectorValue == null || newSelectorValue.Value.CompareTo(previousSelectorValue.Value) < 0))
                    setValue(selectorValues.Values.Max());
            };
            valueRemoved = (sender, e) =>
            {
                if (selectorValues.Count == 1)
                {
                    selectorValues = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, TResult?>)new SortedDictionary<TKey, TResult?>() : new Dictionary<TKey, TResult?>();
                    setValidity(false);
                }
                else
                {
                    var currentMax = result.Value;
                    var maxRemoved = false;
                    var key = e.Key;
                    if (selectorValues.TryGetValue(key, out var selectorValue))
                    {
                        if (currentMax != null && selectorValue != null && selectorValue.Value.CompareTo(currentMax.Value) == 0)
                            maxRemoved = true;
                        selectorValues.Remove(key);
                    }
                    if (maxRemoved)
                        setValue(selectorValues.Values.Max());
                }
            };
            valueReplaced = (sender, e) =>
            {
                var key = e.Key;
                var previousSelectorValue = selectorValues[key];
                var currentSelectorValue = selector(key, e.NewValue);
                selectorValues[key] = currentSelectorValue;
                var previousToCurrent = (previousSelectorValue ?? default).CompareTo(currentSelectorValue ?? default);
                if (currentSelectorValue != null && (previousSelectorValue == null || previousToCurrent < 0) && (result.Value == null || currentSelectorValue.Value.CompareTo(result.Value.Value) > 0))
                    setValue(currentSelectorValue);
                else if (previousSelectorValue != null && (currentSelectorValue == null || (previousToCurrent > 0 && previousSelectorValue.Value.CompareTo(result.Value.Value) == 0)))
                    setValue(selectorValues.Values.Max());
            };
            valuesAdded = (sender, e) =>
            {
                if (selectorValues.Count == 0)
                {
                    var firstKeyValuePair = source.First();
                    var firstKey = firstKeyValuePair.Key;
                    var currentMax = selector(firstKey, firstKeyValuePair.Value);
                    selectorValues.Add(firstKey, currentMax);
                    foreach (var keyValuePair in source.Skip(1))
                    {
                        var key = keyValuePair.Key;
                        var selectorValue = selector(key, keyValuePair.Value);
                        if (selectorValue != null && (currentMax == null || selectorValue.Value.CompareTo(currentMax.Value) > 0))
                            currentMax = selectorValue;
                        selectorValues.Add(key, selectorValue);
                    }
                    setValidity(true);
                    setValue(currentMax);
                }
                else
                {
                    var currentMax = result.Value;
                    foreach (var keyValuePair in e.KeyValuePairs)
                    {
                        var key = keyValuePair.Key;
                        var selectorValue = selector(key, keyValuePair.Value);
                        selectorValues.Add(key, selectorValue);
                        if (selectorValue != null && (currentMax == null || selectorValue.Value.CompareTo(currentMax.Value) > 0))
                            currentMax = selectorValue;
                    }
                    if (((currentMax == null) != (result.Value == null)) || (currentMax != null && result.Value != null && currentMax.Value.CompareTo(result.Value.Value) > 0))
                        setValue(currentMax);
                }
            };
            valuesRemoved = (sender, e) =>
            {
                if (selectorValues.Count == e.KeyValuePairs.Count)
                {
                    selectorValues = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, TResult?>)new SortedDictionary<TKey, TResult?>() : new Dictionary<TKey, TResult?>();
                    setValidity(false);
                }
                else
                {
                    var currentMax = result.Value;
                    var maxRemoved = false;
                    foreach (var keyValuePair in e.KeyValuePairs)
                    {
                        var key = keyValuePair.Key;
                        if (selectorValues.TryGetValue(key, out var selectorValue))
                        {
                            if (currentMax != null && selectorValue != null && selectorValue.Value.CompareTo(currentMax.Value) == 0)
                                maxRemoved = true;
                            selectorValues.Remove(key);
                        }
                    }
                    if (maxRemoved)
                        setValue(selectorValues.Values.Max());
                }
            };
            monitor.ValueAdded += valueAdded;
            monitor.ValuePropertyChanged += valuePropertyChanged;
            monitor.ValueRemoved += valueRemoved;
            monitor.ValueReplaced += valueReplaced;
            monitor.ValuesAdded += valuesAdded;
            monitor.ValuesRemoved += valuesRemoved;
            return result;
        }

        public static ActiveAggregateValue<TResult> ActiveMax<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult> selector, params string[] selectorProperties) where TSource : class where TResult : IComparable<TResult>
        {
            var selectorValues = new Dictionary<object, TResult>();
            var firstIsValid = false;
            TResult firstMax = default;
            if (source.Count > 0)
            {
                firstIsValid = true;
                var firstElement = source.First();
                firstMax = selector(firstElement);
                selectorValues.Add(firstElement, firstMax);
                foreach (var element in source.Skip(1))
                {
                    var selectorValue = selector(element);
                    if (selectorValue.CompareTo(firstMax) > 0)
                        firstMax = selectorValue;
                    selectorValues.Add(element, selectorValue);
                }
            }
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChanged = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAdded = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemoved = null;
            var result = new ActiveAggregateValue<TResult>(firstIsValid, firstMax, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChanged;
                monitor.ElementsAdded -= elementsAdded;
                monitor.ElementsRemoved -= elementsRemoved;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChanged = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                selectorValues[element] = newSelectorValue;
                var currentMax = result.Value;
                var comparison = newSelectorValue.CompareTo(currentMax);
                if (comparison > 0)
                    setValue(newSelectorValue);
                else if (comparison < 0 && previousSelectorValue.CompareTo(currentMax) == 0)
                    setValue(selectorValues.Values.Max());
            };
            elementsAdded = (sender, e) =>
            {
                if (selectorValues.Count == 0)
                {
                    var firstElement = e.Elements.First();
                    var currentMax = selector(firstElement);
                    selectorValues.Add(firstElement, currentMax);
                    foreach (var element in e.Elements.Skip(1))
                    {
                        var selectorValue = selector(element);
                        if (selectorValue.CompareTo(currentMax) > 0)
                            currentMax = selectorValue;
                        selectorValues.Add(element, selectorValue);
                    }
                    setValidity(true);
                    setValue(currentMax);
                }
                else
                {
                    var currentMax = result.Value;
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        if (selectorValue.CompareTo(currentMax) > 0)
                            currentMax = selectorValue;
                    }
                    if (currentMax.CompareTo(result.Value) > 0)
                        setValue(currentMax);
                }
            };
            elementsRemoved = (sender, e) =>
            {
                if (selectorValues.Count == e.Count)
                {
                    selectorValues = new Dictionary<object, TResult>();
                    setValidity(false);
                }
                else
                {
                    var currentMax = result.Value;
                    var maxRemoved = false;
                    foreach (var element in e.Elements)
                    {
                        if (selectorValues.TryGetValue(element, out var selectorValue))
                        {
                            if (selectorValue.CompareTo(currentMax) == 0)
                                maxRemoved = true;
                            selectorValues.Remove(element);
                        }
                    }
                    if (maxRemoved)
                        setValue(selectorValues.Values.Max());
                }
            };
            monitor.ElementPropertyChanged += elementPropertyChanged;
            monitor.ElementsAdded += elementsAdded;
            monitor.ElementsRemoved += elementsRemoved;
            return result;
        }

        public static ActiveAggregateValue<TResult?> ActiveMax<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult?> selector, params string[] selectorProperties) where TSource : class where TResult : struct, IComparable<TResult>
        {
            var selectorValues = new Dictionary<object, TResult?>();
            var firstIsValid = false;
            TResult? firstMax = default;
            if (source.Count > 0)
            {
                firstIsValid = true;
                var firstElement = source.First();
                firstMax = selector(firstElement);
                selectorValues.Add(firstElement, firstMax);
                foreach (var element in source.Skip(1))
                {
                    var selectorValue = selector(element);
                    if (selectorValue != null && (firstMax == null || selectorValue.Value.CompareTo(firstMax.Value) > 0))
                        firstMax = selectorValue;
                    selectorValues.Add(element, selectorValue);
                }
            }
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChanged = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAdded = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemoved = null;
            var result = new ActiveAggregateValue<TResult?>(firstIsValid, firstMax, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChanged;
                monitor.ElementsAdded -= elementsAdded;
                monitor.ElementsRemoved -= elementsRemoved;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChanged = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                selectorValues[element] = newSelectorValue;
                var currentMax = result.Value;
                if (newSelectorValue != null && (currentMax == null || newSelectorValue.Value.CompareTo(currentMax.Value) > 0))
                    setValue(newSelectorValue);
                else if (previousSelectorValue != null && currentMax != null && previousSelectorValue.Value.CompareTo(currentMax.Value) == 0 && (newSelectorValue == null || newSelectorValue.Value.CompareTo(previousSelectorValue.Value) < 0))
                    setValue(selectorValues.Values.Max());
            };
            elementsAdded = (sender, e) =>
            {
                if (selectorValues.Count == 0)
                {
                    var firstElement = source.First();
                    var currentMax = selector(firstElement);
                    selectorValues.Add(firstElement, currentMax);
                    foreach (var element in source.Skip(1))
                    {
                        var selectorValue = selector(element);
                        if (selectorValue != null && (currentMax == null || selectorValue.Value.CompareTo(currentMax.Value) > 0))
                            currentMax = selectorValue;
                        selectorValues.Add(element, selectorValue);
                    }
                    setValidity(true);
                    setValue(currentMax);
                }
                else
                {
                    var currentMax = result.Value;
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        if (selectorValue != null && (currentMax == null || selectorValue.Value.CompareTo(currentMax.Value) > 0))
                            currentMax = selectorValue;
                    }
                    if (((currentMax == null) != (result.Value == null)) || (currentMax != null && result.Value != null && currentMax.Value.CompareTo(result.Value.Value) > 0))
                        setValue(currentMax);
                }
            };
            elementsRemoved = (sender, e) =>
            {
                if (selectorValues.Count == e.Count)
                {
                    selectorValues = new Dictionary<object, TResult?>();
                    setValidity(false);
                }
                else
                {
                    var currentMax = result.Value;
                    var maxRemoved = false;
                    foreach (var element in e.Elements)
                    {
                        if (selectorValues.TryGetValue(element, out var selectorValue))
                        {
                            if (currentMax != null && selectorValue != null && selectorValue.Value.CompareTo(currentMax.Value) == 0)
                                maxRemoved = true;
                            selectorValues.Remove(element);
                        }
                    }
                    if (maxRemoved)
                        setValue(selectorValues.Values.Max());
                }
            };
            monitor.ElementPropertyChanged += elementPropertyChanged;
            monitor.ElementsAdded += elementsAdded;
            monitor.ElementsRemoved += elementsRemoved;
            return result;
        }

        public static ActiveAggregateValue<TResult> ActiveMin<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult> selector, params string[] selectorProperties) where TSource : class where TResult : IComparable<TResult>
        {
            var selectorValues = new Dictionary<object, TResult>();
            var firstIsValid = false;
            TResult firstMin = default;
            if (source.Count > 0)
            {
                firstIsValid = true;
                var firstElement = source.First();
                firstMin = selector(firstElement);
                selectorValues.Add(firstElement, firstMin);
                foreach (var element in source.Skip(1))
                {
                    var selectorValue = selector(element);
                    if (selectorValue.CompareTo(firstMin) < 0)
                        firstMin = selectorValue;
                    selectorValues.Add(element, selectorValue);
                }
            }
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChanged = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAdded = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemoved = null;
            var result = new ActiveAggregateValue<TResult>(firstIsValid, firstMin, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChanged;
                monitor.ElementsAdded -= elementsAdded;
                monitor.ElementsRemoved -= elementsRemoved;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChanged = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                selectorValues[element] = newSelectorValue;
                var currentMin = result.Value;
                var comparison = newSelectorValue.CompareTo(currentMin);
                if (comparison < 0)
                    setValue(newSelectorValue);
                else if (comparison > 0 && previousSelectorValue.CompareTo(currentMin) == 0)
                    setValue(selectorValues.Values.Min());
            };
            elementsAdded = (sender, e) =>
            {
                if (selectorValues.Count == 0)
                {
                    var firstElement = e.Elements.First();
                    var currentMin = selector(firstElement);
                    selectorValues.Add(firstElement, currentMin);
                    foreach (var element in e.Elements.Skip(1))
                    {
                        var selectorValue = selector(element);
                        if (selectorValue.CompareTo(currentMin) < 0)
                            currentMin = selectorValue;
                        selectorValues.Add(element, selectorValue);
                    }
                    setValidity(true);
                    setValue(currentMin);
                }
                else
                {
                    var currentMin = result.Value;
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        if (selectorValue.CompareTo(currentMin) < 0)
                            currentMin = selectorValue;
                    }
                    if (currentMin.CompareTo(result.Value) < 0)
                        setValue(currentMin);
                }
            };
            elementsRemoved = (sender, e) =>
            {
                if (selectorValues.Count == e.Count)
                {
                    selectorValues = new Dictionary<object, TResult>();
                    setValidity(false);
                }
                else
                {
                    var currentMin = result.Value;
                    var minRemoved = false;
                    foreach (var element in e.Elements)
                    {
                        if (selectorValues.TryGetValue(element, out var selectorValue))
                        {
                            if (selectorValue.CompareTo(currentMin) == 0)
                                minRemoved = true;
                            selectorValues.Remove(element);
                        }
                    }
                    if (minRemoved)
                        setValue(selectorValues.Values.Min());
                }
            };
            monitor.ElementPropertyChanged += elementPropertyChanged;
            monitor.ElementsAdded += elementsAdded;
            monitor.ElementsRemoved += elementsRemoved;
            return result;
        }

        public static ActiveAggregateValue<TResult?> ActiveMin<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult?> selector, params string[] selectorProperties) where TSource : class where TResult : struct, IComparable<TResult>
        {
            var selectorValues = new Dictionary<object, TResult?>();
            var firstIsValid = false;
            TResult? firstMin = default;
            if (source.Count > 0)
            {
                firstIsValid = true;
                var firstElement = source.First();
                firstMin = selector(firstElement);
                selectorValues.Add(firstElement, firstMin);
                foreach (var element in source.Skip(1))
                {
                    var selectorValue = selector(element);
                    if (selectorValue != null && (firstMin == null || selectorValue.Value.CompareTo(firstMin.Value) < 0))
                        firstMin = selectorValue;
                    selectorValues.Add(element, selectorValue);
                }
            }
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChanged = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAdded = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemoved = null;
            var result = new ActiveAggregateValue<TResult?>(firstIsValid, firstMin, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChanged;
                monitor.ElementsAdded -= elementsAdded;
                monitor.ElementsRemoved -= elementsRemoved;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChanged = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                selectorValues[element] = newSelectorValue;
                var currentMin = result.Value;
                if (newSelectorValue != null && (currentMin == null || newSelectorValue.Value.CompareTo(currentMin.Value) < 0))
                    setValue(newSelectorValue);
                else if (previousSelectorValue != null && currentMin != null && previousSelectorValue.Value.CompareTo(currentMin.Value) == 0 && (newSelectorValue == null || newSelectorValue.Value.CompareTo(previousSelectorValue.Value) > 0))
                    setValue(selectorValues.Values.Min());
            };
            elementsAdded = (sender, e) =>
            {
                if (selectorValues.Count == 0)
                {
                    var firstElement = e.Elements.First();
                    var currentMin = selector(firstElement);
                    selectorValues.Add(firstElement, currentMin);
                    foreach (var element in e.Elements.Skip(1))
                    {
                        var selectorValue = selector(element);
                        if (selectorValue != null && (currentMin == null || selectorValue.Value.CompareTo(currentMin.Value) < 0))
                            currentMin = selectorValue;
                        selectorValues.Add(element, selectorValue);
                    }
                    setValidity(true);
                    setValue(currentMin);
                }
                else
                {
                    var currentMin = result.Value;
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        if (selectorValue != null && (currentMin == null || selectorValue.Value.CompareTo(currentMin.Value) < 0))
                            currentMin = selectorValue;
                    }
                    if (((currentMin == null) != (result.Value == null)) || (currentMin != null && result.Value != null && currentMin.Value.CompareTo(result.Value.Value) != 0))
                        setValue(currentMin);
                }
            };
            elementsRemoved = (sender, e) =>
            {
                if (selectorValues.Count == e.Count)
                {
                    selectorValues = new Dictionary<object, TResult?>();
                    setValidity(false);
                }
                else
                {
                    var currentMin = result.Value;
                    var minRemoved = false;
                    foreach (var element in e.Elements)
                    {
                        if (selectorValues.TryGetValue(element, out var selectorValue))
                        {
                            if (currentMin != null && selectorValue != null && selectorValue.Value.CompareTo(currentMin.Value) == 0)
                                minRemoved = true;
                            selectorValues.Remove(element);
                        }
                    }
                    if (minRemoved)
                        setValue(selectorValues.Values.Min());
                }
            };
            monitor.ElementPropertyChanged += elementPropertyChanged;
            monitor.ElementsAdded += elementsAdded;
            monitor.ElementsRemoved += elementsRemoved;
            return result;
        }

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, Func<TSource, IComparable> ascendingOrderSelector, IEnumerable<string> ascendingSelectorProperties = null, bool indexed = false) where TSource : class =>
            ActiveOrderBy(source, (source as ISynchronizable)?.SynchronizationContext, ascendingOrderSelector, ascendingSelectorProperties, indexed);

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, Func<TSource, IComparable> ascendingOrderSelector, IEnumerable<string> ascendingSelectorProperties = null, bool indexed = false) where TSource : class =>
            ActiveOrderBy(source, synchronizationContext, new Func<TSource, IComparable>[] { ascendingOrderSelector }, ascendingSelectorProperties, indexed);

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, IEnumerable<Func<TSource, IComparable>> ascendingOrderSelectors, IEnumerable<string> ascendingSelectorsProperties = null, bool indexed = false) where TSource : class =>
            ActiveOrderBy(source, (source as ISynchronizable)?.SynchronizationContext, ascendingOrderSelectors, ascendingSelectorsProperties, indexed);

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, IEnumerable<Func<TSource, IComparable>> ascendingOrderSelectors, IEnumerable<string> ascendingSelectorsProperties = null, bool indexed = false) where TSource : class =>
            ActiveOrderBy(source, synchronizationContext, ascendingOrderSelectors.Select(aos => new ActiveOrderingDescriptor<TSource>(aos, false)), ascendingSelectorsProperties, indexed);

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, ActiveOrderingDescriptor<TSource> orderingDescriptor, IEnumerable<string> selectorProperties = null, bool indexed = false) where TSource : class =>
            ActiveOrderBy(source, (source as ISynchronizable)?.SynchronizationContext, orderingDescriptor, selectorProperties, indexed);

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, ActiveOrderingDescriptor<TSource> orderingDescriptor, IEnumerable<string> selectorProperties = null, bool indexed = false) where TSource : class =>
            ActiveOrderBy(source, synchronizationContext, new ActiveOrderingDescriptor<TSource>[] { orderingDescriptor }, selectorProperties, indexed);

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, IEnumerable<ActiveOrderingDescriptor<TSource>> orderingDescriptors, IEnumerable<string> selectorsProperties = null, bool indexed = false) where TSource : class =>
            ActiveOrderBy(source, (source as ISynchronizable)?.SynchronizationContext, orderingDescriptors, selectorsProperties, indexed);

        public static ActiveEnumerable<TSource> ActiveOrderBy<TSource>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, IEnumerable<ActiveOrderingDescriptor<TSource>> orderingDescriptors, IEnumerable<string> selectorsProperties = null, bool indexed = false) where TSource : class
        {
            var comparer = new ActiveOrderingComparer<TSource>(orderingDescriptors);
            var sortedSource = source.ToList();
            sortedSource.Sort(comparer);
            Dictionary<TSource, int> sortingIndicies = null;

            void rebuildSortingIndicies(IReadOnlyList<TSource> fromSort)
            {
                sortingIndicies = new Dictionary<TSource, int>();
                for (var i = 0; i < fromSort.Count; ++i)
                    sortingIndicies.Add(fromSort[i], i);
            }

            if (indexed)
                rebuildSortingIndicies(sortedSource);
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TSource>(synchronizationContext, sortedSource, false);
            var rangeObservableCollectionAccess = new AsyncLock();

            void repositionElement(TSource element)
            {
                var startingPosition = indexed ? sortingIndicies[element] : rangeObservableCollection.IndexOf(element);
                var position = startingPosition;
                if (indexed)
                {
                    while (position > 0 && comparer.Compare(element, rangeObservableCollection[position - 1]) < 0)
                        ++sortingIndicies[rangeObservableCollection[--position]];
                    while (position < rangeObservableCollection.Count - 1 && comparer.Compare(element, rangeObservableCollection[position + 1]) > 0)
                        --sortingIndicies[rangeObservableCollection[++position]];
                }
                else
                {
                    while (position > 0 && comparer.Compare(element, rangeObservableCollection[position - 1]) < 0)
                        --position;
                    while (position < rangeObservableCollection.Count - 1 && comparer.Compare(element, rangeObservableCollection[position + 1]) > 0)
                        ++position;
                }
                if (startingPosition != position)
                {
                    if (indexed)
                        sortingIndicies[element] = position;
                    rangeObservableCollection.Move(startingPosition, position);
                }
            }

            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorsProperties == null ? new string[0] : selectorsProperties.ToArray());

            async void elementPropertyChangedHandler(object sender, ElementPropertyChangeEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableCollection.ExecuteAsync(() => repositionElement(e.Element)).ConfigureAwait(false);
            }

            async void elementsAddedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableCollection.ExecuteAsync(() =>
                    {
                        {
                            if (rangeObservableCollection.Count == 0)
                            {
                                var sorted = e.Elements.ToList();
                                sorted.Sort(comparer);
                                if (indexed)
                                    rebuildSortingIndicies(sorted);
                                rangeObservableCollection.Reset(sorted);
                            }
                            else
                                foreach (var element in e.Elements)
                                {
                                    var position = 0;
                                    while (position < rangeObservableCollection.Count && comparer.Compare(element, rangeObservableCollection[position]) >= 0)
                                        ++position;
                                    var insertionPosition = position;
                                    if (indexed)
                                    {
                                        while (position < rangeObservableCollection.Count)
                                        {
                                            sortingIndicies[rangeObservableCollection[position]] = position + 1;
                                            ++position;
                                        }
                                        sortingIndicies.Add(element, insertionPosition);
                                    }
                                    rangeObservableCollection.Insert(insertionPosition, element);
                                }
                        }
                    }).ConfigureAwait(false);
            }

            async void elementsRemovedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableCollection.ExecuteAsync(() =>
                    {
                        if (rangeObservableCollection.Count == e.Count)
                        {
                            if (indexed)
                                sortingIndicies = new Dictionary<TSource, int>();
                            rangeObservableCollection.Clear();
                        }
                        else
                        {
                            var elementsRemovedByIndex = new List<(TSource element, int index)>();
                            if (indexed)
                                foreach (var element in e.Elements)
                                    elementsRemovedByIndex.Add((element, sortingIndicies.TryGetValue(element, out var index) ? index : -1));
                            else
                                foreach (var element in e.Elements)
                                    elementsRemovedByIndex.Add((element, rangeObservableCollection.IndexOf(element)));
                            var elementsRemovedByIndexSorted = elementsRemovedByIndex.Where(ie => ie.index >= 0).OrderByDescending(ie => ie.index).ToList();
                            if (indexed)
                                foreach (var (element, index) in elementsRemovedByIndexSorted)
                                {
                                    rangeObservableCollection.RemoveAt(index);
                                    sortingIndicies.Remove(element);
                                }
                            else
                                foreach (var (element, index) in elementsRemovedByIndexSorted)
                                    rangeObservableCollection.RemoveAt(index);
                            if (indexed && elementsRemovedByIndexSorted.Any())
                            {
                                for (int i = elementsRemovedByIndexSorted.Last().index, ii = rangeObservableCollection.Count; i < ii; ++i)
                                    sortingIndicies[rangeObservableCollection[i]] = i;
                            }
                        }
                    }).ConfigureAwait(false);
            }

            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            var result = new ActiveEnumerable<TSource>(rangeObservableCollection, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveEnumerable<TResult> ActiveSelect<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult> selector, Action<TResult> releaser = null, Action<TSource, string, TResult> updater = null, IndexingStrategy indexingStrategy = IndexingStrategy.NoneOrInherit, params string[] selectorProperties) where TSource : class =>
            ActiveSelect(source, (source as ISynchronizable)?.SynchronizationContext, selector, releaser, updater, indexingStrategy, selectorProperties);

        public static ActiveEnumerable<TResult> ActiveSelect<TSource, TResult>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, Func<TSource, TResult> selector, Action<TResult> releaser = null, Action<TSource, string, TResult> updater = null, IndexingStrategy indexingStrategy = IndexingStrategy.NoneOrInherit, params string[] selectorProperties) where TSource : class
        {
            IDictionary<TSource, int> sourceToIndex;
            switch (indexingStrategy)
            {
                case IndexingStrategy.HashTable:
                    sourceToIndex = new Dictionary<TSource, int>();
                    break;
                case IndexingStrategy.SelfBalancingBinarySearchTree:
                    sourceToIndex = new SortedDictionary<TSource, int>();
                    break;
                default:
                    sourceToIndex = null;
                    break;
            }
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TResult>(synchronizationContext, indexingStrategy != IndexingStrategy.NoneOrInherit ? source.Select((element, index) =>
            {
                sourceToIndex.Add(element, index);
                return selector(element);
            }) : source.Select(element => selector(element)), false);
            var rangeObservableCollectionAccess = new AsyncLock();
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);

            async void elementPropertyChangedHandler(object sender, ElementPropertyChangeEventArgs<TSource> e)
            {
                var sourceElement = e.Element;
                TResult targetElement;
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var index = indexingStrategy != IndexingStrategy.NoneOrInherit ? sourceToIndex[sourceElement] : source.IndexOf(sourceElement);
                    if (updater == null)
                        targetElement = await rangeObservableCollection.ReplaceAsync(index, selector(sourceElement)).ConfigureAwait(false);
                    else
                        targetElement = await rangeObservableCollection.GetItemAsync(index).ConfigureAwait(false);
                }
                if (updater == null)
                    releaser?.Invoke(targetElement);
                else
                    updater(e.Element, e.PropertyName, targetElement);
            }

            async void elementsAddedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    if (indexingStrategy != IndexingStrategy.NoneOrInherit)
                    {
                        var index = e.Index - 1;
                        foreach (var element in e.Elements)
                            sourceToIndex.Add(element, ++index);
                        for (var i = e.Index + e.Count; i < source.Count; ++i)
                            sourceToIndex[source[i]] = i;
                    }
                    await rangeObservableCollection.InsertRangeAsync(e.Index, e.Elements.Select(selector)).ConfigureAwait(false);
                }
            }

            async void elementsMovedHandler(object sender, ElementsMovedEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    if (indexingStrategy != IndexingStrategy.NoneOrInherit)
                    {
                        var endIndex = (e.FromIndex > e.ToIndex ? e.FromIndex : e.ToIndex) + e.Count;
                        for (var i = e.FromIndex < e.ToIndex ? e.FromIndex : e.ToIndex; i < endIndex; ++i)
                            sourceToIndex[source[i]] = i;
                    }
                    await rangeObservableCollection.MoveRangeAsync(e.FromIndex, e.ToIndex, e.Count).ConfigureAwait(false);
                }
            }

            async void elementsRemovedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                IReadOnlyList<TResult> removedItems;
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    removedItems = await rangeObservableCollection.ReplaceRangeAsync(e.Index, e.Count).ConfigureAwait(false);
                    if (indexingStrategy != IndexingStrategy.NoneOrInherit)
                        for (var i = e.Index; i < source.Count; ++i)
                            sourceToIndex[source[i]] = i;
                }
                if (releaser != null)
                    foreach (var removedItem in removedItems)
                        releaser(removedItem);
            }

            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsMoved += elementsMovedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            var result = new ActiveEnumerable<TResult>(rangeObservableCollection, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsMoved -= elementsMovedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveEnumerable<TResult> ActiveSelectMany<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, IEnumerable<TResult>> selector, Action<TResult> releaser = null, Action<TSource, string, IList<TResult>> updater = null, params string[] selectorProperties) where TSource : class =>
            ActiveSelectMany(source, (source as ISynchronizable)?.SynchronizationContext, selector, releaser, updater, selectorProperties);

        public static ActiveEnumerable<TResult> ActiveSelectMany<TSource, TResult>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, Func<TSource, IEnumerable<TResult>> selector, Action<TResult> releaser = null, Action<TSource, string, IList<TResult>> updater = null, params string[] selectorProperties) where TSource : class
        {
            var sourceToSourceIndex = new Dictionary<TSource, int>();
            var sourceToResultsIndex = new Dictionary<TSource, int>();
            var selectedResults = new Dictionary<TSource, IList<TResult>>();
            var resultsCount = 0;
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TResult>(synchronizationContext, source.SelectMany((element, index) =>
            {
                var results = selector(element);
                var resultsList = new List<TResult>(results);
                sourceToSourceIndex.Add(element, index);
                sourceToResultsIndex.Add(element, resultsCount);
                selectedResults.Add(element, resultsList);
                resultsCount += resultsList.Count;
                return results;
            }), false);
            var rangeObservableCollectionAccess = new AsyncLock();
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);

            async void elementPropertyChangedHandler(object sender, ElementPropertyChangeEventArgs<TSource> e)
            {
                IEnumerable<TResult> releasingItems = null;
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var sourceElement = e.Element;
                    if (!sourceToSourceIndex.TryGetValue(sourceElement, out var sourceIndex))
                        return;
                    var pastResultsList = selectedResults[sourceElement];
                    if (updater == null)
                    {
                        var newResults = selector(sourceElement);
                        var newResultsList = new List<TResult>(newResults);
                        selectedResults[sourceElement] = newResultsList;
                        var lengthDiff = newResultsList.Count - pastResultsList.Count;
                        if (lengthDiff != 0)
                            for (var i = sourceIndex + 1; i < source.Count; ++i)
                                sourceToResultsIndex[source[i]] += lengthDiff;
                        await rangeObservableCollection.ReplaceRangeAsync(sourceToResultsIndex[sourceElement], pastResultsList.Count, newResults).ConfigureAwait(false);
                    }
                    else
                    {
                        var pastResults = pastResultsList.ToList();
                        updater(sourceElement, e.PropertyName, pastResultsList);
                        if (!pastResults.SequenceEqual(pastResultsList))
                        {
                            var lengthDiff = pastResultsList.Count - pastResults.Count;
                            if (lengthDiff != 0)
                                for (var i = sourceIndex + 1; i < source.Count; ++i)
                                    sourceToResultsIndex[source[i]] += lengthDiff;
                            await rangeObservableCollection.ReplaceRangeAsync(sourceToResultsIndex[sourceElement], pastResults.Count, pastResultsList).ConfigureAwait(false);
                            if (releaser != null)
                            {
                                var removedItems = pastResults.Except(pastResultsList);
                                if (removedItems.Any())
                                    releasingItems = removedItems;
                            }
                        }
                    }
                }
                if (releasingItems != null)
                    foreach (var releasingItem in releasingItems)
                        releaser(releasingItem);
            }

            async void elementsAddedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var resultsIndex = 0;
                    if (sourceToResultsIndex.Count > 0)
                    {
                        if (e.Index == sourceToResultsIndex.Count)
                            resultsIndex = await rangeObservableCollection.CountAsync.ConfigureAwait(false);
                        else
                            resultsIndex = sourceToResultsIndex[source[e.Index + e.Count]];
                    }
                    var iteratingResultsIndex = resultsIndex;
                    var resultsAdded = new List<TResult>(e.Elements.SelectMany((element, index) =>
                    {
                        var results = selector(element);
                        var resultsList = new List<TResult>(results);
                        sourceToSourceIndex.Add(element, e.Index + index);
                        sourceToResultsIndex.Add(element, resultsIndex);
                        selectedResults.Add(element, resultsList);
                        iteratingResultsIndex += resultsList.Count;
                        return results;
                    }));
                    for (var i = e.Index + e.Count; i < source.Count; ++i)
                    {
                        sourceToSourceIndex[source[i]] = i;
                        sourceToResultsIndex[source[i]] += resultsAdded.Count;
                    }
                    await rangeObservableCollection.InsertRangeAsync(resultsIndex, resultsAdded).ConfigureAwait(false);
                }
            }

            async void elementsMovedHandler(object sender, ElementsMovedEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var firstElement = e.Elements.First();
                    var resultFromIndex = sourceToResultsIndex[firstElement];
                    var sourceStartIndex = e.FromIndex < e.ToIndex ? e.FromIndex : e.ToIndex;
                    var sourceEndIndex = (e.FromIndex > e.ToIndex ? e.FromIndex : e.ToIndex) + e.Count;
                    var resultStartIndex = 0;
                    if (sourceStartIndex > 0)
                    {
                        var prefixedSourceElement = source[sourceStartIndex - 1];
                        resultStartIndex = sourceToResultsIndex[prefixedSourceElement] + selectedResults[prefixedSourceElement].Count;
                    }
                    var iterativeResultIndex = resultStartIndex;
                    for (var i = sourceStartIndex; i < sourceEndIndex; ++i)
                    {
                        var sourceElement = source[i];
                        sourceToSourceIndex[sourceElement] = i;
                        sourceToResultsIndex[sourceElement] = iterativeResultIndex;
                        var results = selectedResults[sourceElement];
                        iterativeResultIndex += results.Count;
                    }
                    var resultToIndex = sourceToResultsIndex[firstElement];
                    await rangeObservableCollection.MoveRangeAsync(resultFromIndex, resultToIndex, e.Elements.Sum(element => selectedResults[element].Count)).ConfigureAwait(false);
                }
            }

            async void elementsRemovedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                IEnumerable<TResult> releasingItems = null;
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var removedItems = new List<TResult>();
                    foreach (var sourceElement in e.Elements)
                    {
                        removedItems.AddRange(selectedResults[sourceElement]);
                        sourceToSourceIndex.Remove(sourceElement);
                        sourceToResultsIndex.Remove(sourceElement);
                        selectedResults.Remove(sourceElement);
                    }
                    var resultIndex = 0;
                    if (e.Index > 0)
                        resultIndex = sourceToResultsIndex[source[e.Index - 1]];
                    var iteratingResultIndex = resultIndex;
                    for (var i = e.Index; i < source.Count; ++i)
                    {
                        var sourceElement = source[i];
                        sourceToSourceIndex[sourceElement] = i;
                        sourceToResultsIndex[sourceElement] = iteratingResultIndex;
                        iteratingResultIndex += selectedResults[sourceElement].Count;
                    }
                    await rangeObservableCollection.RemoveRangeAsync(resultIndex, removedItems.Count).ConfigureAwait(false);
                    if (releaser != null && removedItems.Count > 0)
                        releasingItems = removedItems;
                }
                if (releasingItems != null)
                    foreach (var releasingItem in releasingItems)
                        releaser(releasingItem);
            }

            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsMoved += elementsMovedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            var result = new ActiveEnumerable<TResult>(rangeObservableCollection, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsMoved -= elementsMovedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveAggregateValue<decimal> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, decimal> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, decimal>();
            var firstSum = (decimal)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<decimal>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (decimal)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (decimal)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<decimal?> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, decimal?> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, decimal?>();
            var firstSum = (decimal?)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<decimal?>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (decimal?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (decimal?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<double> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, double> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, double>();
            var firstSum = (double)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<double>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (double)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (double)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<double?> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, double?> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, double?>();
            var firstSum = (double?)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<double?>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (double?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (double?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<float> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, float> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, float>();
            var firstSum = (float)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<float>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (float)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (float)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<float?> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, float?> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, float?>();
            var firstSum = (float?)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<float?>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (float?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (float?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<int> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, int> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, int>();
            var firstSum = 0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<int>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = 0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = 0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<int?> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, int?> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, int?>();
            var firstSum = (int?)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<int?>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (int?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (int?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<long> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, long> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, long>();
            var firstSum = (long)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<long>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (long)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (long)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveAggregateValue<long?> ActiveSum<TSource>(this IReadOnlyList<TSource> source, Func<TSource, long?> selector, params string[] selectorProperties) where TSource : class
        {
            var selectorValues = new Dictionary<TSource, long?>();
            var firstSum = (long?)0;
            var monitor = ActiveListMonitor<TSource>.Monitor(source, selectorProperties);
            if (monitor.ElementsNotifyChanging)
                foreach (var item in source)
                    firstSum += selector(item);
            else
                foreach (var item in source)
                {
                    var selectorValue = selector(item);
                    firstSum += selectorValue;
                    selectorValues.Add(item, selectorValue);
                }
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangedHandler = null;
            EventHandler<ElementPropertyChangeEventArgs<TSource>> elementPropertyChangingHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsAddedHandler = null;
            EventHandler<ElementMembershipEventArgs<TSource>> elementsRemovedHandler = null;
            var result = new ActiveAggregateValue<long?>(true, firstSum, out var setValidity, out var setValue, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            elementPropertyChangedHandler = (sender, e) =>
            {
                var element = e.Element;
                var previousSelectorValue = selectorValues[element];
                var newSelectorValue = selector(element);
                if (previousSelectorValue != newSelectorValue)
                {
                    if (!monitor.ElementsNotifyChanging)
                        selectorValues[element] = newSelectorValue;
                    setValue(result.Value + (newSelectorValue - previousSelectorValue));
                }
                if (monitor.ElementsNotifyChanging)
                    selectorValues.Remove(element);
            };
            elementPropertyChangingHandler = (sender, e) =>
            {
                var element = e.Element;
                var selectorValue = selector(element);
                if (selectorValues.ContainsKey(element))
                    selectorValues[element] = selectorValue;
                else
                    selectorValues.Add(element, selectorValue);
            };
            elementsAddedHandler = (sender, e) =>
            {
                var delta = (long?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta += selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        var selectorValue = selector(element);
                        selectorValues.Add(element, selectorValue);
                        delta += selectorValue;
                    }
                setValue(result.Value + delta);
            };
            elementsRemovedHandler = (sender, e) =>
            {
                var delta = (long?)0;
                if (monitor.ElementsNotifyChanging)
                    foreach (var element in e.Elements)
                        delta -= selector(element);
                else
                    foreach (var element in e.Elements)
                    {
                        delta -= selectorValues[element];
                        selectorValues.Remove(element);
                    }
                setValue(result.Value + delta);
            };
            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            return result;
        }

        public static ActiveEnumerable<TSource> ActiveWhere<TSource>(this IReadOnlyList<TSource> source, Func<TSource, bool> predicate, params string[] predicateProperties) where TSource : class =>
            ActiveWhere(source, (source as ISynchronizable)?.SynchronizationContext, predicate, predicateProperties);

        public static ActiveEnumerable<TSource> ActiveWhere<TSource>(this IReadOnlyList<TSource> source, SynchronizationContext synchronizationContext, Func<TSource, bool> predicate, params string[] predicateProperties) where TSource : class
        {
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TSource>(synchronizationContext, source.Where(predicate));
            var rangeObservableCollectionAccess = new AsyncLock();
            var monitor = ActiveListMonitor<TSource>.Monitor(source, predicateProperties);
            HashSet<TSource> currentItems;
            if (monitor.ElementsNotifyChanging)
                currentItems = new HashSet<TSource>();
            else
                currentItems = new HashSet<TSource>(rangeObservableCollection);

            async void elementPropertyChangedHandler(object sender, ElementPropertyChangeEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var element = e.Element;
                    if (predicate(element))
                    {
                        if (currentItems.Add(element))
                        {
                            await rangeObservableCollection.AddAsync(element).ConfigureAwait(false);
                            if (monitor.ElementsNotifyChanging)
                                currentItems.Remove(element);
                        }
                    }
                    else if (currentItems.Remove(element))
                        await rangeObservableCollection.RemoveAsync(element).ConfigureAwait(false);
                }
            }

            async void elementPropertyChangingHandler(object sender, ElementPropertyChangeEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var element = e.Element;
                    if (predicate(element))
                        currentItems.Add(element);
                }
            }

            async void elementsAddedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var matchingElements = e.Elements.Where(predicate);
                    if (!monitor.ElementsNotifyChanging)
                        currentItems.UnionWith(matchingElements);
                    await rangeObservableCollection.AddRangeAsync(matchingElements).ConfigureAwait(false);
                }
            }

            async void elementsRemovedHandler(object sender, ElementMembershipEventArgs<TSource> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var matchingElements = new HashSet<TSource>(currentItems);
                    matchingElements.IntersectWith(e.Elements);
                    currentItems.ExceptWith(e.Elements);
                    await rangeObservableCollection.RemoveRangeAsync(matchingElements).ConfigureAwait(false);
                }
            }

            monitor.ElementPropertyChanged += elementPropertyChangedHandler;
            monitor.ElementPropertyChanging += elementPropertyChangingHandler;
            monitor.ElementsAdded += elementsAddedHandler;
            monitor.ElementsRemoved += elementsRemovedHandler;
            var result = new ActiveEnumerable<TSource>(rangeObservableCollection, disposing =>
            {
                monitor.ElementPropertyChanged -= elementPropertyChangedHandler;
                monitor.ElementPropertyChanging -= elementPropertyChangingHandler;
                monitor.ElementsAdded -= elementsAddedHandler;
                monitor.ElementsRemoved -= elementsRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveLookup<TKey, TValue> ActiveWhere<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, Func<TKey, TValue, bool> predicate, IndexingStrategy indexingStrategy = IndexingStrategy.NoneOrInherit, params string[] predicateProperties) =>
            ActiveWhere(source, (source as ISynchronizable)?.SynchronizationContext, predicate, indexingStrategy, predicateProperties);

        public static ActiveLookup<TKey, TValue> ActiveWhere<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, SynchronizationContext synchronizationContext, Func<TKey, TValue, bool> predicate, IndexingStrategy indexingStrategy = IndexingStrategy.NoneOrInherit, params string[] predicateProperties)
        {
            bool selfBalancingBinarySearchTree;
            switch (indexingStrategy)
            {
                case IndexingStrategy.HashTable:
                    selfBalancingBinarySearchTree = false;
                    break;
                case IndexingStrategy.SelfBalancingBinarySearchTree:
                    selfBalancingBinarySearchTree = true;
                    break;
                default:
                    selfBalancingBinarySearchTree = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue>;
                    break;
            }
            var rangeObservableDictionary = selfBalancingBinarySearchTree ? (ISynchronizableRangeDictionary<TKey, TValue>)new SynchronizedObservableSortedDictionary<TKey, TValue>(synchronizationContext, false) : new SynchronizedObservableDictionary<TKey, TValue>(synchronizationContext, false);
            rangeObservableDictionary.AddRange(source.Where(kvp => predicate(kvp.Key, kvp.Value)));
            var rangeDictionaryAccess = new AsyncLock();
            var monitor = ActiveDictionaryMonitor<TKey, TValue>.Monitor(source, predicateProperties);

            async void valueAddedHandler(object sender, NotifyDictionaryValueEventArgs<TKey, TValue> e)
            {
                var key = e.Key;
                var value = e.Value;
                var match = predicate(key, value);
                if (match)
                    using (await rangeDictionaryAccess.LockAsync().ConfigureAwait(false))
                        await rangeObservableDictionary.AddAsync(key, value).ConfigureAwait(false);
            }

            async void valuePropertyChangedHandler(object sender, ValuePropertyChangeEventArgs<TKey, TValue> e)
            {
                var key = e.Key;
                var value = e.Value;
                var currentMatch = predicate(key, value);
                using (await rangeDictionaryAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableDictionary.ExecuteAsync(() =>
                    {
                        var previousMatch = rangeObservableDictionary.ContainsKey(key);
                        if (previousMatch && !currentMatch)
                            rangeObservableDictionary.Remove(key);
                        else if (!previousMatch && currentMatch)
                            rangeObservableDictionary.Add(key, value);
                    }).ConfigureAwait(false);
            }

            async void valueRemovedHandler(object sender, NotifyDictionaryValueEventArgs<TKey, TValue> e)
            {
                using (await rangeDictionaryAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableDictionary.RemoveAsync(e.Key).ConfigureAwait(false);
            }

            async void valueReplacedHandler(object sender, NotifyDictionaryValueReplacedEventArgs<TKey, TValue> e)
            {
                var key = e.Key;
                var value = e.NewValue;
                var currentMatch = predicate(key, value);
                using (await rangeDictionaryAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableDictionary.ExecuteAsync(() =>
                    {
                        var previousMatch = rangeObservableDictionary.ContainsKey(key);
                        if (previousMatch && !currentMatch)
                            rangeObservableDictionary.Remove(key);
                        else if (!previousMatch && currentMatch)
                            rangeObservableDictionary.Add(key, value);
                    }).ConfigureAwait(false);
            }

            async void valuesAddedHandler(object sender, NotifyDictionaryValuesEventArgs<TKey, TValue> e)
            {
                var matches = e.KeyValuePairs.Where(kvp => predicate(kvp.Key, kvp.Value)).ToList();
                using (await rangeDictionaryAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableDictionary.AddRangeAsync(matches).ConfigureAwait(false);
            }

            async void valuesRemovedHandler(object sender, NotifyDictionaryValuesEventArgs<TKey, TValue> e)
            {
                var keys = e.KeyValuePairs.Select(kvp => kvp.Key).ToList();
                using (await rangeDictionaryAccess.LockAsync().ConfigureAwait(false))
                    await rangeObservableDictionary.RemoveRangeAsync(keys).ConfigureAwait(false);
            }

            monitor.ValueAdded += valueAddedHandler;
            monitor.ValuePropertyChanged += valuePropertyChangedHandler;
            monitor.ValueRemoved += valueRemovedHandler;
            monitor.ValueReplaced += valueReplacedHandler;
            monitor.ValuesAdded += valuesAddedHandler;
            monitor.ValuesRemoved += valuesRemovedHandler;
            var result = new ActiveLookup<TKey, TValue>(rangeObservableDictionary, disposing =>
            {
                monitor.ValueAdded -= valueAddedHandler;
                monitor.ValuePropertyChanged -= valuePropertyChangedHandler;
                monitor.ValueRemoved -= valueRemovedHandler;
                monitor.ValueReplaced -= valueReplacedHandler;
                monitor.ValuesAdded -= valuesAddedHandler;
                monitor.ValuesRemoved -= valuesRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            if (synchronizationContext != null)
                rangeObservableDictionary.IsSynchronized = true;
            return result;
        }

        public static ActiveEnumerable<TValue> ToActiveEnumerable<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source) =>
            ToActiveEnumerable(source, (source as ISynchronizable)?.SynchronizationContext);

        public static ActiveEnumerable<TValue> ToActiveEnumerable<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, SynchronizationContext synchronizationContext) =>
            ToActiveEnumerable(source, synchronizationContext, (key, value) => value);

        public static ActiveEnumerable<TResult> ToActiveEnumerable<TKey, TValue, TResult>(this IReadOnlyDictionary<TKey, TValue> source, Func<TKey, TValue, TResult> selector, Action<TResult> releaser = null, Action<TKey, TValue, string, TResult> updater = null, params string[] selectorProperties) =>
            ToActiveEnumerable(source, (source as ISynchronizable)?.SynchronizationContext, selector, releaser, updater, selectorProperties);

        public static ActiveEnumerable<TResult> ToActiveEnumerable<TKey, TValue, TResult>(this IReadOnlyDictionary<TKey, TValue> source, SynchronizationContext synchronizationContext, Func<TKey, TValue, TResult> selector, Action<TResult> releaser = null, Action<TKey, TValue, string, TResult> updater = null, params string[] selectorProperties)
        {
            var keyToIndex = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, int>)new SortedDictionary<TKey, TValue>() : (IDictionary<TKey, int>)new Dictionary<TKey, TValue>();
            var rangeObservableCollection = new SynchronizedRangeObservableCollection<TResult>(synchronizationContext, source.Select((element, index) =>
            {
                keyToIndex.Add(element.Key, index);
                return selector(element.Key, element.Value);
            }), false);
            var rangeObservableCollectionAccess = new AsyncLock();
            var monitor = ActiveDictionaryMonitor<TKey, TValue>.Monitor(source, selectorProperties);

            async void valueAddedHandler(object sender, NotifyDictionaryValueEventArgs<TKey, TValue> e)
            {
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    keyToIndex.Add(e.Key, keyToIndex.Count);
                    await rangeObservableCollection.AddAsync(selector(e.Key, e.Value)).ConfigureAwait(false);
                }
            }

            async void valuePropertyChangedHandler(object sender, ValuePropertyChangeEventArgs<TKey, TValue> e)
            {
                TResult element;
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    if (updater == null)
                        element = await rangeObservableCollection.ReplaceAsync(keyToIndex[e.Key], selector(e.Key, e.Value)).ConfigureAwait(false);
                    else
                        element = await rangeObservableCollection.GetItemAsync(keyToIndex[e.Key]).ConfigureAwait(false);
                }
                if (updater == null)
                    releaser?.Invoke(element);
                else
                    updater(e.Key, e.Value, e.PropertyName, element);
            }

            async void valueRemovedHandler(object sender, NotifyDictionaryValueEventArgs<TKey, TValue> e)
            {
                TResult removedElement;
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                {
                    var removingIndex = keyToIndex[e.Key];
                    keyToIndex.Remove(e.Key);
                    foreach (var key in keyToIndex.Keys.ToList())
                    {
                        var index = keyToIndex[key];
                        if (index > removingIndex)
                            keyToIndex[key] = index - 1;
                    }
                    removedElement = await rangeObservableCollection.GetAndRemoveAtAsync(removingIndex).ConfigureAwait(false);
                }
                releaser?.Invoke(removedElement);
            }

            async void valueReplacedHandler(object sender, NotifyDictionaryValueReplacedEventArgs<TKey, TValue> e)
            {
                TResult replacedElement;
                using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                    replacedElement = await rangeObservableCollection.ReplaceAsync(keyToIndex[e.Key], selector(e.Key, e.NewValue)).ConfigureAwait(false);
                releaser?.Invoke(replacedElement);
            }

            async void valuesAddedHandler(object sender, NotifyDictionaryValuesEventArgs<TKey, TValue> e)
            {
                if (e.KeyValuePairs.Any())
                    using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                    {
                        var lastIndex = keyToIndex.Count - 1;
                        foreach (var keyValuePair in e.KeyValuePairs)
                            keyToIndex.Add(keyValuePair.Key, ++lastIndex);
                        await rangeObservableCollection.AddRangeAsync(e.KeyValuePairs.Select(kvp => selector(kvp.Key, kvp.Value))).ConfigureAwait(false);
                    }
            }

            async void valuesRemovedHandler(object sender, NotifyDictionaryValuesEventArgs<TKey, TValue> e)
            {
                if (e.KeyValuePairs.Any())
                    using (await rangeObservableCollectionAccess.LockAsync().ConfigureAwait(false))
                    {
                        var removingIndicies = new List<int>();
                        foreach (var kvp in e.KeyValuePairs)
                        {
                            removingIndicies.Add(keyToIndex[kvp.Key]);
                            keyToIndex.Remove(kvp.Key);
                        }
                        var rangeStart = -1;
                        var rangeCount = 0;
                        await rangeObservableCollection.ExecuteAsync(() =>
                        {
                            foreach (var removingIndex in removingIndicies.OrderByDescending(i => i))
                            {
                                if (removingIndex != rangeStart - 1 && rangeStart != -1)
                                {
                                    if (rangeCount == 1)
                                        rangeObservableCollection.RemoveAt(rangeStart);
                                    else
                                        rangeObservableCollection.RemoveRange(rangeStart, rangeCount);
                                    rangeCount = 0;
                                }
                                rangeStart = removingIndex;
                                ++rangeCount;
                            }
                            if (rangeStart != -1)
                            {
                                if (rangeCount == 1)
                                    rangeObservableCollection.RemoveAt(rangeStart);
                                else
                                    rangeObservableCollection.RemoveRange(rangeStart, rangeCount);
                            }
                        }).ConfigureAwait(false);
                        var revisedKeyedIndicies = keyToIndex.OrderBy(kvp => kvp.Value).Select((element, index) => (element.Key, index));
                        keyToIndex = source is SortedDictionary<TKey, TValue> || source is ObservableSortedDictionary<TKey, TValue> || source is SynchronizedObservableSortedDictionary<TKey, TValue> ? (IDictionary<TKey, int>)new SortedDictionary<TKey, TValue>() : (IDictionary<TKey, int>)new Dictionary<TKey, TValue>();
                        foreach (var (key, index) in revisedKeyedIndicies)
                            keyToIndex.Add(key, index);
                    }
            }

            monitor.ValueAdded += valueAddedHandler;
            monitor.ValuePropertyChanged += valuePropertyChangedHandler;
            monitor.ValueRemoved += valueRemovedHandler;
            monitor.ValueReplaced += valueReplacedHandler;
            monitor.ValuesAdded += valuesAddedHandler;
            monitor.ValuesRemoved += valuesRemovedHandler;
            var result = new ActiveEnumerable<TResult>(rangeObservableCollection, disposing =>
            {
                monitor.ValueAdded -= valueAddedHandler;
                monitor.ValuePropertyChanged -= valuePropertyChangedHandler;
                monitor.ValueRemoved -= valueRemovedHandler;
                monitor.ValueReplaced -= valueReplacedHandler;
                monitor.ValuesAdded -= valuesAddedHandler;
                monitor.ValuesRemoved -= valuesRemovedHandler;
                if (disposing)
                    monitor.Dispose();
            });
            if (synchronizationContext != null)
                rangeObservableCollection.IsSynchronized = true;
            return result;
        }

        public static ActiveEnumerable<TSource> ToActiveEnumerable<TSource>(this IReadOnlyList<TSource> source) => new ActiveEnumerable<TSource>(source);
    }
}
