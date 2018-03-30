﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Gear.Components
{
    /// <summary>
    /// Provides a mechanism for notifying about property changes
    /// </summary>
    public abstract class PropertyChangeNotifier : INotifyPropertyChanged, INotifyPropertyChanging
    {
        /// <summary>
        /// Occurs when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Occurs when a property value is changing
        /// </summary>
        public event PropertyChangingEventHandler PropertyChanging;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event
        /// </summary>
        /// <param name="e">The arguments of the event</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            PropertyChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Notifies that a property changed
        /// </summary>
        /// <param name="propertyName">The name of the property that changed</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanging"/> event
        /// </summary>
        /// <param name="e">The arguments of the event</param>
        protected virtual void OnPropertyChanging(PropertyChangingEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            PropertyChanging?.Invoke(this, e);
        }

        /// <summary>
        /// Notifies that a property is changing
        /// </summary>
        /// <param name="propertyName">The name of the property that is changing</param>
        protected void OnPropertyChanging([CallerMemberName] string propertyName = null)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));
            OnPropertyChanging(new PropertyChangingEventArgs(propertyName));
        }

        /// <summary>
        /// Compares a property's backing field and a new value for inequality, and when they are unequal, raises the <see cref="PropertyChanging"/> event, sets the backing field to the new value, and then raises the <see cref="PropertyChanged"/> event
        /// </summary>
        /// <typeparam name="TValue">The type of the property</typeparam>
        /// <param name="backingField">A reference to the backing field of the property</param>
        /// <param name="value">The new value</param>
        /// <param name="propertyName">The name of the property</param>
        /// <returns><see cref="true"/> if <paramref name="backingField"/> was unequal to <paramref name="value"/>; otherwise, <see cref="false"/></returns>
        protected bool SetBackedProperty<TValue>(ref TValue backingField, in TValue value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<TValue>.Default.Equals(backingField, value))
            {
                OnPropertyChanging(propertyName);
                backingField = value;
                OnPropertyChanged(propertyName);
                return true;
            }
            return false;
        }
    }
}