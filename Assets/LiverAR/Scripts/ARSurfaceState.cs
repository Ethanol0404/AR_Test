namespace LiverAR.Runtime
{
    public enum ARSurfaceState
    {
        Initialising,
        Searching,
        SurfaceDetected,
        Ready,
        VirtualMode,
        VirtualEnvironment,
        Placed,
        TrackingLimited,
        Unsupported
    }

    public static class ARSurfaceStatusMessage
    {
        public static string GetMessage(ARSurfaceState state)
        {
            switch (state)
            {
                case ARSurfaceState.Initialising:
                    return "Starting AR...";
                case ARSurfaceState.Searching:
                    return "Move your phone slowly to detect a flat surface.";
                case ARSurfaceState.SurfaceDetected:
                    return "Surface detected. Tap Place Liver.";
                case ARSurfaceState.Ready:
                    return "Surface detected. Tap Place Liver.";
                case ARSurfaceState.VirtualMode:
                    return "Virtual surface ready. Tap Place Liver.";
                case ARSurfaceState.VirtualEnvironment:
                    return "Virtual environment active.";
                case ARSurfaceState.TrackingLimited:
                    return "Tracking limited - move slowly.";
                case ARSurfaceState.Unsupported:
                    return "AR is unavailable on this device.";
                case ARSurfaceState.Placed:
                default:
                    return string.Empty;
            }
        }
    }
}
