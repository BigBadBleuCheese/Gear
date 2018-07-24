using System;

namespace Gear.Components
{
    /// <summary>
    /// Provides a mechanism for releasing unmanaged resources synchronously and notifying about property changes
    /// </summary>
    public abstract class SyncDisposablePropertyChangeNotifier : PropertyChangeNotifier, IDisposable
    {
        ~SyncDisposablePropertyChangeNotifier() => Dispose(false);

        readonly object disposalAccess = new object();
        bool isDisposed;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
        /// </summary>
        public void Dispose()
        {
            lock (disposalAccess)
                if (!IsDisposed)
                {
                    Dispose(true);
                    IsDisposed = true;
                    GC.SuppressFinalize(this);
                }
        }

        /// <summary>
        /// Frees, releases, or resets unmanaged resources
        /// </summary>
        /// <param name="disposing">false if invoked by the finalizer because the object is being garbage collected; otherwise, true</param>
        protected abstract void Dispose(bool disposing);

        /// <summary>
        /// Ensure the object has not been disposed
        /// </summary>
		/// <exception cref="ObjectDisposedException">The object has already been disposed</exception>
		protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Gets whether this object has been disposed
        /// </summary>
        public bool IsDisposed
        {
            get => isDisposed;
            private set => SetBackedProperty(ref isDisposed, in value);
        }
    }
}