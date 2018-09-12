using Gear.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Gear.ActiveExpressions
{
    /// <summary>
    /// Provides the base class from which the classes that represent active expression tree nodes are derived.
    /// Active expressions subscribe to notification events for values in each stage of evaluation and will re-evaluate dependent portions of the expression tree when a change occurs.
    /// Use <see cref="Create{TResult}(LambdaExpression, object[])"/> or one of its strongly-typed overloads to create an active expression.
    /// </summary>
    public abstract class ActiveExpression : SyncDisposablePropertyChangeNotifier
    {
        static readonly ConcurrentDictionary<MethodInfo, FastMethodInfo> compiledMethods = new ConcurrentDictionary<MethodInfo, FastMethodInfo>();

        static FastMethodInfo CreateFastMethodInfo(MethodInfo key) => new FastMethodInfo(key);

        protected static FastMethodInfo GetFastMethodInfo(MethodInfo methodInfo) => compiledMethods.GetOrAdd(methodInfo, CreateFastMethodInfo);

        internal static ActiveExpression Create(Expression expression)
        {
            switch (expression)
            {
                case BinaryExpression binaryExpression:
                    return ActiveBinaryExpression.Create(binaryExpression);
                case ConditionalExpression conditionalExpression:
                    return ActiveConditionalExpression.Create(conditionalExpression);
                case ConstantExpression constantExpression:
                    return ActiveConstantExpression.Create(constantExpression);
                case IndexExpression indexExpression:
                    return ActiveIndexExpression.Create(indexExpression);
                case MemberExpression memberExpression:
                    return ActiveMemberExpression.Create(memberExpression);
                case MethodCallExpression methodCallExpression:
                    return ActiveMethodCallExpression.Create(methodCallExpression);
                case NewExpression newExpression:
                    return ActiveNewExpression.Create(newExpression);
                case UnaryExpression unaryExpression:
                    return ActiveUnaryExpression.Create(unaryExpression);
                case null:
                    throw new ArgumentNullException(nameof(expression));
                default:
                    throw new NotSupportedException($"Cannot create an expression of type \"{expression.GetType().Name}\"");
            }
        }

        /// <summary>
        /// Creates an active expression using a specified lambda expression and arguments
        /// </summary>
        /// <typeparam name="TResult">The type that <paramref name="lambdaExpression"/> returns.</typeparam>
        /// <param name="lambdaExpression">The lambda expression.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The active expression.</returns>
        public static ActiveExpression<TResult> Create<TResult>(LambdaExpression lambdaExpression, params object[] arguments) => ActiveExpression<TResult>.Create(lambdaExpression, arguments);

        /// <summary>
        /// Creates an active expression using a specified strongly-typed lambda expression
        /// </summary>
        /// <typeparam name="TResult">The type that <paramref name="expression"/> returns.</typeparam>
        /// <param name="expression">The strongly-typed lambda expression.</param>
        /// <returns>The active expression.</returns>
        public static ActiveExpression<TResult> Create<TResult>(Expression<Func<TResult>> expression) => Create<TResult>((LambdaExpression)expression);

        /// <summary>
        /// Creates an active expression using a specified strongly-typed lambda expression and one argument
        /// </summary>
        /// <typeparam name="TArg">The type of the argument.</typeparam>
        /// <typeparam name="TResult">The type that <paramref name="expression"/> returns.</typeparam>
        /// <param name="expression">The strongly-typed lambda expression.</param>
        /// <param name="arg">The argument.</param>
        /// <returns>The active expression.</returns>
        public static ActiveExpression<TResult> Create<TArg, TResult>(Expression<Func<TArg, TResult>> expression, TArg arg) => Create<TResult>(expression, arg);

        /// <summary>
        /// Creates an active expression using a specified strongly-typed lambda expression and two arguments
        /// </summary>
        /// <typeparam name="TArg1">The type of the first argument</typeparam>
        /// <typeparam name="TArg2">The type of the second argument</typeparam>
        /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
        /// <param name="expression">The strongly-typed lambda expression</param>
        /// <param name="arg1">The first argument</param>
        /// <param name="arg2">The second argument</param>
        /// <returns>The active expression</returns>
        public static ActiveExpression<TResult> Create<TArg1, TArg2, TResult>(Expression<Func<TArg1, TArg2, TResult>> expression, TArg1 arg1, TArg2 arg2) => Create<TResult>(expression, arg1, arg2);

        /// <summary>
        /// Creates an active expression using a specified strongly-typed lambda expression and three arguments
        /// </summary>
        /// <typeparam name="TArg1">The type of the first argument</typeparam>
        /// <typeparam name="TArg2">The type of the second argument</typeparam>
        /// <typeparam name="TArg3">The type of the third argument</typeparam>
        /// <typeparam name="TResult">The type that <paramref name="expression"/> returns</typeparam>
        /// <param name="expression">The strongly-typed lambda expression</param>
        /// <param name="arg1">The first argument</param>
        /// <param name="arg2">The second argument</param>
        /// <param name="arg3">The third argument</param>
        /// <returns>The active expression</returns>
        public static ActiveExpression<TResult> Create<TArg1, TArg2, TArg3, TResult>(Expression<Func<TArg1, TArg2, TArg3, TResult>> expression, TArg1 arg1, TArg2 arg2, TArg3 arg3) => Create<TResult>(expression, arg1, arg2, arg3);

        internal static Expression ReplaceParameters(LambdaExpression lambdaExpression, params object[] arguments)
        {
            var parameterTranslation = new Dictionary<ParameterExpression, ConstantExpression>();
            for (var i = 0; i < lambdaExpression.Parameters.Count; ++i)
            {
                var parameter = lambdaExpression.Parameters[i];
                parameterTranslation.Add(parameter, Expression.Constant(arguments[i], parameter.Type));
            }
            var expression = lambdaExpression.Body;
            while (expression?.CanReduce ?? false)
                expression = expression.ReduceAndCheck();
            return ReplaceParameters(parameterTranslation, expression);
        }

        static Expression ReplaceParameters(Dictionary<ParameterExpression, ConstantExpression> parameterTranslation, Expression expression)
        {
            switch (expression)
            {
                case BinaryExpression binaryExpression:
                    return Expression.MakeBinary(binaryExpression.NodeType, ReplaceParameters(parameterTranslation, binaryExpression.Left), ReplaceParameters(parameterTranslation, binaryExpression.Right), binaryExpression.IsLiftedToNull, binaryExpression.Method, (LambdaExpression)ReplaceParameters(parameterTranslation, binaryExpression.Conversion));
                case ConditionalExpression conditionalExpression:
                    return Expression.Condition(ReplaceParameters(parameterTranslation, conditionalExpression.Test), ReplaceParameters(parameterTranslation, conditionalExpression.IfTrue), ReplaceParameters(parameterTranslation, conditionalExpression.IfTrue), conditionalExpression.Type);
                case ConstantExpression constantExpression:
                    return constantExpression;
                case IndexExpression indexExpression:
                    return Expression.MakeIndex(ReplaceParameters(parameterTranslation, indexExpression.Object), indexExpression.Indexer, indexExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument)));
                case MemberExpression memberExpression:
                    return Expression.MakeMemberAccess(ReplaceParameters(parameterTranslation, memberExpression.Expression), memberExpression.Member);
                case MethodCallExpression methodCallExpression:
                    return methodCallExpression.Object == null ? Expression.Call(methodCallExpression.Method, methodCallExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument))) : Expression.Call(ReplaceParameters(parameterTranslation, methodCallExpression.Object), methodCallExpression.Method, methodCallExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument)));
                case NewExpression newExpression:
                    return Expression.New(newExpression.Constructor, newExpression.Arguments.Select(argument => ReplaceParameters(parameterTranslation, argument)), newExpression.Members);
                case ParameterExpression parameterExpression:
                    return parameterTranslation[parameterExpression];
                case UnaryExpression unaryExpression:
                    return Expression.MakeUnary(unaryExpression.NodeType, ReplaceParameters(parameterTranslation, unaryExpression.Operand), unaryExpression.Type, unaryExpression.Method);
                case null:
                    return null;
                default:
                    throw new NotSupportedException($"Cannot replace parameters in {expression.GetType().Name}");
            }
        }

        public ActiveExpression(Type type, ExpressionType nodeType)
        {
            Type = type;
            NodeType = nodeType;
        }

        Exception fault;
        object val;

        public Exception Fault
        {
            get => fault;
            protected set
            {
                if (value != null)
                    Value = null;
                SetBackedProperty(ref fault, in value);
            }
        }

        public ExpressionType NodeType { get; }

        public Type Type { get; }

        public object Value
        {
            get => val;
            protected set
            {
                if (value != null)
                    Fault = null;
                SetBackedProperty(ref val, in value);
            }
        }
    }

    /// <summary>
    /// Represents an active evaluation of an expression.
    /// <see cref="INotifyPropertyChanged"/>, <see cref="INotifyCollectionChanged"/>, and <see cref="INotifyDictionaryChanged"/> events raised by any value within the expression will cause all dependent portions to be re-evaluated.
    /// </summary>
    /// <typeparam name="TResult">The value returned by the expression upon which this active expression is based</typeparam>
    public class ActiveExpression<TResult> : SyncDisposablePropertyChangeNotifier
    {
        static readonly object instanceManagementLock = new object();
        static readonly Dictionary<(string expressionString, EquatableList<object> arguments), ActiveExpression<TResult>> instances = new Dictionary<(string expressionString, EquatableList<object> arguments), ActiveExpression<TResult>>();

        internal static ActiveExpression<TResult> Create(LambdaExpression expression, params object[] arguments)
        {
            var expressionString = expression.ToString();
            var key = (expressionString, new EquatableList<object>(arguments));
            lock (instanceManagementLock)
            {
                if (!instances.TryGetValue(key, out var activeExpression))
                {
                    activeExpression = new ActiveExpression<TResult>(ActiveExpression.Create(ActiveExpression.ReplaceParameters(expression, arguments)), key);
                    instances.Add(key, activeExpression);
                }
                ++activeExpression.disposalCount;
                return activeExpression;
            }
        }

        ActiveExpression(ActiveExpression expression, (string expressionString, EquatableList<object> arguments) key)
        {
            this.expression = expression;
            fault = this.expression.Fault;
            val = (TResult)this.expression.Value;
            this.expression.PropertyChanged += ExpressionPropertyChanged;
            this.key = key;
        }

        int disposalCount;
        readonly ActiveExpression expression;
        Exception fault;
        readonly (string expressionString, EquatableList<object> arguments) key;
        TResult val;

        protected override void Dispose(bool disposing)
        {
            lock (instanceManagementLock)
            {
                if (--disposalCount > 0)
                    return;
                expression.PropertyChanged -= ExpressionPropertyChanged;
                expression.Dispose();
                instances.Remove(key);
            }
        }

        void ExpressionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Fault))
                Fault = expression.Fault;
            else if (e.PropertyName == nameof(Value))
                Value = (TResult)expression.Value;
        }

        /// <summary>
        /// Gets the exception that was thrown while evaluating the expression; <c>null</c> if there was no such exception
        /// </summary>
        public Exception Fault
        {
            get => fault;
            private set => SetBackedProperty(ref fault, in value);
        }

        /// <summary>
        /// Gets the result of evaluating the expression
        /// </summary>
        public TResult Value
        {
            get => val;
            private set => SetBackedProperty(ref val, in value);
        }
    }
}