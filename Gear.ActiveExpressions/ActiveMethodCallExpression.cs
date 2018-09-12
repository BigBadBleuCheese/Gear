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
        static readonly Dictionary<(ActiveExpression @object, MethodInfo method, EquatableList<ActiveExpression> arguments), ActiveMethodCallExpression> instanceInstances = new Dictionary<(ActiveExpression @object, MethodInfo method, EquatableList<ActiveExpression> arguments), ActiveMethodCallExpression>();
        static readonly Dictionary<(MethodInfo method, EquatableList<ActiveExpression> arguments), ActiveMethodCallExpression> staticInstances = new Dictionary<(MethodInfo method, EquatableList<ActiveExpression> arguments), ActiveMethodCallExpression>();

        public static ActiveMethodCallExpression Create(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Object == null)
            {
                var method = methodCallExpression.Method;
                var arguments = new EquatableList<ActiveExpression>(methodCallExpression.Arguments.Select(argument => Create(argument)).ToList());
                var key = (method, arguments);
                lock (instanceManagementLock)
                {
                    if (!staticInstances.TryGetValue(key, out var activeMethodCallExpression))
                    {
                        activeMethodCallExpression = new ActiveMethodCallExpression(methodCallExpression.Type, method, arguments);
                        staticInstances.Add(key, activeMethodCallExpression);
                    }
                    ++activeMethodCallExpression.disposalCount;
                    return activeMethodCallExpression;
                }
            }
            else
            {
                var @object = Create(methodCallExpression.Object);
                var method = methodCallExpression.Method;
                var arguments = new EquatableList<ActiveExpression>(methodCallExpression.Arguments.Select(argument => Create(argument)).ToList());
                var key = (@object, method, arguments);
                lock (instanceManagementLock)
                {
                    if (!instanceInstances.TryGetValue(key, out var activeMethodCallExpression))
                    {
                        activeMethodCallExpression = new ActiveMethodCallExpression(methodCallExpression.Type, @object, method, arguments);
                        instanceInstances.Add(key, activeMethodCallExpression);
                    }
                    ++activeMethodCallExpression.disposalCount;
                    return activeMethodCallExpression;
                }
            }
        }

        ActiveMethodCallExpression(Type type, ActiveExpression @object, MethodInfo method, EquatableList<ActiveExpression> arguments) : base(type, ExpressionType.Call)
        {
            this.method = method;
            fastMethod = GetFastMethodInfo(this.method);
            this.@object = @object;
            this.@object.PropertyChanged += ObjectPropertyChanged;
            this.arguments = arguments;
            foreach (var argument in this.arguments)
                argument.PropertyChanged += ArgumentPropertyChanged;
            Evaluate();
        }

        ActiveMethodCallExpression(Type type, MethodInfo method, EquatableList<ActiveExpression> arguments) : base(type, ExpressionType.Call)
        {
            this.method = method;
            fastMethod = GetFastMethodInfo(this.method);
            this.arguments = arguments;
            foreach (var argument in this.arguments)
                argument.PropertyChanged += ArgumentPropertyChanged;
            Evaluate();
        }

        readonly EquatableList<ActiveExpression> arguments;
        int disposalCount;
        readonly FastMethodInfo fastMethod;
        readonly MethodInfo method;
        readonly ActiveExpression @object;

        void ArgumentPropertyChanged(object sender, PropertyChangedEventArgs e) => Evaluate();

        protected override void Dispose(bool disposing)
        {
            lock (instanceManagementLock)
            {
                if (--disposalCount > 0)
                    return;
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
                instanceInstances.Remove((@object, method, arguments));
            }
        }

        void Evaluate()
        {
            try
            {
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

        void ObjectPropertyChanged(object sender, PropertyChangedEventArgs e) => Evaluate();
    }
}