using System.Collections;
using System.Collections.Generic;
using ProjectW.IngameCore.CaseReview;
using UnityEditor;
using UnityEngine;

namespace ProjectW.Editor
{
public static class RemoteSpreadsheetDataEditor
{
    private const string MenuPath = "Tools/ProjectW/Case Review/Sync Google Sheet Data";
    private static EditorCoroutineRunner runner;

    [MenuItem(MenuPath)]
    public static void SyncGoogleSheetData()
    {
        if (runner != null)
        {
            return;
        }

        Debug.Log("ProjectW Google Sheets sync started.");
        runner = new EditorCoroutineRunner(RemoteSpreadsheetData.Sync(OnSyncCompleted));
        runner.Start();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateSyncGoogleSheetData()
    {
        return runner == null;
    }

    private static void OnSyncCompleted(RemoteSpreadsheetSyncResult result)
    {
        runner?.Stop();
        runner = null;

        if (result.Success)
        {
            Debug.Log($"ProjectW Google Sheets sync complete. {result.Message}");
            EditorUtility.DisplayDialog(
                "ProjectW Google Sheets",
                $"Sync complete.\n\n{result.Message}",
                "OK");
            return;
        }

        Debug.LogError($"ProjectW Google Sheets sync failed. {result.Message}");
        EditorUtility.DisplayDialog(
            "ProjectW Google Sheets",
            $"Sync failed.\n\n{result.Message}",
            "OK");
    }

    private sealed class EditorCoroutineRunner
    {
        private readonly Stack<IEnumerator> stack = new();
        private object currentYield;

        public EditorCoroutineRunner(IEnumerator routine)
        {
            stack.Push(routine);
        }

        public void Start()
        {
            EditorApplication.update += Update;
        }

        public void Stop()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (currentYield is AsyncOperation operation && !operation.isDone)
            {
                return;
            }

            currentYield = null;
            while (stack.Count > 0)
            {
                var routine = stack.Peek();
                if (!routine.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (routine.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                currentYield = routine.Current;
                return;
            }

            Stop();
            runner = null;
        }
    }
}
}
