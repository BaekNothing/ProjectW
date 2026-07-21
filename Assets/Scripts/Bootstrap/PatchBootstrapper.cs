using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HybridCLR;
using ProjectW.Contracts;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectW.Bootstrap
{
    public sealed class PatchBootstrapper : MonoBehaviour
    {
        public const int BaseVersion = 1;
        public const string DefaultChannelUrl =
            "https://raw.githubusercontent.com/BaekNothing/ProjectW/ai-integration/PatchChannels/dev.json";

        private const string EmbeddedVersion = "embedded";
        private const string EmbeddedDllName = "ProjectW.HotUpdate.dll.bytes";
        private const string PendingMarkerName = "boot-pending";
        private const string ManifestName = "patch-manifest.json";

        [SerializeField] private string channelUrl = DefaultChannelUrl;
        [SerializeField] private int requestTimeoutSeconds = 20;

        private string patchRoot;
        private string currentPath;
        private string previousPath;
        private string stagingPath;
        private string pendingMarkerPath;
        private string status = "패치 시스템을 시작하는 중...";
        private bool gameStarted;
        private string activeVersion = "none";
        private readonly object logLock = new object();
        private readonly List<DiagnosticLog> diagnosticLogs = new List<DiagnosticLog>();
        private Vector2 diagnosticScroll;
        private bool diagnosticsExpanded;
        private GUIStyle diagnosticHeader;
        private GUIStyle diagnosticBody;
        private GUIStyle diagnosticError;

        private void Awake()
        {
            Application.logMessageReceivedThreaded += CaptureLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= CaptureLog;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            InitializePaths();
            RecoverInterruptedBoot();

#if !UNITY_EDITOR
            yield return TryInstallRemotePatch();
#endif

            if (TryStartInstalledPatch()) yield break;
            yield return StartEmbeddedPatch();
        }

        private void InitializePaths()
        {
            patchRoot = Path.Combine(Application.persistentDataPath, "patches");
            currentPath = Path.Combine(patchRoot, "current");
            previousPath = Path.Combine(patchRoot, "previous");
            stagingPath = Path.Combine(patchRoot, "staging");
            pendingMarkerPath = Path.Combine(patchRoot, PendingMarkerName);
            Directory.CreateDirectory(patchRoot);
        }

        private void RecoverInterruptedBoot()
        {
            if (!File.Exists(pendingMarkerPath) || !Directory.Exists(previousPath)) return;

            status = "이전 패치의 시작 실패를 감지하여 롤백합니다.";
            string failedPath = Path.Combine(patchRoot, $"failed-{DateTime.UtcNow:yyyyMMddHHmmss}");
            if (Directory.Exists(currentPath)) Directory.Move(currentPath, failedPath);
            Directory.Move(previousPath, currentPath);
            File.Delete(pendingMarkerPath);
        }

        private IEnumerator TryInstallRemotePatch()
        {
            status = "업데이트 채널 확인 중...";
            string channelJson = null;
            yield return DownloadText(AddCacheBuster(channelUrl, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), value => channelJson = value);
            if (string.IsNullOrWhiteSpace(channelJson)) yield break;

            PatchChannel channel;
            try { channel = JsonUtility.FromJson<PatchChannel>(channelJson); }
            catch (Exception exception)
            {
                Debug.LogWarning($"Patch channel parse failed: {exception.Message}");
                yield break;
            }

            if (channel == null || channel.schemaVersion != 1 || string.IsNullOrWhiteSpace(channel.manifestUrl))
            {
                Debug.LogWarning("Patch channel is invalid or unsupported.");
                yield break;
            }

            string manifestJson = null;
            yield return DownloadText(channel.manifestUrl, value => manifestJson = value);
            if (string.IsNullOrWhiteSpace(manifestJson)) yield break;

            PatchManifest manifest;
            try { manifest = JsonUtility.FromJson<PatchManifest>(manifestJson); }
            catch (Exception exception)
            {
                Debug.LogWarning($"Patch manifest parse failed: {exception.Message}");
                yield break;
            }

            if (!ValidateManifest(manifest)) yield break;
            PatchManifest installed = ReadManifest(currentPath);
            if (installed != null && string.CompareOrdinal(installed.patchVersion, manifest.patchVersion) >= 0) yield break;

            status = $"패치 {manifest.patchVersion} 다운로드 중...";
            RecreateDirectory(stagingPath);
            bool succeeded = true;
            foreach (PatchFile file in manifest.files)
            {
                string destination = Path.Combine(stagingPath, file.name);
                bool downloaded = false;
                yield return DownloadFile(file.url, destination, value => downloaded = value);
                if (!downloaded || !VerifyFile(destination, file))
                {
                    succeeded = false;
                    break;
                }
            }

            if (!succeeded)
            {
                status = "패치 검증 실패. 기존 버전으로 시작합니다.";
                RecreateDirectory(stagingPath);
                yield break;
            }

            File.WriteAllText(Path.Combine(stagingPath, ManifestName), manifestJson);
            PromoteStaging();
        }

        private bool ValidateManifest(PatchManifest manifest)
        {
            if (manifest == null || manifest.schemaVersion != 1 || !IsValidPatchVersion(manifest.patchVersion) ||
                manifest.minBaseVersion > BaseVersion || string.IsNullOrWhiteSpace(manifest.entryAssembly) ||
                string.IsNullOrWhiteSpace(manifest.entryType) || manifest.files == null)
                return false;

            foreach (PatchFile file in manifest.files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.name) || file.name != Path.GetFileName(file.name) ||
                    string.IsNullOrWhiteSpace(file.url) || string.IsNullOrWhiteSpace(file.sha256) || file.size < 1)
                    return false;
            }
            return manifest.files.Any(file => file.role == "hotUpdateAssembly" && file.name == manifest.entryAssembly + ".bytes");
        }

        private void PromoteStaging()
        {
            if (Directory.Exists(previousPath)) Directory.Delete(previousPath, true);
            if (Directory.Exists(currentPath)) Directory.Move(currentPath, previousPath);
            Directory.Move(stagingPath, currentPath);
            status = "새 패치 설치 완료.";
        }

        private bool TryStartInstalledPatch()
        {
            PatchManifest manifest = ReadManifest(currentPath);
            if (!ValidateManifest(manifest)) return false;

            try
            {
                LoadAotMetadata(currentPath, manifest);
                PatchFile code = manifest.files.First(file => file.role == "hotUpdateAssembly");
                byte[] dllBytes = File.ReadAllBytes(Path.Combine(currentPath, code.name));
                StartAssembly(dllBytes, manifest.entryAssembly, manifest.entryType,
                    manifest.patchVersion, currentPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                status = "새 패치 실행 실패. 내장 버전으로 복구합니다.";
                return false;
            }
        }

        private static void LoadAotMetadata(string directory, PatchManifest manifest)
        {
#if !UNITY_EDITOR
            foreach (PatchFile file in manifest.files.Where(file => file.role == "aotMetadata"))
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(directory, file.name));
                LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
                if (result != LoadImageErrorCode.OK && result != LoadImageErrorCode.HOMOLOGOUS_ASSEMBLY_HAS_LOADED)
                    throw new InvalidOperationException($"AOT metadata load failed for {file.name}: {result}");
            }
#endif
        }

        private IEnumerator StartEmbeddedPatch()
        {
            status = "APK 내장 버전 시작 중...";
#if UNITY_EDITOR
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetName().Name == "ProjectW.HotUpdate");
            if (assembly == null) throw new InvalidOperationException("ProjectW.HotUpdate is not loaded in the Editor.");
            StartEntry(assembly, "ProjectW.HotUpdate.GameEntry", EmbeddedVersion, string.Empty);
#else
            string metadataRoot = Path.Combine(Application.streamingAssetsPath, "HotUpdate", "AotMetadata");
            foreach (string metadataName in new[] { "mscorlib.dll.bytes", "System.dll.bytes", "System.Core.dll.bytes" })
            {
                string metadataSource = Path.Combine(metadataRoot, metadataName);
                using (UnityWebRequest metadataRequest = UnityWebRequest.Get(metadataSource))
                {
                    metadataRequest.timeout = requestTimeoutSeconds;
                    yield return metadataRequest.SendWebRequest();
                    if (metadataRequest.result != UnityWebRequest.Result.Success) continue;
                    LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(
                        metadataRequest.downloadHandler.data, HomologousImageMode.SuperSet);
                    if (result != LoadImageErrorCode.OK && result != LoadImageErrorCode.HOMOLOGOUS_ASSEMBLY_HAS_LOADED)
                        Debug.LogWarning($"Embedded AOT metadata load failed for {metadataName}: {result}");
                }
            }

            string source = Path.Combine(Application.streamingAssetsPath, "HotUpdate", EmbeddedDllName);
            byte[] bytes = null;
            using (UnityWebRequest request = UnityWebRequest.Get(source))
            {
                request.timeout = requestTimeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) bytes = request.downloadHandler.data;
                else Debug.LogError($"Embedded patch read failed: {request.error}");
            }
            if (bytes == null || bytes.Length == 0) yield break;
            StartAssembly(bytes, "ProjectW.HotUpdate", "ProjectW.HotUpdate.GameEntry", EmbeddedVersion, string.Empty);
#endif
            yield break;
        }

        private void StartAssembly(byte[] bytes, string assemblyName, string entryType, string version, string dataPath)
        {
            Assembly assembly = Assembly.Load(bytes);
            if (assembly.GetName().Name != assemblyName)
                throw new InvalidOperationException($"Expected assembly {assemblyName}, got {assembly.GetName().Name}.");
            StartEntry(assembly, entryType, version, dataPath);
        }

        private void StartEntry(Assembly assembly, string entryType, string version, string dataPath)
        {
            Type type = assembly.GetType(entryType, true);
            if (!(Activator.CreateInstance(type) is IGameEntry entry))
                throw new InvalidOperationException($"{entryType} must implement IGameEntry.");

            activeVersion = version;
            File.WriteAllText(pendingMarkerPath, version);
            entry.Start(new GameStartupContext(gameObject, version, dataPath, MarkHealthy));
        }

        private void MarkHealthy()
        {
            if (File.Exists(pendingMarkerPath)) File.Delete(pendingMarkerPath);
            gameStarted = true;
            status = "실행 중";
        }

        private static PatchManifest ReadManifest(string directory)
        {
            string path = Path.Combine(directory, ManifestName);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<PatchManifest>(File.ReadAllText(path)); }
            catch { return null; }
        }

        private IEnumerator DownloadText(string url, Action<string> complete)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = requestTimeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) complete(request.downloadHandler.text);
                else Debug.LogWarning($"Patch request failed ({request.responseCode}, {url}): {request.error}");
            }
        }

        private IEnumerator DownloadFile(string url, string destination, Action<bool> complete)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = requestTimeoutSeconds;
                request.downloadHandler = new DownloadHandlerFile(destination) { removeFileOnAbort = true };
                yield return request.SendWebRequest();
                bool success = request.result == UnityWebRequest.Result.Success;
                if (!success) Debug.LogWarning($"Patch file request failed ({request.responseCode}, {url}): {request.error}");
                complete(success);
            }
        }

        private static bool VerifyFile(string path, PatchFile expected)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != expected.size) return false;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                return string.Equals(actual, expected.sha256, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsValidPatchVersion(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 12 || value[8] != '-') return false;
            for (int index = 0; index < value.Length; index++)
            {
                if (index == 8) continue;
                if (value[index] < '0' || value[index] > '9') return false;
            }
            return true;
        }

        public static string AddCacheBuster(string url, long nonce)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            return $"{url}{(url.Contains("?") ? "&" : "?")}projectw_nocache={nonce}";
        }

        private static void RecreateDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }

        private void OnGUI()
        {
            EnsureDiagnosticStyles();
            GUI.depth = -1000;
            GUI.matrix = Matrix4x4.identity;

            DiagnosticLog[] logs;
            lock (logLock) logs = diagnosticLogs.ToArray();
            string installed = ReadManifest(currentPath)?.patchVersion ?? "none";
            float width = Mathf.Min(Screen.width - 24, 920);

            GUILayout.BeginArea(new Rect(12, 10, width, Screen.height - 20));
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"PATCH  active:{activeVersion}  installed:{installed}  state:{status}", diagnosticHeader);
            GUILayout.FlexibleSpace();
            if (logs.Length > 0 && GUILayout.Button(diagnosticsExpanded ? "로그 닫기" : $"로그 {logs.Length}", GUILayout.Width(92)))
                diagnosticsExpanded = !diagnosticsExpanded;
            GUILayout.EndHorizontal();

            if (diagnosticsExpanded && logs.Length > 0)
            {
                float logHeight = Mathf.Min(Screen.height * .45f, 420);
                diagnosticScroll = GUILayout.BeginScrollView(diagnosticScroll, GUI.skin.box, GUILayout.Height(logHeight));
                foreach (DiagnosticLog log in logs)
                {
                    GUIStyle style = log.Type == LogType.Error || log.Type == LogType.Exception || log.Type == LogType.Assert
                        ? diagnosticError : diagnosticBody;
                    GUILayout.Label($"[{log.Type}] {log.Message}\n{log.StackTrace}", style);
                }
                GUILayout.EndScrollView();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("로그 지우기", GUILayout.Width(110)))
                {
                    lock (logLock) diagnosticLogs.Clear();
                    diagnosticsExpanded = false;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            lock (logLock)
            {
                diagnosticLogs.Add(new DiagnosticLog(type, message, stackTrace));
                if (diagnosticLogs.Count > 40) diagnosticLogs.RemoveAt(0);
            }
            if (type != LogType.Warning) diagnosticsExpanded = true;
        }

        private void EnsureDiagnosticStyles()
        {
            if (diagnosticHeader != null) return;
            diagnosticHeader = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            diagnosticBody = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            diagnosticError = new GUIStyle(diagnosticBody);
            diagnosticError.normal.textColor = new Color(1f, .35f, .25f);
        }

        private readonly struct DiagnosticLog
        {
            public readonly LogType Type;
            public readonly string Message;
            public readonly string StackTrace;

            public DiagnosticLog(LogType type, string message, string stackTrace)
            {
                Type = type;
                Message = message;
                StackTrace = stackTrace;
            }
        }
    }
}
