using System;

namespace Gear.ActiveQuery
{
    /// <summary>
    /// Provides data for the <see cref="INotifyElementFaultChanges.ElementFaultChanged"/> and <see cref="INotifyElementFaultChanges.ElementFaultChanging"/> events
    /// </summary>
    public class ElementFaultChangeEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ElementFaultChangeEventArgs"/> class for an element that only appears once in the sequence
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="fault">The fault associated with the element</param>
        public ElementFaultChangeEventArgs(object element, Exception fault)
        {
            Element = element;
            Fault = fault;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ElementFaultChangeEventArgs"/> class
        /// </summary>
        /// <param name="element">The element</param>
        /// <param name="fault">The fault associated with the element</param>
        /// <param name="count">The number of times the element appears in the sequence</param>
        public ElementFaultChangeEventArgs(object element, Exception fault, int count) : this(element, fault) => Count = count;

        /// <summary>
        /// Gets the number of times the element appears in the sequence
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the element
        /// </summary>
        public object Element { get; }

        /// <summary>
        /// Gets the fault associated with the element
        /// </summary>
        public Exception Fault { get; }
    }
}
