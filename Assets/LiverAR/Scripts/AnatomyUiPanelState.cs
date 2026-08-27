namespace LiverAR.Runtime
{
    public static class AnatomyUiPanelState
    {
        public static bool ShouldCloseDetailOverlayOnNavigationChange(bool isDetailOverlay)
        {
            return !isDetailOverlay;
        }
    }
}
