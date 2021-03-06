using Gear.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Gear.ActiveExpressions
{
    class ActiveIndexExpression : ActiveExpression
    {
        static readonly object instanceManagementLock = new object();
        static readonly Dictionary<(ActiveExpression @object, PropertyInfo indexer, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options), ActiveIndexExpression> instances = new Dictionary<(ActiveExpression @object, PropertyInfo indexer, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options), ActiveIndexExpression>();

        public static ActiveIndexExpression Create(IndexExpression indexExpression, ActiveExpressionOptions options, bool deferEvaluation)
        {
            var @object = Create(indexExpression.Object, options, deferEvaluation);
            var indexer = indexExpression.Indexer;
            var arguments = new EquatableList<ActiveExpression>(indexExpression.Arguments.Select(argument => Create(argument, options, deferEvaluation)).ToList());
            var key = (@object, indexer, arguments, options);
            lock (instanceManagementLock)
            {
                if (!instances.TryGetValue(key, out var activeIndexExpression))
                {
                    activeIndexExpression = new ActiveIndexExpression(indexExpression.Type, @object, indexer, arguments, options, deferEvaluation);
                    instances.Add(key, activeIndexExpression);
                }
                ++activeIndexExpression.disposalCount;
                return activeIndexExpression;
            }
        }

        public static bool operator ==(ActiveIndexExpression a, ActiveIndexExpression b) => a?.arguments == b?.arguments && a?.indexer == b?.indexer && a?.@object == b?.@object && a?.options == b?.options;

        public static bool operator !=(ActiveIndexExpression a, ActiveIndexExpression b) => a?.arguments != b?.arguments || a?.indexer != b?.indexer || a?.@object != b?.@object || a?.options != b?.options;

        ActiveIndexExpression(Type type, ActiveExpression @object, PropertyInfo indexer, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options, bool deferEvaluation) : base(type, ExpressionType.Index, options, deferEvaluation)
        {
            this.indexer = indexer;
            getMethod = this.indexer.GetMethod;
            fastGetter = GetFastMethodInfo(getMethod);
            this.@object = @object;
            this.@object.PropertyChanged += ObjectPropertyChanged;
            this.arguments = arguments;
            foreach (var argument in this.arguments)
                argument.PropertyChanged += ArgumentPropertyChanged;
            EvaluateIfNotDeferred();
        }

        readonly EquatableList<ActiveExpression> arguments;
        int disposalCount;
        readonly FastMethodInfo fastGetter;
        readonly MethodInfo getMethod;
        readonly PropertyInfo indexer;
        readonly ActiveExpression @object;
        object objectValue;

        void ArgumentPropertyChanged(object sender, PropertyChangedEventArgs e) => Evaluate();

        protected override bool Dispose(bool disposing)
        {
            var result = false;
            lock (instanceManagementLock)
                if (--disposalCount == 0)
                {
                    instances.Remove((@object, indexer, arguments, options));
                    result = true;
                }
            if (result)
            {
                UnsubscribeFromObjectValueNotifications();
                @object.PropertyChanged -= ObjectPropertyChanged;
                @object.Dispose();
                foreach (var argument in arguments)
                {
                    argument.PropertyChanged -= ArgumentPropertyChanged;
                    argument.Dispose();
                }
                DisposeValueIfNecessary();
            }
            return result;
        }

        void DisposeValueIfNecessary()
        {
            if (ApplicableOptions.IsMethodReturnValueDisposed(getMethod) && TryGetUndeferredValue(out var value))
            {
                if (value is IDisposable disposable)
                    disposable.Dispose();
                else if (value is IAsyncDisposable asyncDisposable)
                    asyncDisposable.DisposeAsync().Wait();
            }
        }

        public override bool Equals(object obj) => obj is ActiveIndexExpression other && arguments.Equals(other.arguments) && indexer.Equals(other.indexer) && @object.Equals(other.@object) && (options?.Equals(other.options) ?? other.options is null);

        protected override void Evaluate()
        {
            try
            {
                DisposeValueIfNecessary();
                var objectFault = @object.Fault;
                var argumentFault = arguments.Select(argument => argument.Fault).Where(fault => fault != null).FirstOrDefault();
                if (objectFault != null)
                    Fault = objectFault;
                else if (argumentFault != null)
                    Fault = argumentFault;
                else
                {
                    var newObjectValue = @object.Value;
                    if (newObjectValue != objectValue)
                    {
                        UnsubscribeFromObjectValueNotifications();
                        objectValue = newObjectValue;
                        SubscribeToObjectValueNotifications();
                    }
                    Value = fastGetter.Invoke(objectValue, arguments.Select(argument => argument.Value).ToArray());
                }
            }
            catch (Exception ex)
            {
                Fault = ex;
            }
        }

        public override int GetHashCode() => HashCodes.CombineObjects(typeof(ActiveIndexExpression), arguments, indexer, @object, options);

        void ObjectPropertyChanged(object sender, PropertyChangedEventArgs e) => Evaluate();

        void ObjectValueCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        if (e.NewStartingIndex >= 0 && (e.NewItems?.Count ?? 0) > 0 && arguments.Count == 1 && arguments[0].Value is int index && e.NewStartingIndex <= index)
                            Evaluate();
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    {
                        var movingCount = Math.Max(e.OldItems?.Count ?? 0, e.NewItems?.Count ?? 0);
                        if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && movingCount > 0 && arguments.Count == 1 && arguments[0].Value is int index && ((index >= e.OldStartingIndex && index < e.OldStartingIndex + movingCount) || (index >= e.NewStartingIndex && index < e.NewStartingIndex + movingCount)))
                            Evaluate();
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    {
                        if (e.OldStartingIndex >= 0 && (e.OldItems?.Count ?? 0) > 0 && arguments.Count == 1 && arguments[0].Value is int index && e.OldStartingIndex <= index)
                            Evaluate();
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    {
                        if (arguments.Count == 1 && arguments[0].Value is int index)
                        {
                            var oldCount = e.OldItems?.Count ?? 0;
                            var newCount = e.NewItems?.Count ?? 0;
                            if ((oldCount != newCount && (e.OldStartingIndex >= 0 || e.NewStartingIndex >= 0) && index >= Math.Min(Math.Max(e.OldStartingIndex, 0), Math.Max(e.NewStartingIndex, 0))) || (e.OldStartingIndex >= 0 && index >= e.OldStartingIndex && index < e.OldStartingIndex + oldCount) || (e.NewStartingIndex >= 0 && index >= e.NewStartingIndex && index < e.NewStartingIndex + newCount))
                                Evaluate();
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    Evaluate();
                    break;
            }
        }

        void ObjectValueDictionaryChanged(object sender, NotifyDictionaryChangedEventArgs<object, object> e)
        {
            if (e.Action == NotifyDictionaryChangedAction.Reset)
                Evaluate();
            else if (arguments.Count == 1)
            {
                var removed = false;
                var key = arguments[0].Value;
                if (key != null)
                {
                    removed = e.OldItems?.Any(kv => key.Equals(kv.Key)) ?? false;
                    var keyValuePair = e.NewItems?.FirstOrDefault(kv => key.Equals(kv.Key)) ?? default;
                    if (keyValuePair.Key != null)
                    {
                        removed = false;
                        Value = keyValuePair.Value;
                    }
                }
                if (removed)
                    Fault = new KeyNotFoundException($"Key '{key}' was removed");
            }
        }

        void ObjectValuePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == indexer.Name)
                Evaluate();
        }

        void SubscribeToObjectValueNotifications()
        {
            if (objectValue is INotifyDictionaryChanged dictionaryChangedNotifier)
                dictionaryChangedNotifier.DictionaryChanged += ObjectValueDictionaryChanged;
            else if (objectValue is INotifyCollectionChanged collectionChangedNotifier)
                collectionChangedNotifier.CollectionChanged += ObjectValueCollectionChanged;
            if (objectValue is INotifyPropertyChanged propertyChangedNotifier)
                propertyChangedNotifier.PropertyChanged += ObjectValuePropertyChanged;
        }

        public override string ToString() => $"{@object}{string.Join(string.Empty, arguments.Select(argument => $"[{argument}]"))} {ToStringSuffix}";

        void UnsubscribeFromObjectValueNotifications()
        {
            if (objectValue is INotifyDictionaryChanged dictionaryChangedNotifier)
                dictionaryChangedNotifier.DictionaryChanged -= ObjectValueDictionaryChanged;
            else if (objectValue is INotifyCollectionChanged collectionChangedNotifier)
                collectionChangedNotifier.CollectionChanged -= ObjectValueCollectionChanged;
            if (objectValue is INotifyPropertyChanged propertyChangedNotifier)
                propertyChangedNotifier.PropertyChanged -= ObjectValuePropertyChanged;
        }
    }
}
