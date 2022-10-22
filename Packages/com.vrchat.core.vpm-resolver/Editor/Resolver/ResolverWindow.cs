using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.PackageManagement.Core;
using VRC.PackageManagement.Core.Types;
using VRC.PackageManagement.Core.Types.Packages;
using Version = VRC.PackageManagement.Core.Types.VPMVersion.Version;

namespace VRC.PackageManagement.Resolver
{
    public class ResolverWindow : EditorWindow
    {
        // VisualElements
        private static VisualElement _rootView;
        private static Button _refreshButton;
        private static Button _createButton;
        private static Button _resolveButton;
        private static Box _manifestInfo;
        private static Label _manifestLabel;
        private static bool _isUpdating;
        private static Color _colorPositive = Color.green;
        private static Color _colorNegative = new Color(1, 0.3f, 0.3f);


        [MenuItem("VRChat SDK/Utilities/Package Resolver")]
        public static void ShowWindow()
        {
            ResolverWindow wnd = GetWindow<ResolverWindow>();
            wnd.titleContent = new GUIContent("Package Resolver");
        }

        public static void Refresh()
        {
            if (_rootView == null || string.IsNullOrWhiteSpace(Resolver.ProjectDir)) return;

            _manifestInfo.SetEnabled(!_isUpdating);
            _refreshButton.SetEnabled(!_isUpdating);
            _manifestLabel.text = (_isUpdating ? "Working ..." : "Required Packages");
            _manifestInfo.Clear();
            _manifestInfo.Add(_manifestLabel);

            bool needsResolve = VPMProjectManifest.ResolveIsNeeded(Resolver.ProjectDir);
            string resolveStatus = needsResolve ? "Please press  \"Resolve\" to Download them." : "All of them are in the project.";
            
            // check for vpm dependencies
            if (!Resolver.VPMManifestExists())
            {
                TextElement noManifestText = new TextElement();
                noManifestText.text = "No VPM Manifest";
                noManifestText.style.color = _colorNegative;
                _manifestInfo.Add(noManifestText);
            }
            else
            {
                var manifest = VPMProjectManifest.Load(Resolver.ProjectDir);
                var project = new UnityProject(Resolver.ProjectDir);
                
                // Here is where we detect if all dependencies are installed
                var allDependencies = (manifest.locked != null && manifest.locked.Count > 0)
                    ? manifest.locked
                    : manifest.dependencies;

                foreach (var pair in allDependencies)
                {
                    var id = pair.Key;
                    var version = pair.Value.version;
                    IVRCPackage package = project.VPMProvider.GetPackage(id, version);
                    _manifestInfo.Add(CreateDependencyRow(id, version, project, (package != null)));
                }

            }
            _resolveButton.SetEnabled(needsResolve);
            Resolver.ForceRefresh();
        }

        /// <summary>
        /// Unity calls the CreateGUI method automatically when the window needs to display
        /// </summary>
        private void CreateGUI()
        {
            _rootView = rootVisualElement;
            _rootView.name = "root-view";
            _rootView.styleSheets.Add((StyleSheet)Resources.Load("ResolverWindowStyle"));

            // Main Container
            var container = new Box()
            {
                name = "buttons"
            };
            _rootView.Add(container);

            // Create Button
            if (!Resolver.VPMManifestExists())
            {
                _createButton = new Button(Resolver.CreateManifest)
                {
                    text = "Create",
                    name = "create-button-base"
                };
                container.Add(_createButton);
            }
            else
            {
                _resolveButton = new Button(Resolver.ResolveManifest)
                {
                    text = "Resolve All",
                    name = "resolve-button-base"
                };
                container.Add(_resolveButton);
            }

            // Manifest Info
            _manifestInfo = new Box()
            {
                name = "manifest-info",
            };
            _manifestLabel = (new Label("Required Packages") { name = "manifest-header" });

            _rootView.Add(_manifestInfo);

            // Refresh Button
            var refreshBox = new Box();
            _refreshButton = new Button(Refresh)
            {
                text = "Refresh",
                name = "refresh-button-base"
            };
            refreshBox.Add(_refreshButton);
            _rootView.Add(refreshBox);

            Refresh();
        }

        private static VisualElement CreateDependencyRow(string id, string version, UnityProject project, bool havePackage)
        {
            // Table

            VisualElement row = new Box() { name = "package-box" };
            VisualElement column1 = new Box() { name = "package-box" };
            VisualElement column2 = new Box() { name = "package-box" };
            VisualElement column3 = new Box() { name = "package-box" };
            VisualElement column4 = new Box() { name = "package-box" };

            column1.style.minWidth = 200;
            column2.style.minWidth = 100;
            column3.style.minWidth = 100;
            column4.style.minWidth = 100;

            row.Add(column1);
            row.Add(column2);
            row.Add(column3);
            row.Add(column4);

            // Package Name + Status

            TextElement text = new TextElement { text = $"{id} {version} " };

            column1.Add(text);

            if (!havePackage)
            {
                TextElement missingText = new TextElement { text = "MISSING" };
                missingText.style.color = _colorNegative;
                missingText.style.display = (_isUpdating ? DisplayStyle.None : DisplayStyle.Flex);
                column2.Add(missingText);
            }

            // Version Popup

            var choices = new List<string>();
            foreach (string n in Resolver.GetAllVersionsOf(id))
            {
                choices.Add(n);
            }

            var popupField = new PopupField<string>(choices, 0);
            popupField.value = choices[0];
            popupField.style.display = (_isUpdating ? DisplayStyle.None : DisplayStyle.Flex);

            column3.Add(popupField);

            // Button

            Button updateButton = new Button() { text = "Update" };
            if (havePackage)
                RefreshUpdateButton(updateButton, version, choices[0]);
            else
                RefreshMissingButton(updateButton);

            updateButton.clicked += (() =>
            {
                IVRCPackage package = Repos.GetPackageWithVersionMatch(id, popupField.value);

                // Check and warn on Dependencies if Updating or Downgrading
                if (Version.TryParse(version, out var currentVersion) &&
                    Version.TryParse(popupField.value, out var newVersion))
                {
                    Dictionary<string, string> dependencies = new Dictionary<string, string>();
                    StringBuilder dialogMsg = new StringBuilder();
                    List<string> affectedPackages = Resolver.GetAffectedPackageList(package);
                    for (int v = 0; v < affectedPackages.Count; v++)
                    {
                        dialogMsg.Append(affectedPackages[v]);
                    }

                    if (affectedPackages.Count > 1)
                    {
                        dialogMsg.Insert(0, "This will update multiple packages:\n\n");
                        dialogMsg.AppendLine("\nAre you sure?");
                        if (EditorUtility.DisplayDialog("Package Has Dependencies", dialogMsg.ToString(), "OK", "Cancel"))
                            OnUpdatePackageClicked(project, package);
                    }
                    else
                    {
                        OnUpdatePackageClicked(project, package);
                    }
                }

            });
            column4.Add(updateButton);

            popupField.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                if (havePackage)
                    RefreshUpdateButton(updateButton, version, evt.newValue);
                else
                    RefreshMissingButton(updateButton);
            });

            return row;
        }

        private static void RefreshUpdateButton(Button button, string currentVersion, string highestAvailableVersion)
        {
            if (currentVersion == highestAvailableVersion)
            {
                button.style.display = DisplayStyle.None;
            }
            else
            {
                button.style.display = (_isUpdating ? DisplayStyle.None : DisplayStyle.Flex);
                if (Version.TryParse(currentVersion, out var currentVersionObject) &&
                    Version.TryParse(highestAvailableVersion, out var highestAvailableVersionObject))
                {
                    if (currentVersionObject < highestAvailableVersionObject)
                    {
                        SetButtonColor(button, _colorPositive);
                        button.text = "Update";
                    }
                    else
                    {
                        SetButtonColor(button, _colorNegative);
                        button.text = "Downgrade";
                    }
                }
            }
        }

        private static void RefreshMissingButton(Button button)
        {
            button.text = "Resolve";
            SetButtonColor(button, Color.white);
            button.style.display = (_isUpdating ? DisplayStyle.None : DisplayStyle.Flex);
        }

        private static void SetButtonColor(Button button, Color color)
        {
            button.style.color = color;
            color.a = 0.25f;
            button.style.borderRightColor =
            button.style.borderLeftColor =
            button.style.borderTopColor =
            button.style.borderBottomColor =
            color;
        }

        private static async void OnUpdatePackageClicked(UnityProject project, IVRCPackage package)
        {
            _isUpdating = true;
            Refresh();
            await Task.Delay(500);
            await Task.Run(() => project.UpdateVPMPackage(package));
            _isUpdating = false;
            Refresh();
        }

    }
}