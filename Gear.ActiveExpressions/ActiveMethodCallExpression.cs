using Gear.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Gear.ActiveExpressions
{
    class ActiveMethodCallExpression : ActiveExpression
    {
        static readonly object instanceManagementLock = new object();
        static readonly Dictionary<(ActiveExpression @object, MethodInfo method, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options), ActiveMethodCallExpression> instanceInstances = new Dictionary<(ActiveExpression @object, MethodInfo method, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options), ActiveMethodCallExpression>();
        static readonly Dictionary<(MethodInfo method, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options), ActiveMethodCallExpression> staticInstances = new Dictionary<(MethodInfo method, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options), ActiveMethodCallExpression>();

        public static ActiveMethodCallExpression Create(MethodCallExpression methodCallExpression, ActiveExpressionOptions options, bool deferEvaluation)
        {
            if (methodCallExpression.Object == null)
            {
                var method = methodCallExpression.Method;
                var arguments = new EquatableList<ActiveExpression>(methodCallExpression.Arguments.Select(argument => Create(argument, options, deferEvaluation)).ToList());
                var key = (method, arguments, options);
                lock (instanceManagementLock)
                {
                    if (!staticInstances.TryGetValue(key, out var activeMethodCallExpression))
                    {
                        activeMethodCallExpression = new ActiveMethodCallExpression(methodCallExpression.Type, method, arguments, options, deferEvaluation);
                        staticInstances.Add(key, activeMethodCallExpression);
                    }
                    ++activeMethodCallExpression.disposalCount;
                    return activeMethodCallExpression;
                }
            }
            else
            {
                var @object = Create(methodCallExpression.Object, options, deferEvaluation);
                var method = methodCallExpression.Method;
                var arguments = new EquatableList<ActiveExpression>(methodCallExpression.Arguments.Select(argument => Create(argument, options, deferEvaluation)).ToList());
                var key = (@object, method, arguments, options);
                lock (instanceManagementLock)
                {
                    if (!instanceInstances.TryGetValue(key, out var activeMethodCallExpression))
                    {
                        activeMethodCallExpression = new ActiveMethodCallExpression(methodCallExpression.Type, @object, method, arguments, options, deferEvaluation);
                        instanceInstances.Add(key, activeMethodCallExpression);
                    }
                    ++activeMethodCallExpression.disposalCount;
                    return activeMethodCallExpression;
                }
            }
        }

        public static bool operator ==(ActiveMethodCallExpression a, ActiveMethodCallExpression b) => a?.arguments == b?.arguments && a?.method == b?.method && a?.@object == b?.@object && a?.options == b?.options;

        public static bool operator !=(ActiveMethodCallExpression a, ActiveMethodCallExpression b) => a?.arguments != b?.arguments || a?.method != b?.method || a?.@object != b?.@object || a?.options != b?.options;

        ActiveMethodCallExpression(Type type, ActiveExpression @object, MethodInfo method, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options, bool deferEvaluation) : base(type, ExpressionType.Call, options, deferEvaluation)
        {
            this.method = method;
            fastMethod = GetFastMethodInfo(this.method);
            this.@object = @object;
            this.@object.PropertyChanged += ObjectPropertyChanged;
            this.arguments = arguments;
            foreach (var argument in this.arguments)
                argument.PropertyChanged += ArgumentPropertyChanged;
            EvaluateIfNotDeferred();
        }

        ActiveMethodCallExpression(Type type, MethodInfo method, EquatableList<ActiveExpression> arguments, ActiveExpressionOptions options, bool deferEvaluation) : base(type, ExpressionType.Call, options, deferEvaluation)
        {
            this.method = method;
            fastMethod = GetFastMethodInfo(this.method);
            this.arguments = arguments;
            foreach (var argument in this.arguments)
                argument.PropertyChanged += ArgumentPropertyChanged;
            EvaluateIfNotDeferred();
        }

        readonly EquatableList<ActiveExpression> arguments;
        int disposalCount;
        readonly FastMethodInfo fastMethod;
        readonly MethodInfo method;
        readonly ActiveExpression @object;

        void ArgumentPropertyChanged(object sender, PropertyChangedEventArgs e) => Evaluate();

        protected override bool Dispose(bool disposing)
        {
            var result = false;
            lock (instanceManagementLock)
                if (--disposalCount == 0)
                {
                    if (@object == null)
                        staticInstances.Remove((method, arguments, options));
                    else
                        instanceInstances.Remove((@object, method, arguments, options));
                    result = true;
                }
            if (result)
            {
                if (@object != null)
                {
                    @object.PropertyChanged -= ObjectPropertyChanged;
                    @object.Dispose();
                }
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
            if (ApplicableOptions.IsMethodReturnValueDisposed(method) && TryGetUndeferredValue(out var value))
            {
                if (value is IDisposable disposable)
                    disposable.Dispose();
                else if (value is IAsyncDisposable asyncDisposable)
                    asyncDisposable.DisposeAsync().Wait();
            }
        }

        public override bool Equals(object obj) => obj is ActiveMethodCallExpression other && arguments.Equals(other.arguments) && method.Equals(other.method) && (@object?.Equals(other.@object) ?? other.@object is null) && (options?.Equals(other.options) ?? other.options is null);

        protected override void Evaluate()
        {
            try
            {
                DisposeValueIfNecessary();
                var objectFault = @object?.Fault;
                var argumentFault = arguments.Select(argument => argument.Fault).Where(fault => fault != null).FirstOrDefault();
                if (objectFault != null)
                    Fault = objectFault;
                else if (argumentFault != null)
                    Fault = argumentFault;
                else
                    Value = fastMethod.Invoke(@object?.Value, arguments.Select(argument => argument.Value).ToArray());
            }
            catch (Exception ex)
            {
                Fault = ex;
            }
        }

        public override int GetHashCode() => HashCodes.CombineObjects(typeof(ActiveMethodCallExpression), arguments, method, @object, options);

        void ObjectPropertyChanged(object sender, PropertyChangedEventArgs e) => Evaluate();

        public override string ToString() => $"{@object?.ToString() ?? method.DeclaringType.FullName}.{method.Name}({string.Join(", ", arguments.Select(argument => $"{argument}"))}) {ToStringSuffix}";
    }
}
