using UnityEngine;

namespace LiverAR.Runtime
{
    public static class AndroidFolderPicker
    {
        public static bool TryPickFolder(string destinationPath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var picker = new AndroidJavaClass("com.liverar.LiverARFilePicker"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                picker.CallStatic("pickFolder", activity, destinationPath);
                return true;
            }
#else
            Debug.Log("Android folder picker is only available in an Android build.");
            return false;
#endif
        }

        public static bool TryConsumePickedFolder(out string path)
        {
            path = string.Empty;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var picker = new AndroidJavaClass("com.liverar.LiverARFilePicker"))
            {
                path = picker.CallStatic<string>("consumePickedFolder") ?? string.Empty;
                return !string.IsNullOrWhiteSpace(path);
            }
#else
            return false;
#endif
        }

        public static bool TryConsumePickerStatus(out string status)
        {
            status = string.Empty;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var picker = new AndroidJavaClass("com.liverar.LiverARFilePicker"))
            {
                status = picker.CallStatic<string>("consumePickerStatus") ?? string.Empty;
                return !string.IsNullOrWhiteSpace(status);
            }
#else
            return false;
#endif
        }
    }
}
