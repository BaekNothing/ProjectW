using System;
using ProjectW.Bootstrap;
using ProjectW.Contracts;
using UnityEngine;

namespace ProjectW.WebPreview
{
    // Spec: Operation/WebPreviewPublishing. Static player entry, never an Android patch loader.
    public sealed class WebPreviewBootstrap : MonoBehaviour, IPatchDiagnostics
    {
        private bool started;
        public Font UiFont;
        public string ActiveVersion => "web-preview";
        public string InstalledVersion => ActiveVersion;
        public string Status { get; private set; } = "웹 버전 시작 중";
        public string LastPatchResult => "새 버전은 페이지를 새로고침하면 적용됩니다.";
        public bool UpdateInProgress => false;
        private void Start()
        {
            StartGame(new ProjectW.HotUpdate.GameEntry());
            GetComponent<ProjectW.MilestonePrototype.MilestonePrototypeController>().WebPreviewFont = UiFont;
        }

        public void StartGame(IGameEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (started) return;
            started = true;
            entry.Start(new GameStartupContext(gameObject, ActiveVersion, string.Empty,
                new PlayerPrefsStringStorage(), this, () => Status = "실행 중"));
        }

        public bool RequestUpdate() => false;
        public PatchDiagnosticEntry[] GetLogs() => Array.Empty<PatchDiagnosticEntry>();
        public void ClearLogs() { }
    }
}
