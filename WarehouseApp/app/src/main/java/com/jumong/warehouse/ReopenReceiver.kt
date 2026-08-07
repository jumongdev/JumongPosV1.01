package com.jumong.warehouse

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/**
 * Re-launches the app automatically right after an in-app update finishes installing.
 * The package installer kills the app process during the install, so without this
 * receiver the user would be dropped back to the home screen after tapping UPDATE NOW.
 */
class ReopenReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_MY_PACKAGE_REPLACED) {
            // System is still settling the newly installed package; give it a moment.
            android.os.Handler(android.os.Looper.getMainLooper()).postDelayed({
                try {
                    val launch = context.packageManager.getLaunchIntentForPackage(context.packageName)
                    if (launch != null) {
                        launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                        context.startActivity(launch)
                    }
                } catch (e: Exception) {
                    // App icon still works — just don't crash here.
                }
            }, 500)
        }
    }
}