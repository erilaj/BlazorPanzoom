using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace BlazorPanzoom
{
    public class PanzoomInterop : IPanzoom, IAsyncDisposable
    {
        private readonly IJSObjectReference _jsPanzoomReference;
        private bool _isDisposed = false;
        internal PanzoomInterop(IJSObjectReference jsPanzoomReference)
        {
            _jsPanzoomReference = jsPanzoomReference;
        }

        public IJSObjectReference JSPanzoomReference => _jsPanzoomReference;


        //public async ValueTask DisposeAsync()
        //{
        //    GC.SuppressFinalize(this);

        //    if (_jsPanzoomReference != null)
        //    {
        //        await _jsPanzoomReference.DisposeAsync();
        //        await DestroyAsync();
        //        await OnDispose.InvokeAsync();
        //    }

        //    DisposeAllEventHandlers();
        //}
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return; // Already disposed
            }
            _isDisposed = true; // Mark as disposing

            GC.SuppressFinalize(this);

            // 1. Invoke C# event handler (less likely to fail with JSObjectReference error, but good practice to protect)
            try
            {
                if (OnDispose != null) // Check if there are subscribers
                {
                    await OnDispose.InvokeAsync();
                }
            }
            catch (Exception ex)
            {
                // Log or handle C# event handler exceptions if necessary
                Console.WriteLine($"Error invoking OnDispose event: {ex.Message}");
            }

            // 2. Attempt to destroy the JS-side panzoom instance
            await DestroyAsync(); // DestroyAsync now contains its own try/catch

            // 3. Attempt to dispose the JSObjectReference itself
            try
            {
                // No need to check _jsPanzoomReference for null if constructor enforces it,
                // but the ObjectDisposedException catch handles the relevant case.
                await _jsPanzoomReference.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
                // Expected if Blazor already disposed it or if DestroyAsync failed because it was disposed.
                Console.WriteLine("PanzoomInterop: _jsPanzoomReference was already disposed when DisposeAsync tried to dispose it. Ignoring.");
            }
            catch (JSDisconnectedException)
            {
                // Expected if the circuit is disconnected (Blazor Server).
                Console.WriteLine("PanzoomInterop: JS disconnected when DisposeAsync tried to dispose _jsPanzoomReference. Ignoring.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop calls cannot be issued"))
            {
                // Another symptom of disconnection or prerendering disposal issues.
                Console.WriteLine($"PanzoomInterop: JS interop unavailable during _jsPanzoomReference.DisposeAsync: {ex.Message}. Ignoring.");
            }
            catch (Exception ex)
            {
                // Catch unexpected errors during JSObjectReference disposal
                Console.WriteLine($"PanzoomInterop: Unexpected error disposing _jsPanzoomReference: {ex.Message}");
            }

            // 4. Clean up C# event handlers (safe operation)
            DisposeAllEventHandlers();
        }
        public async ValueTask PanAsync(double x, double y, IPanOnlyOptions? overridenOptions = default)
        {
            await _jsPanzoomReference.InvokeVoidAsync("pan", x, y, overridenOptions);
        }

        public async ValueTask ZoomInAsync(IZoomOnlyOptions? options = default)
        {
            await _jsPanzoomReference.InvokeVoidAsync("zoomIn");
        }

        public async ValueTask ZoomOutAsync(IZoomOnlyOptions? options = default)
        {
            await _jsPanzoomReference.InvokeVoidAsync("zoomOut");
        }

        public async ValueTask ZoomAsync(double toScale, IZoomOnlyOptions? options = default)
        {
            await _jsPanzoomReference.InvokeVoidAsync("zoom", toScale);
        }

        public async ValueTask ZoomToPointAsync(double toScale, double clientX, double clientY,
            IZoomOnlyOptions? overridenZoomOptions = default)
        {
            await _jsPanzoomReference.InvokeVoidAsync("zoomToPoint", toScale, new PointArgs(clientX, clientY),
                overridenZoomOptions);
        }

        public async ValueTask ZoomWithWheelAsync(CustomWheelEventArgs args,
            IZoomOnlyOptions? overridenOptions = default)
        {
            var currentOptions = await GetOptionsAsync();
            var currentScale = await GetScaleAsync();
            var minScale = currentOptions.GetMinScaleOrDefault();
            var maxScale = currentOptions.GetMaxScaleOrDefault();
            var step = currentOptions.GetStepOrDefault();
            if (overridenOptions is not null)
            {
                minScale = overridenOptions.GetMinScaleOrDefault(minScale);
                maxScale = overridenOptions.GetMaxScaleOrDefault(maxScale);
                step = overridenOptions.GetStepOrDefault(step);
            }

            var delta = args.DeltaY == 0 && args.DeltaX != 0 ? args.DeltaX : args.DeltaY;
            var direction = delta < 0 ? 1 : -1;
            var calculatedScale = currentScale * Math.Exp(direction * step / 3);
            var constrainedScale = Math.Min(Math.Max(calculatedScale, minScale), maxScale);
            await ZoomToPointAsync(constrainedScale, args.ClientX, args.ClientY, overridenOptions);
        }

        public async ValueTask ResetAsync(PanzoomOptions? options = default)
        {
            await _jsPanzoomReference.InvokeVoidAsync("reset");
        }

        public async ValueTask SetOptionsAsync(PanzoomOptions options)
        {
            // TODO not allowed to set Force option
            await _jsPanzoomReference.InvokeVoidAsync("setOptions", options);
        }

        public async ValueTask<PanzoomOptions> GetOptionsAsync()
        {
            return await _jsPanzoomReference.InvokeAsync<PanzoomOptions>("getOptions");
        }

        public async ValueTask<double> GetScaleAsync()
        {
            return await _jsPanzoomReference.InvokeAsync<double>("getScale");
        }

        public async ValueTask<ReadOnlyFocalPoint> GetPanAsync()
        {
            return await _jsPanzoomReference.InvokeAsync<ReadOnlyFocalPoint>("getPan");
        }

        public async ValueTask ResetStyleAsync()
        {
            await _jsPanzoomReference.InvokeVoidAsync("resetStyle");
        }

        public async ValueTask SetStyleAsync(string name, string value)
        {
            await _jsPanzoomReference.InvokeVoidAsync("setStyle", name, value);
        }

        //public async ValueTask DestroyAsync()
        //{
        //   await _jsPanzoomReference.InvokeVoidAsync("destroy");
        //}
        public async ValueTask DestroyAsync()
        {
            // Prevent attempts if already disposed or reference is invalid
            if (_isDisposed)
            {
                return;
            }

            try
            {
                // The core JS interop call that likely caused the original error
                await _jsPanzoomReference.InvokeVoidAsync("destroy");
            }
            catch (ObjectDisposedException)
            {
                // The JSObjectReference was already disposed. This is okay during cleanup.
                Console.WriteLine("PanzoomInterop: _jsPanzoomReference was already disposed during DestroyAsync. Ignoring.");
                // Since it's disposed, mark our class state accordingly
                _isDisposed = true;
            }
            catch (JSDisconnectedException)
            {
                // The JS runtime is unavailable (e.g., SignalR connection lost).
                Console.WriteLine("PanzoomInterop: JS disconnected during DestroyAsync. Ignoring.");
                // Mark as disposed as we can't interact further
                _isDisposed = true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop calls cannot be issued"))
            {
                // Another symptom of disconnection or prerendering disposal issues.
                Console.WriteLine($"PanzoomInterop: JS interop unavailable during DestroyAsync: {ex.Message}. Ignoring.");
                _isDisposed = true;
            }
            catch (Exception ex) // Catch unexpected errors during the destroy call
            {
                Console.WriteLine($"PanzoomInterop: Error during DestroyAsync JS call: {ex.Message}");
                // Decide if you need to re-throw or just log. Logging is usually sufficient for disposal.
                // Consider marking as disposed even on generic error, as state is uncertain.
                _isDisposed = true;
            }
        }
        public event BlazorPanzoomEventHandler<CustomWheelEventArgs>? OnCustomWheel;
        public event BlazorPanzoomEventHandler<SetTransformEventArgs>? OnSetTransform;
        public event BlazorPanzoomEventHandler? OnDispose;

        [JSInvokable]
        public async ValueTask OnCustomWheelEvent(CustomWheelEventArgs args)
        {
            await OnCustomWheel.InvokeAsync(args);
        }

        [JSInvokable]
        public async ValueTask OnSetTransformEvent(SetTransformEventArgs args)
        {
            await OnSetTransform.InvokeAsync(args);
        }

        private void DisposeAllEventHandlers()
        {
            OnCustomWheel = null;
            OnSetTransform = null;
            OnDispose = null;
        }

        protected bool Equals(PanzoomInterop other)
        {
            return _jsPanzoomReference.Equals(other._jsPanzoomReference);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PanzoomInterop) obj);
        }

        public override int GetHashCode()
        {
            return _jsPanzoomReference.GetHashCode();
        }

        public static bool operator ==(PanzoomInterop? left, PanzoomInterop? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PanzoomInterop? left, PanzoomInterop? right)
        {
            return !Equals(left, right);
        }
    }
}
