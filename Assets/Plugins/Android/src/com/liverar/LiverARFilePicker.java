package com.liverar;

import android.app.Activity;
import android.content.Intent;

public final class LiverARFilePicker {
    private static String pendingPath = "";
    private static String pendingStatus = "";

    public static void pickFolder(Activity activity, String destination) {
        Intent intent = new Intent(activity, LiverARPickerActivity.class);
        intent.putExtra("destination", destination);
        activity.startActivity(intent);
    }

    public static synchronized String consumePickedFolder() {
        String result = pendingPath;
        pendingPath = "";
        return result;
    }

    public static synchronized String consumePickerStatus() {
        String result = pendingStatus;
        pendingStatus = "";
        return result;
    }

    static synchronized void setPendingPath(String path) { pendingPath = path == null ? "" : path; }
    static synchronized void setPendingStatus(String status) { pendingStatus = status == null ? "" : status; }
}
