using System.Collections.Generic;
using System.IO;
using SimplePatchToolCore;
using SimplePatchToolSecurity;

namespace UpdateManager.Core.Security
{
    /// <summary>
    /// Подпись собранного патча приватным RSA-ключом. Подписывает манифест VersionInfo.info и
    /// КАЖДЫЙ PatchInfo-xml инкрементальных патчей (Output/IncrementalPatch/*.info) — их проверяет
    /// клиент через UseVersionInfoVerifier / UsePatchInfoVerifier.
    ///
    /// XMLSigner.SignXMLFile вкладывает подпись (enveloped XMLDSIG) прямо в файл и идемпотентен
    /// (повторная подпись заменяет прежнюю). Подписывать нужно ПОСЛЕ любой правки этих xml
    /// (например, перезаписи BaseDownloadURL) и ПЕРЕД заливкой — иначе подпись станет невалидной.
    /// </summary>
    public class PatchSigner
    {
        /// <summary>
        /// Подписать все манифесты в папке Output приватным ключом.
        /// Возвращает список относительных имён подписанных файлов (для лога).
        /// </summary>
        public List<string> Sign(string outputPath, string privateKeyText)
        {
            var signed = new List<string>();

            // 1. Главный манифест VersionInfo.info.
            var versionInfo = Path.Combine(outputPath, PatchParameters.VERSION_INFO_FILENAME);
            if (File.Exists(versionInfo))
            {
                XMLSigner.SignXMLFile(versionInfo, privateKeyText);
                signed.Add(PatchParameters.VERSION_INFO_FILENAME);
            }

            // 2. PatchInfo каждого инкрементального патча (Output/IncrementalPatch/*.info).
            var incrementalDir = Path.Combine(outputPath, PatchParameters.INCREMENTAL_PATCH_DIRECTORY);
            if (Directory.Exists(incrementalDir))
            {
                var pattern = "*" + PatchParameters.INCREMENTAL_PATCH_INFO_EXTENSION; // "*.info"
                foreach (var info in Directory.GetFiles(incrementalDir, pattern, SearchOption.TopDirectoryOnly))
                {
                    XMLSigner.SignXMLFile(info, privateKeyText);
                    signed.Add(PatchParameters.INCREMENTAL_PATCH_DIRECTORY + "/" + Path.GetFileName(info));
                }
            }

            return signed;
        }
    }
}
