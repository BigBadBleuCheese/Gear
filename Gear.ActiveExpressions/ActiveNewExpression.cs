using Gear.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

namespace Gear.ActiveExpressions
{
    class ActiveNewExpression : ActiveExpression
    {
        static readonly object instanceManagementLock = new object();
        static readonly Dictionary<(Type type, EquatableList<ActiveExpression> arguments), ActiveNewExpression> instances = new Dictionary<(Type type, EquatableList<ActiveExpression> arguments), ActiveNewExpression>();

        public static ActiveNewExpression Create(NewExpression newExpression)
        {
            var type = newExpression.Type;
            var arguments = new EquatableList<ActiveExpression>(newExpression.Arguments.Select(argument => Create(argument)).ToList());
            var key = (type, arguments);
            lock (instanceManagementLock)
            {
                if (!instances.TryGetValue(key, out var activeNewExpression))
                {
                    activeNewExpression = new ActiveNewExpression(type, arguments);
                    instances.Add(key, activeNewExpression);
                }
                ++activeNewExpression.disposalCount;
                return activeNewExpression;
            }
        }

        ActiveNewExpression(Type type, EquatableList<ActiveExpression> arguments) : base(type, ExpressionType.New)
        {
            this.arguments = arguments;
            foreach (var argument in this.arguments)
                argument.PropertyChanged += ArgumentPropertyChanged;
            Evaluate();
        }

        readonly EquatableList<ActiveExpression> arguments;
        int disposalCount;

        void ArgumentPropertyChanged(object sender, PropertyChangedEventArgs e) => Evaluate();

        protected override void Dispose(bool disposing)
        {
            lock (instanceManagementLock)
            {
                if (--disposalCount > 0)
                    return;
                foreach (var argument in arguments)
                {
                    argument.PropertyChanged -= ArgumentPropertyChanged;
                    argument.Dispose();
                }
                instances.Remove((Type, arguments));
            }
        }

        void Evaluate()
        {
            try
            {
                var argumentFault = arguments.Select(argument => argument.Fault).Where(fault => fault != null).FirstOrDefault();
                if (argumentFault != null)
                    Fault = argumentFault;
                else
                    Value = Activator.CreateInstance(Type, arguments.Select(argument => argument.Value).ToArray());
            }
            catch (Exception ex)
            {
                Fault = ex;
            }
        }
    }
}