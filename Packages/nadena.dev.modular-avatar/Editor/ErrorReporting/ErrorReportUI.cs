using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.editor.ErrorReporting
{
    internal class ErrorReportUI : EditorWindow
    {
        internal static Action reloadErrorReport = () => { };

        [MenuItem("Tools/Modular Avatar/Show error report", false, 100)]
        public static void OpenErrorReportUI()
        {
            GetWindow<ErrorReportUI>().Show();
        }

        public static void MaybeOpenErrorReportUI()
        {
            if (BuildReport.CurrentReport.Avatars.Any(av => av.logs.Count > 0))
            {
                OpenErrorReportUI();
            }
        }

        private Vector2 _avatarScrollPos, _errorScrollPos;
        private int _selectedAvatar = -1;
        private List<Button> _avatarButtons = new List<Button>();

        private Box selectAvatar;

        private void OnEnable()
        {
            titleContent = new GUIContent("Error Report");

            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("ModularAvatarErrorReport"));
            RenderContent();

            reloadErrorReport = RenderContent;

            Selection.selectionChanged += ScheduleRender;
            EditorApplication.hierarchyChanged += ScheduleRender;
            AvatarTagComponent.OnChangeAction += ScheduleRender;
            Localization.OnLangChange += RenderContent;
        }

        private void OnDisable()
        {
            reloadErrorReport = () => { };
            Selection.selectionChanged -= ScheduleRender;
            EditorApplication.hierarchyChanged -= ScheduleRender;
            AvatarTagComponent.OnChangeAction -= ScheduleRender;
            Localization.OnLangChange -= RenderContent;
        }

        private readonly int RefreshDelayTime = 500;
        private Stopwatch DelayTimer = new Stopwatch();
        private bool RenderPending = false;

        private void ScheduleRender()
        {
            if (RenderPending) return;
            RenderPending = true;
            DelayTimer.Restart();
            EditorApplication.delayCall += StartRenderTimer;
        }

        private async void StartRenderTimer()
        {
            while (DelayTimer.ElapsedMilliseconds < RefreshDelayTime)
            {
                long remaining = RefreshDelayTime - DelayTimer.ElapsedMilliseconds;
                if (remaining > 0)
                {
                    await Task.Delay((int) remaining);
                }
            }

            RenderPending = false;
            RenderContent();
            Repaint();
        }

        private void RenderContent()
        {
            rootVisualElement.Clear();

            var root = new Box();
            root.Clear();
            root.name = "Root";
            rootVisualElement.Add(root);

            root.Add(CreateLogo());

            var box = new ScrollView();
            var lookupCache = new ObjectRefLookupCache();

            int reported = 0;

            AvatarReport activeAvatar = null;

            GameObject activeAvatarObject = null;
            if (Selection.gameObjects.Length == 1)
            {
                activeAvatarObject = RuntimeUtil.FindAvatarInParents(Selection.activeGameObject.transform)?.gameObject;
                activeAvatar = BuildReport.CurrentReport.Avatars.FirstOrDefault(av =>
                    av.objectRef.path == RuntimeUtil.RelativePath(null, activeAvatarObject));

                if (activeAvatar == null)
                {
                    activeAvatar = new AvatarReport();
                    activeAvatar.objectRef = new ObjectRef(activeAvatarObject);
                }
            }

            if (activeAvatar == null)
            {
                activeAvatar = BuildReport.CurrentReport.Avatars.LastOrDefault();
            }

            if (activeAvatar != null)
            {
                reported++;

                var avBox = new Box();
                avBox.AddToClassList("avatarBox");

                var header = new Box();
                header.Add(new Label("Error report for " + activeAvatar.objectRef.name));
                header.AddToClassList("avatarHeader");
                avBox.Add(header);

                List<ErrorLog> errorLogs = activeAvatar.logs
                    .Where(l => activeAvatarObject == null || l.reportLevel != ReportLevel.Validation).ToList();

                if (activeAvatarObject != null)
                {
                    activeAvatar.logs = errorLogs;

                    activeAvatar.logs.AddRange(ComponentValidation.ValidateAll(activeAvatarObject));
                }

                foreach (var ev in activeAvatar.logs)
                {
                    avBox.Add(new ErrorElement(ev, lookupCache));
                }

                activeAvatar.logs.Sort((a, b) => a.reportLevel.CompareTo(b.reportLevel));

                box.Add(avBox);
                root.Add(box);
            }

            /*
            if (reported == 0)
            {
                var container = new Box();
                container.name = "no-errors";
                container.Add(new Label("Nothing to report!"));
                root.Add(container);
            }
            */
        }

        private VisualElement CreateLogo()
        {
            var img = new Image();
            img.image = LogoDisplay.LOGO_ASSET;

            // I've given up trying to get USS to resize proportionally for now :|
            float height = 64;
            img.style.height = new StyleLength(new Length(height, LengthUnit.Pixel));
            img.style.width = new StyleLength(new Length(LogoDisplay.ImageWidth(height), LengthUnit.Pixel));

            var box = new Box();
            box.name = "logo";
            box.Add(img);
            return box;
        }

        private VisualElement BuildErrorBox()
        {
            return new Box();
        }

        private VisualElement BuildSelectAvatarBox()
        {
            if (selectAvatar == null) selectAvatar = new Box();
            selectAvatar.Clear();
            _avatarButtons.Clear();

            var avatars = BuildReport.CurrentReport.Avatars;
            for (int i = 0; i < avatars.Count; i++)
            {
                var btn = new Button(() => SelectAvatar(i));
                btn.text = avatars[i].objectRef.name;
                _avatarButtons.Add(btn);
                selectAvatar.Add(btn);
            }

            SelectAvatar(_selectedAvatar);

            return selectAvatar;
        }

        private void SelectAvatar(int idx)
        {
            _selectedAvatar = idx;

            for (int i = 0; i < _avatarButtons.Count; i++)
            {
                if (_selectedAvatar == i)
                {
                    _avatarButtons[i].AddToClassList("selected");
                }
                else
                {
                    _avatarButtons[i].RemoveFromClassList("selected");
                }
            }
        }

        private void OnGUI___()
        {
            var report = BuildReport.CurrentReport;

            EditorGUILayout.BeginVertical(GUILayout.MaxHeight(150), GUILayout.Width(position.width));
            if (report.Avatars.Count == 0)
            {
                GUILayout.Label("<no build messages>");
            }
            else
            {
                _avatarScrollPos = EditorGUILayout.BeginScrollView(_avatarScrollPos, false, true);

                for (int i = 0; i < report.Avatars.Count; i++)
                {
                    var avatarReport = report.Avatars[i];

                    EditorGUILayout.Space();
                    if (GUILayout.Toggle(_selectedAvatar == i, avatarReport.objectRef.name, EditorStyles.toggle))
                    {
                        _selectedAvatar = i;
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            var rect = EditorGUILayout.BeginVertical(GUILayout.Width(position.width));

            _errorScrollPos = EditorGUILayout.BeginScrollView(_errorScrollPos, false, true);

            EditorGUILayout.BeginVertical(
                GUILayout.Width(rect.width
                                - GUI.skin.scrollView.margin.horizontal
                                - GUI.skin.scrollView.padding.horizontal),
                GUILayout.ExpandWidth(false));

            if (_selectedAvatar >= 0 && _selectedAvatar < BuildReport.CurrentReport.Avatars.Count)
            {
                foreach (var logEntry in BuildReport.CurrentReport.Avatars[_selectedAvatar].logs)
                {
                    imguiRenderLogEntry(logEntry);
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private static void imguiRenderLogEntry(ErrorLog logEntry)
        {
            MessageType ty = MessageType.Error;
            switch (logEntry.reportLevel)
            {
                case ReportLevel.InternalError:
                case ReportLevel.Error:
                    ty = MessageType.Error;
                    break;
                case ReportLevel.Warning:
                    ty = MessageType.Warning;
                    break;
                case ReportLevel.Info:
                    ty = MessageType.Info;
                    break;
            }

            EditorGUILayout.HelpBox(logEntry.ToString(), ty);
        }
    }
}