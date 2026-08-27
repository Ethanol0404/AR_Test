namespace LiverAR.Runtime
{
    public static class AnatomyGestureClassifier
    {
        public static AnatomyGestureState ClassifySingleTouch(
            float elapsedSeconds,
            float movementPixels,
            float dragThresholdPixels,
            float longPressSeconds,
            float longPressTolerancePixels,
            bool startedOnPart)
        {
            if (movementPixels >= dragThresholdPixels)
                return AnatomyGestureState.Dragging;

            if (startedOnPart && elapsedSeconds >= longPressSeconds && movementPixels <= longPressTolerancePixels)
                return AnatomyGestureState.LongPressTriggered;

            return startedOnPart ? AnatomyGestureState.LongPressPending : AnatomyGestureState.PossibleTap;
        }
    }
}
