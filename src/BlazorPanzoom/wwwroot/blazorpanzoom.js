class BlazorPanzoomInterop {

    constructor() {
    }

    fire(dispatcher, eventName, args) {
        const name = "On" + eventName + "Event"
        dispatcher.invokeMethodAsync(name, args)
    }

    createPanzoomForReference(element, options) {
        const panzoom = Panzoom(element, options)
        panzoom.boundElement = element
        return panzoom
    }

    createPanzoomForSelector(selector, options) {
        try {
            const elements = document.querySelectorAll(selector)
            const array = []
            for (let i = 0; i < elements.length; i++) {
                const element = elements[i]
                array.push(DotNet.createJSObjectReference(this.createPanzoomForReference(element, options)))
            }
            return array
        } catch {
            throw new Error(`Cannot create a Panzoom object from selectors!`);
        }
    }

    performForAllPanzoom(functionName, panzoomList, args) {
        if (!panzoomList) {
            return
        }

        for (let i = 0; i < panzoomList.length; i++) {
            const ref = panzoomList[i]
            ref[functionName](args)
        }
    }

    registerSetTransform(dotnetReference, panzoom) {
        if (!panzoom) {
            return
        }
        const opts = {
            setTransform: (elem, {x, y, scale, isSVG}) => {
                this.fire(dotnetReference, 'SetTransform', {x, y, scale, isSVG})
            }
        }
        panzoom.setOptions(opts)
    }

    registerZoomWithWheel(panzoom, element) {
        // Ensure panzoom and its boundElement exist if element is not provided
        const targetElement = element || (panzoom ? panzoom.boundElement : null);
        if (!targetElement) {
            console.warn("registerZoomWithWheel: Target element not found.");
            return;
        }

        const parent = targetElement.parentElement;
        if (parent) { // Only add listener if parent exists
            parent.addEventListener('wheel', panzoom.zoomWithWheel);
        } else {
            console.warn("registerZoomWithWheel: Target element has no parentElement.");
        }
    }

    registerWheelListener(dotnetReference, panzoom, element) {
        // Ensure panzoom and its boundElement exist if element is not provided
        const targetElement = element || (panzoom ? panzoom.boundElement : null);
        if (!targetElement) {
            console.warn("registerWheelListener: Target element not found.");
            return;
        }

        const parent = targetElement.parentElement;
        if (parent) { // Only add listener if parent exists
            // Ensure boundWheelListener doesn't already exist or clean up previous if necessary
            if (panzoom.boundWheelListener) {
                parent.removeEventListener('wheel', panzoom.boundWheelListener);
            }
            panzoom.boundWheelListener = this.wheelHandler.bind(this, dotnetReference);
            parent.addEventListener('wheel', panzoom.boundWheelListener);
        } else {
            console.warn("registerWheelListener: Target element has no parentElement.");
        }
    }


    wheelHandler(dotnetReference, event) {
        event.preventDefault()
        this.fire(dotnetReference, 'CustomWheel', {
            deltaX: event.deltaX,
            deltaY: event.deltaY,
            clientX: event.clientX,
            clientY: event.clientY,
            shiftKey: event.shiftKey,
            altKey: event.altKey
        })
    }

    removeZoomWithWheel(panzoom, element) {
        // Determine the target element whose parent we need
        const targetElement = element || (panzoom ? panzoom.boundElement : null);

        // Check if we have a valid target element AND the panzoom instance itself
        if (!targetElement || !panzoom) {
            // console.warn("removeZoomWithWheel: Could not find target element or panzoom instance. Skipping removeEventListener.");
            return; // Silently exit if elements are gone during cleanup
        }

        // Check if the target element has a parentElement
        const parent = targetElement.parentElement;
        if (!parent) {
            // console.warn("removeZoomWithWheel: Target element does not have a parentElement. Skipping removeEventListener.");
            return; // Silently exit
        }

        // If we have a valid parent, remove the listener
        // Check if zoomWithWheel function actually exists on the panzoom object
        if (typeof panzoom.zoomWithWheel === 'function') {
            parent.removeEventListener('wheel', panzoom.zoomWithWheel);
        }
    }

    removeWheelListener(panzoom, element) {
        // Determine the target element whose parent we need
        const targetElement = element || (panzoom ? panzoom.boundElement : null);

        // Check if we have a valid target element AND the panzoom instance itself
        if (!targetElement || !panzoom) {
            // console.warn("removeWheelListener: Could not find target element or panzoom instance. Skipping removeEventListener.");
            return; // Silently exit
        }

        // Check if the target element has a parentElement
        const parent = targetElement.parentElement;
        if (!parent) {
            // console.warn("removeWheelListener: Target element does not have a parentElement. Skipping removeEventListener.");
            return; // Silently exit
        }

        // Check if the specific bound listener exists before trying to remove it
        if (panzoom && typeof panzoom.boundWheelListener === 'function') {
            parent.removeEventListener('wheel', panzoom.boundWheelListener);
            // It's good practice to delete the reference after removing the listener
            delete panzoom.boundWheelListener;
        } else {
            // console.warn("removeWheelListener: panzoom.boundWheelListener not found or not a function. Skipping removeEventListener.");
        }
    }

    destroyPanzoom(panzoom) {
        // This is likely called by the main panzoom destroy or your C# code
        // It's okay to delete boundElement here, as the remove listener functions above should now handle it being missing.
        if (panzoom) {
            // You might also want to explicitly call the remove listener functions here IF they weren't called from C#
            // this.removeZoomWithWheel(panzoom, null); // Example, might cause double removal if also called from C#
            // this.removeWheelListener(panzoom, null); // Example

            // Clean up the panzoom instance itself (assuming the library provides a destroy method)
            if (typeof panzoom.destroy === 'function') {
                panzoom.destroy();
            }

            // Then delete custom properties
            delete panzoom.boundElement;
            delete panzoom.boundWheelListener; // Ensure this is cleaned up too
        }
    }
}

window.blazorPanzoom = new BlazorPanzoomInterop()
