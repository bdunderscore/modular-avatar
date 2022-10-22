using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;
using Serilog.Sinks.Unity3D;
using UnityEditor;
using UnityEngine;
using VRC.PackageManagement.Core;
using VRC.PackageManagement.Core.Types;
using VRC.PackageManagement.Core.Types.Packages;

namespace VRC.PackageManagement.Resolver
{
    
    [InitializeOnLoad]
    public class Resolver
    {
        private const string _projectLoadedKey = "PROJECT_LOADED";
        
        private static string _projectDir;
        public static string ProjectDir
        {
            get
            {
                if (_projectDir != null)
                {
                    return _projectDir;
                }

                try
                {
                    _projectDir = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent.Parent
                        .FullName;
                    return _projectDir;
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }

        static Resolver()
        {
            SetupLogging();
            if (!SessionState.GetBool(_projectLoadedKey, false))
            {
#pragma warning disable 4014
                CheckResolveNeeded();
#pragma warning restore 4014
            }
        }

        private static void SetupLogging()
        {
            VRCLibLogger.SetLoggerDirectly(
                new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Unity3D()
                    .CreateLogger()
            );
        }

        private static async Task CheckResolveNeeded()
        {
            SessionState.SetBool(_projectLoadedKey, true);
            
            //Wait for project to finish compiling
            while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                await Task.Delay(250);
            }

            try
            {

                if (string.IsNullOrWhiteSpace(ProjectDir))
                {
                    return;
                }
                
                if (VPMProjectManifest.ResolveIsNeeded(ProjectDir))
                {
                    Debug.Log($"Resolve needed.");
                    var result = EditorUtility.DisplayDialog("VRChat Package Management",
                        $"This project requires some VRChat Packages which are not in the project yet.\n\nPress OK to download and install them.",
                        "OK", "Show Me What's Missing");
                    if (result)
                    {
                        ResolveStatic(ProjectDir);
                    }
                    else
                    {
                        ResolverWindow.ShowWindow();
                    }
                }
            }
            catch (Exception)
            {
                // Unity says we can't open windows from this function so it throws an exception but also works fine.
            }
        }
        
        public static bool VPMManifestExists()
        {
            return VPMProjectManifest.Exists(ProjectDir, out _);
        }

        public static void CreateManifest()
        {
            VPMProjectManifest.Load(ProjectDir);
            ResolverWindow.Refresh();
        }
        
        public static void ResolveManifest()
        {
            ResolveStatic(ProjectDir);
        }

        public static void ResolveStatic(string dir)
        {
            // Todo: calculate and show actual progress
            EditorUtility.DisplayProgressBar($"Getting all VRChat Packages", "Downloading and Installing...", 0.5f);
            VPMProjectManifest.Resolve(ProjectDir);
            EditorUtility.ClearProgressBar();
            ForceRefresh();
        }
        
        public static List<string> GetAllVersionsOf(string id)
        {
            var project = new UnityProject(ProjectDir);

            var versions = new List<string>();
            foreach (var provider in Repos.GetAll)
            {
                var packagesWithVersions = provider.GetAllWithVersions();

                foreach (var packageVersionList in packagesWithVersions)
                {
                    foreach (var package in packageVersionList.Value.VersionsDescending)
                    {
                        if (package.Id != id)
                            continue;
                        if (Version.TryParse(package.Version, out var result))
                        {
                            if (!versions.Contains(package.Version))
                                versions.Add(package.Version);
                        }
                    }
                }
            }

            // Sort packages in project to the top
            var sorted = from entry in versions orderby project.VPMProvider.HasPackage(entry) descending select entry;

            return sorted.ToList<string>();
        }

        public static List<string> GetAffectedPackageList(IVRCPackage package)
        {
            List<string> list = new List<string>();

            var project = new UnityProject(ProjectDir);

            if (Repos.GetAllDependencies(package, out Dictionary<string, string> dependencies, null))
            {
                foreach (KeyValuePair<string, string> item in dependencies)
                {
                    project.VPMProvider.Refresh();
                    if (project.VPMProvider.GetPackage(item.Key, item.Value) == null)
                    {
                        IVRCPackage d = Repos.GetPackageWithVersionMatch(item.Key, item.Value);
                        if (d != null)
                        {
                            list.Add(d.Id + " " + d.Version + "\n");
                        }
                    }
                }

                return list;
            }

            return null;
        }
        
        public static void ForceRefresh ()
        {
            MethodInfo method = typeof( UnityEditor.PackageManager.Client ).GetMethod( "Resolve", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
            if( method != null )
                method.Invoke( null, null );

            AssetDatabase.Refresh();
        }

    }
}