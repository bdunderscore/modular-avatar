using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.PackageManagement.Core.Types.Packages;
using YamlDotNet.Serialization.NodeTypeResolvers;

namespace VRC.PackageManagement.PackageMaker
{
    public class PackageMakerWindow : EditorWindow
    {
        // VisualElements
        private VisualElement _rootView;
   		private TextField _targetAssetFolderField;
        private TextField _packageIDField;
        private Button _actionButton;
        private EnumField _targetVRCPackageField;
        private static string _projectDir;
        private PackageMakerWindowData _windowData;

        private void LoadDataFromSave()
        {
            if (!string.IsNullOrWhiteSpace(_windowData.targetAssetFolder))
            {
                _targetAssetFolderField.SetValueWithoutNotify(_windowData.targetAssetFolder);
            }
            _packageIDField.SetValueWithoutNotify(_windowData.packageID);
            _targetVRCPackageField.SetValueWithoutNotify(_windowData.relatedPackage);
            
            RefreshActionButtonState();
        }

        private void OnEnable()
        {
            _projectDir = Directory.GetParent(Application.dataPath).FullName;
            Refresh();
        }

        [MenuItem("VRChat SDK/Utilities/Package Maker")]
        public static void ShowWindow()
        {
            PackageMakerWindow wnd = GetWindow<PackageMakerWindow>();
            wnd.titleContent = new GUIContent("Package Maker");
        }
        
        [MenuItem("Assets/Export VPM as UnityPackage")]
        private static void ExportAsUnityPackage ()
        {
            if (Selection.assetGUIDs.Length != 1)
            {
                Debug.LogWarning($"Cannot export selection, must be a single Folder.");
                return;
            }

            string selectedFolder = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            var manifestPath = Path.Combine(selectedFolder, VRCPackageManifest.Filename);
            var manifest = VRCPackageManifest.GetManifestAtPath(manifestPath);
            if (manifest == null)
            {
                Debug.LogWarning($"Could not read valid Package Manifest at {manifestPath}. You need to create this first.");
                return;
            }

            var exportDir = Path.Combine(Directory.GetCurrentDirectory(), "Exports");
            Directory.CreateDirectory(exportDir);
            AssetDatabase.ExportPackage
            (
                selectedFolder, 
                Path.Combine(exportDir, $"{manifest.Id}-{manifest.Version}.unitypackage"),
                ExportPackageOptions.Recurse | ExportPackageOptions.Interactive
            );
        }

        private void Refresh()
        {
            if (_windowData == null)
            {
                _windowData = PackageMakerWindowData.GetOrCreate();
            }
            
            if (_rootView == null) return;

            if (_windowData != null)
            {
                LoadDataFromSave();
            }
        }

        private void RefreshActionButtonState()
        {
            _actionButton.SetEnabled(
                StringIsValidAssetFolder(_windowData.targetAssetFolder) &&
                !string.IsNullOrWhiteSpace(_windowData.packageID)
            );
        }

        /// <summary>
        /// Unity calls the CreateGUI method automatically when the window needs to display
        /// </summary>
        private void CreateGUI()
        {
            if (_windowData == null)
            {
                _windowData = PackageMakerWindowData.GetOrCreate();
            }
            
            _rootView = rootVisualElement;
            _rootView.name = "root-view";
            _rootView.styleSheets.Add((StyleSheet) Resources.Load("PackageMakerWindowStyle"));

            // Create Target Asset folder and register for drag and drop events
            _rootView.Add(CreateTargetFolderElement());
            _rootView.Add(CreatePackageIDElement());
            _rootView.Add(CreateTargetVRCPackageElement());
            _rootView.Add(CreateActionButton());

            Refresh();
        }

        public enum VRCPackageEnum
        {
            None = 0,
            Worlds = 1,
            Avatars = 2,
            Base = 3,
            UdonSharp = 4,
        }
        
        private VisualElement CreateTargetVRCPackageElement()
        {
            _targetVRCPackageField = new EnumField("Related VRChat Package", VRCPackageEnum.None);
            _targetVRCPackageField.RegisterValueChangedCallback(OnTargetVRCPackageChanged);
            var box = new Box();
            box.Add(_targetVRCPackageField);
            return box;
        }

        private void OnTargetVRCPackageChanged(ChangeEvent<Enum> evt)
        {
            _windowData.relatedPackage = (VRCPackageEnum)evt.newValue;
            _windowData.Save();
        }

        private VisualElement CreateActionButton()
        {
            _actionButton = new Button(OnActionButtonPressed)
            {
                text = "Convert Assets to Package",
                name = "action-button"
            };
            return _actionButton;
        }

        private void OnActionButtonPressed()
        {
            bool result = EditorUtility.DisplayDialog("One-Way Conversion",
                $"This process will move the assets from {_windowData.targetAssetFolder} into a new Package with the id {_windowData.packageID} and give it references to {_windowData.relatedPackage}.",
                "Ok", "Wait, not yet.");
            if (result)
            {
                string newPackageFolderPath = Path.Combine(_projectDir, "Packages", _windowData.packageID);
                Directory.CreateDirectory(newPackageFolderPath);
                var fullTargetAssetFolder = Path.Combine(_projectDir, _windowData.targetAssetFolder);
                DoMigration(fullTargetAssetFolder, newPackageFolderPath);
                ForceRefresh();
            }
        }
        
        public static void ForceRefresh ()
        {
            MethodInfo method = typeof( UnityEditor.PackageManager.Client ).GetMethod( "Resolve", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
            if( method != null )
                method.Invoke( null, null );

            AssetDatabase.Refresh();
        }

        private VisualElement CreatePackageIDElement()
        {
            var box = new Box()
            {
                name = "package-name-box"
            };

            _packageIDField = new TextField("Package ID", 255, false, false, '*');
            _packageIDField.RegisterValueChangedCallback(OnPackageIDChanged);
            box.Add(_packageIDField);
            
            box.Add(new Label("Lowercase letters, numbers and dots only.")
            {
                name="description",
                tooltip = "Standard practice is reverse domain notation like com.vrchat.packagename. Needs to be unique across VRChat, so if you don't own a domain you can try your username.",
            });
            
            return box;
        }

        private Regex packageIdRegex = new Regex("[^a-z0-9.]");
        private void OnPackageIDChanged(ChangeEvent<string> evt)
        {
            if (evt.newValue != null)
            {
                string newId = packageIdRegex.Replace(evt.newValue, "-");
                _packageIDField.SetValueWithoutNotify(newId);
                _windowData.packageID = newId;
                _windowData.Save();
            }
            RefreshActionButtonState();
        }

        private VisualElement CreateTargetFolderElement()
        {
            var targetFolderBox = new Box()
            {
                name = "editor-target-box"
            };
            
            _targetAssetFolderField = new TextField("Target Folder");
            _targetAssetFolderField.RegisterCallback<DragEnterEvent>(OnTargetAssetFolderDragEnter, TrickleDown.TrickleDown);
            _targetAssetFolderField.RegisterCallback<DragLeaveEvent>(OnTargetAssetFolderDragLeave, TrickleDown.TrickleDown);
            _targetAssetFolderField.RegisterCallback<DragUpdatedEvent>(OnTargetAssetFolderDragUpdated, TrickleDown.TrickleDown);
            _targetAssetFolderField.RegisterCallback<DragPerformEvent>(OnTargetAssetFolderDragPerform, TrickleDown.TrickleDown);
            _targetAssetFolderField.RegisterCallback<DragExitedEvent>(OnTargetAssetFolderDragExited, TrickleDown.TrickleDown);
            _targetAssetFolderField.RegisterValueChangedCallback(OnTargetAssetFolderValueChanged);
            targetFolderBox.Add(_targetAssetFolderField);
            
            targetFolderBox.Add(new Label("Drag and Drop an Assets Folder to Convert Above"){name="description"});
            return targetFolderBox;
        }

        #region TargetAssetFolder Field Events

        private bool StringIsValidAssetFolder(string targetFolder)
        {
            return !string.IsNullOrWhiteSpace(targetFolder) && AssetDatabase.IsValidFolder(targetFolder);
        }
        
        private void OnTargetAssetFolderValueChanged(ChangeEvent<string> evt)
        {
            string targetFolder = evt.newValue;

            if (StringIsValidAssetFolder(targetFolder))
            {
                _windowData.targetAssetFolder = evt.newValue;
                _windowData.Save();
                RefreshActionButtonState();
            }
            else
            {
                _targetAssetFolderField.SetValueWithoutNotify(evt.previousValue);
            }
        }
        
        private void OnTargetAssetFolderDragExited(DragExitedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.None;
        }

        private void OnTargetAssetFolderDragPerform(DragPerformEvent evt)
        {
            var targetFolder = DragAndDrop.paths[0];
            if (!string.IsNullOrWhiteSpace(targetFolder) && AssetDatabase.IsValidFolder(targetFolder))
            {
                _targetAssetFolderField.value = targetFolder;
            }
            else
            {
                Debug.LogError($"Could not accept {targetFolder}. Needs to be a folder within the project");
            }
        }

        private void OnTargetAssetFolderDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Length == 1)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                DragAndDrop.AcceptDrag();
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
        }

        private void OnTargetAssetFolderDragLeave(DragLeaveEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.None;
        }

        private void OnTargetAssetFolderDragEnter(DragEnterEvent evt)
        {
            if (DragAndDrop.paths.Length == 1)
            { 
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                DragAndDrop.AcceptDrag();
            }
        }

        #endregion

        #region Migration Logic

        private void DoMigration(string corePath, string targetDir)
        {
            
            EditorUtility.DisplayProgressBar("Migrating Package", "Creating Starter Package", 0.1f);
            
            // Convert PackageType enum to VRC Package ID string
            string packageType = null;
            switch (_windowData.relatedPackage)
            {
                case VRCPackageEnum.Avatars:
                    packageType = "com.vrchat.avatars";
                    break;
                case VRCPackageEnum.Base:
                    packageType = "com.vrchat.base";
                    break;
                case VRCPackageEnum.Worlds:
                    packageType = "com.vrchat.clientsim"; // we want ClientSim too, need to specify that for now
                    break;
                case VRCPackageEnum.UdonSharp:
                    packageType = "com.vrchat.udonsharp";
                    break;
            }

            string parentDir = new DirectoryInfo(targetDir)?.Parent.FullName;
            Core.Utilities.CreateStarterPackage(_windowData.packageID, parentDir, packageType);
            var allFiles = GetAllFiles(corePath).ToList();
            MoveFilesToPackageDir(allFiles, corePath, targetDir);
            
            // Clear target asset folder since it should no longer exist
            _windowData.targetAssetFolder = "";
            
        }
        
        private static IEnumerable<string> GetAllFiles(string path)
        {
            var excludedPaths = new List<string>()
            {
                "Editor.meta"
            };
            return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(
                    s => excludedPaths.All(entry => !s.Contains(entry))
                );
        }
        
        public static void MoveFilesToPackageDir(List<string> files, string pathBase, string targetDir)
        {
            EditorUtility.DisplayProgressBar("Migrating Package", "Moving Package Files", 0f);
            float totalFiles = files.Count;

            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Migrating Package", "Moving Package Files", i / totalFiles);
                    var file = files[i];
                    string simplifiedPath = file.Replace($"{pathBase}\\", "");
                
                    string dest = null;
                    if (simplifiedPath.Contains("Editor\\"))
                    {
                        // Remove extra 'Editor' subfolders
                        dest = simplifiedPath.Replace("Editor\\", "");
                        dest = Path.Combine(targetDir, "Editor", dest);
                    }
                    else
                    {
                        // Make complete path to Runtime folder
                        dest = Path.Combine(targetDir, "Runtime", simplifiedPath);
                    }

                    string targetEnclosingDir = Path.GetDirectoryName(dest);
                    Directory.CreateDirectory(targetEnclosingDir);
                    var sourceFile = Path.Combine(pathBase, simplifiedPath);
                    File.Move(sourceFile, dest);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error moving {files[i]}: {e.Message}");
                    continue;
                }
            }
            
            Directory.Delete(pathBase, true); // cleans up leftover folders since only files are moved
            EditorUtility.ClearProgressBar();
        }
        
        // Important while we're doing copy-and-rename in order to rename paths with "Assets" without renaming paths with "Sample Assets"
        public static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        #endregion
    }

}