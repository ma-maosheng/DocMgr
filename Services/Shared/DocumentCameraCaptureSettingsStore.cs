using System.IO;
using System.Text.Json;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 将直拍处理偏好按用户写入程序目录 <c>settings</c>，避免改库表。
    /// </summary>
    public sealed class DocumentCameraCaptureSettingsStore : IDocumentCameraCaptureSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly object _lock = new();
        private DocumentCameraCaptureSettings? _cached;
        private int? _cachedUserId;

        /// <inheritdoc />
        public DocumentCameraCaptureSettings Load(int? userId)
        {
            lock (_lock)
            {
                if (_cached != null && _cachedUserId == userId)
                {
                    return _cached.Clone();
                }

                DocumentCameraCaptureSettings loaded = ReadFile(userId) ?? DocumentCameraCaptureSettings.CreateDefault();
                loaded.Normalize();
                _cached = loaded.Clone();
                _cachedUserId = userId;
                return loaded;
            }
        }

        /// <inheritdoc />
        public void Save(int? userId, DocumentCameraCaptureSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.Normalize();
            lock (_lock)
            {
                _cached = settings.Clone();
                _cachedUserId = userId;
                WriteFile(userId, settings);
            }
        }

        private static DocumentCameraCaptureSettings? ReadFile(int? userId)
        {
            string path = GetFilePath(userId);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<DocumentCameraCaptureSettings>(json, JsonOptions);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteFile(int? userId, DocumentCameraCaptureSettings settings)
        {
            string path = GetFilePath(userId);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
        }

        private static string GetFilePath(int? userId)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
            string name = userId is > 0
                ? $"document-camera-capture.user-{userId.Value}.json"
                : "document-camera-capture.local.json";
            return Path.Combine(folder, name);
        }
    }
}
