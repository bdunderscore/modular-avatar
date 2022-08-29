using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    [InitializeOnLoad]
    public static class ModularAvatarBootstrap
    {
        private const string VERSION = "0.0.1";
        private const string GIT_HASH = "d13c857e3ccffed4fa5f0ce53d9ad09a4fdd9b6b";

        private const string GIT_URL =
            "git+ssh://github-bdunderscore/bdunderscore/modular-avatar.git?path=Packages/net.fushizen.modular-avatar#" +
            GIT_HASH;

        private const string PACKAGE_NAME = "net.fushizen.modular-avatar";

        private const string JSON_PACKAGE = "jillejr.newtonsoft.json-for-unity";

        private static AddRequest Request;
        
        static ModularAvatarBootstrap()
        {
            SemanticVersioning.Version installedVersion = null;
            try
            {
                installedVersion = GetInstalledVersion();
                var targetVersion = SemanticVersioning.Version.Parse(VERSION);

                if (targetVersion <= installedVersion)
                {
                    return;
                }
            }
            catch (Exception e)
            {
                // Assume we aren't installed.
            }

            // VRCSDK depends on Newtonsoft JSON, so we can (theoretically) rely on it here.
            var manifestFile = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            var manifestContents = File.ReadAllText(manifestFile, Encoding.UTF8);

            JObject manifest = JsonConvert.DeserializeObject<JObject>(manifestContents);
            var packages = manifest["dependencies"] as JObject;
            if (packages == null || packages.ContainsKey(PACKAGE_NAME))
            {
                return; // we assume the package manager is still doing its thing
            }

            packages[PACKAGE_NAME] = GIT_URL;
            File.WriteAllText(manifestFile, JsonConvert.SerializeObject(manifest, Formatting.Indented));
            
            EditorApplication.delayCall += () =>
            {
                MethodInfo refresh = typeof(Client).GetMethod("Resolve",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (refresh != null) refresh.Invoke(null, null);
            };
        }

        private static SemanticVersioning.Version GetInstalledVersion()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var versionType = assembly.GetType("net.fushizen.modular_avatar.core.editor.Version", false);
                if (versionType == null) continue;
                
                var versionString = versionType.GetField("VERSION", BindingFlags.Public | BindingFlags.Static);
                if (versionString == null) continue;
                
                var version = versionString.GetValue(null) as string;
                return SemanticVersioning.Version.Parse(version);
            }

            return null;
        }
    }
}