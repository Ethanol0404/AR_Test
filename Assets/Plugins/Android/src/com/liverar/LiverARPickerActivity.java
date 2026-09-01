package com.liverar;

import android.app.Activity;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

public final class LiverARPickerActivity extends Activity {
    private static final int REQUEST_CODE = 41731;
    private String destination;

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        destination = getIntent().getStringExtra("destination");
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_CODE);
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == REQUEST_CODE && resultCode == RESULT_OK && data != null) {
            try {
                Uri tree = data.getData();
                getContentResolver().takePersistableUriPermission(tree, Intent.FLAG_GRANT_READ_URI_PERMISSION);
                File target = new File(destination, "patient-import");
                delete(target);
                LiverARFilePicker.setPendingStatus("Copying patient GLB and metadata...");
                copyRequiredFiles(tree, target, false);
                LiverARFilePicker.setPendingStatus("Patient folder copied. Loading model...");
                LiverARFilePicker.setPendingPath(target.getAbsolutePath());
            } catch (Exception exception) {
                LiverARFilePicker.setPendingStatus("Patient folder import failed: " + exception.getMessage());
            }
        }
        finish();
    }

    private void copyTree(Uri tree, File destination) throws Exception {
        destination.mkdirs();
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, DocumentsContract.getTreeDocumentId(tree));
        Cursor cursor = getContentResolver().query(children, new String[] { DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME, DocumentsContract.Document.COLUMN_MIME_TYPE }, null, null, null);
        if (cursor == null) throw new IllegalStateException("Could not read selected folder.");
        try {
            while (cursor.moveToNext()) {
                Uri document = DocumentsContract.buildDocumentUriUsingTree(tree, cursor.getString(0));
                File target = new File(destination, cursor.getString(1));
                if (DocumentsContract.Document.MIME_TYPE_DIR.equals(cursor.getString(2))) copyTree(document, target);
                else copyFile(document, target);
            }
        } finally { cursor.close(); }
    }

    private void copyRequiredFiles(Uri tree, File destination, boolean fromSubfolder) throws Exception {
        destination.mkdirs();
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, DocumentsContract.getTreeDocumentId(tree));
        Cursor cursor = getContentResolver().query(children, new String[] { DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME, DocumentsContract.Document.COLUMN_MIME_TYPE }, null, null, null);
        if (cursor == null) throw new IllegalStateException("Could not read selected folder.");
        try {
            while (cursor.moveToNext()) {
                String name = cursor.getString(1);
                if ("patient.glb".equalsIgnoreCase(name) || "metadata.json".equalsIgnoreCase(name)) {
                    LiverARFilePicker.setPendingStatus("Copying " + name + "...");
                    Uri document = DocumentsContract.buildDocumentUriUsingTree(tree, cursor.getString(0));
                    copyFile(document, new File(destination, name));
                } else if (DocumentsContract.Document.MIME_TYPE_DIR.equals(cursor.getString(2))) {
                    Uri document = DocumentsContract.buildDocumentUriUsingTree(tree, cursor.getString(0));
                    copyRequiredFiles(document, destination, true);
                }
            }
        } finally { cursor.close(); }
    }

    private void copyFile(Uri source, File destination) throws Exception {
        InputStream input = getContentResolver().openInputStream(source);
        if (input == null) throw new IllegalStateException("Could not open selected file.");
        destination.getParentFile().mkdirs();
        FileOutputStream output = new FileOutputStream(destination);
        try {
            byte[] buffer = new byte[8192];
            int count;
            while ((count = input.read(buffer)) >= 0) output.write(buffer, 0, count);
        } finally { input.close(); output.close(); }
    }

    private static void delete(File file) {
        if (!file.exists()) return;
        if (file.isDirectory()) for (File child : file.listFiles()) delete(child);
        file.delete();
    }
}
