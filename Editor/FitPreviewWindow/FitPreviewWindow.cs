#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.ui;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    internal class FitPreviewWindow : SceneView
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/FitPreviewWindow/";
        private const string UxmlPath = Root + "FitPreviewWindow.uxml";
        private const string UssPath = Root + "FitPreviewWindowStyles.uss";

        private static readonly MethodInfo? ClearSceneDirtiness = typeof(EditorSceneManager).GetMethod(
            "ClearSceneDirtiness",
            BindingFlags.NonPublic | BindingFlags.Static);

        private static FitPreviewWindow? _activeWindow;
        private static int initCountdown = 3;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.update += CountdownToInit;

            duringSceneGui += sv =>
            {
                if (sv is FitPreviewWindow window)
                {
                    window.DuringSceneGUI();
                }
            };
        }

        private static void CountdownToInit()
        {
            if (--initCountdown == 0)
            {
                EditorApplication.update -= CountdownToInit;
            }
        }

        [MenuItem(UnityMenuItems.GameObject_OpenFitPreview, false, priority = UnityMenuItems.GameObject_OpenFitPreviewOrder)]
        public static void ShowWindow()
        {
            if (_activeWindow != null)
            {
                _activeWindow.Close();
            }

            var window = CreateWindow<FitPreviewWindow>();
            window.m_targetAvatarRoot = Selection.activeGameObject;
        }

        [MenuItem(UnityMenuItems.GameObject_OpenFitPreview, true, priority = UnityMenuItems.GameObject_OpenFitPreviewOrder)]
        public static bool ShowWindow_Validate()
        {
            var t = Selection.activeTransform;

            return t != null && t == RuntimeUtil.FindAvatarTransformInParents(t);
        }
        
        private new void OnDestroy()
        {
            _shadowHierarchyFilter?.Dispose();
            _previewSession?.Dispose();
            _previewSession = null!;
            _activeWindow = null;
            DestroyImmediate(_pbManager);
            FitPreviewSceneManager.UnloadPreviewScene();
        }

        [UsedImplicitly] [SerializeField] private GameObject m_targetAvatarRoot;
        
        private Scene _scene;
        private AssemblyReloadEvents.AssemblyReloadCallback _onReload;
        private VisualElement _parentVisualElement;
        private PreviewSession _previewSession;
        private HideOtherAvatarsTracker _hideOtherAvatarsTracker;
        private ObjectField _targetAvatar;

        private EventCallback<AttachToPanelEvent> _onAttachToPanel;
        private ShadowHierarchyFilter? _shadowHierarchyFilter;

        public override void OnEnable()
        {
            base.OnEnable();

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            _parentVisualElement = uxml;

            EditorApplication.delayCall += PostCreate;

            if (rootVisualElement.panel != null)
            {
                InjectUI(null);
            }
            else
            {
                _onAttachToPanel = InjectUI;
                rootVisualElement.RegisterCallback<AttachToPanelEvent>(_onAttachToPanel);
            }

            sceneViewState.alwaysRefresh = true;
        }

        private void InjectUI(AttachToPanelEvent? evt)
        {
            rootVisualElement.UnregisterCallback<AttachToPanelEvent>(_onAttachToPanel);

            EditorApplication.delayCall += () =>
            {
                var panel = rootVisualElement.panel.visualTree;
                var rootVisualContainer = panel.Children().First(c => c.name.StartsWith("rootVisualContainer"));

                var replacementContainer = new VisualElement();
                replacementContainer.style.flexGrow = 1;
                foreach (var child in rootVisualContainer.Children().ToList())
                {
                    child.RemoveFromHierarchy();
                    replacementContainer.Add(child);
                }

                rootVisualContainer.Add(_parentVisualElement);
                rootVisualContainer.Add(replacementContainer);
            };
        }

        private void PostCreate()
        {
            if (this == null) return;

            // Delay doing anything for a few frames after a domain reload to avoid weird issues if a fit
            // preview window was open across a domain reload.
            if (initCountdown > 0)
            {
                EditorApplication.delayCall += PostCreate;
                RepaintAll(); // force an editor update loop by forcing a repaint
                return;
            }

            try
            {
                _scene = FitPreviewSceneManager.LoadPreviewScene();
            }
            catch (InvalidOperationException)
            {
                // Try again next frame
                EditorApplication.delayCall += PostCreate;
                return;
            }

            titleContent = new GUIContent("MA Fit Preview");
            
            // Initialze PB systems
            _pbManager = new GameObject("PB Manager");
            _pbManager.hideFlags = HideFlags.DontSave;
            SceneManager.MoveGameObjectToScene(_pbManager, _scene);
            var mgr = _pbManager.AddComponent<EditModePBManager>();
            mgr.IsSDK = true;
            PhysBoneManager.Inst = mgr;
            mgr.Init();

            _previewSession = PreviewSession.Current?.Fork() ?? new PreviewSession();
            _previewSession.OverrideCamera(camera);

            _targetAvatar = _parentVisualElement.Q<ObjectField>("target-avatar-field");
            _targetAvatar.Bind(new SerializedObject(this));

            _hideOtherAvatarsTracker = new HideOtherAvatarsTracker();
            _shadowHierarchyFilter = new ShadowHierarchyFilter(_scene);
            _previewSession.HiddenRenderers = _hideOtherAvatarsTracker.GetHiddenRenderers;
            _previewSession.AddMutator(new SequencePoint(), _shadowHierarchyFilter);
            _targetAvatar.RegisterCallback<ChangeEvent<Object>>(ev =>
            {
                _hideOtherAvatarsTracker.targetAvatar.Value = ev.newValue;
                _shadowHierarchyFilter.targetAvatarRoot.Value = ev.newValue as GameObject;
            });
            _hideOtherAvatarsTracker.targetAvatar.Value = m_targetAvatarRoot;
            _shadowHierarchyFilter.targetAvatarRoot.Value = m_targetAvatarRoot;
        }

        private FitPreviewWindow()
        {
        }

        private Vector3? lastClosestApproach;
        private Transform? activeTarget;
        private GameObject _pbManager;

        private Transform? _surrogateHandleTarget;

        protected override void OnSceneGUI()
        {
            var oldHandleHiddenState = Tools.hidden;
            var oldDrawGizmosState = drawGizmos;
            SetToolState();
            try
            {
                // Suppress the DELETE key from deleting objects (to avoid accidents when manipulating UI in this 
                // scene view window)
                switch (Event.current.type)
                {
                    case EventType.KeyDown:
                    case EventType.KeyUp:
                        if (Event.current.keyCode == KeyCode.Delete)
                        {
                            Event.current.Use();
                        }

                        break;
                }
                
                base.OnSceneGUI();
            }
            finally
            {
                Tools.hidden = oldHandleHiddenState;
                drawGizmos = oldDrawGizmosState;
            }
        }

        private void SetToolState()
        {
            _surrogateHandleTarget = null;

            if (Tools.hidden)
            {
                return;
            }

            if (activeTarget != null)
            {
                Tools.hidden = true;
            }

            if (Selection.gameObjects.All(go => go.scene == _scene))
            {
                // All selected objects are in the preview scene; show the real handles
                return;
            }

            // Showing the base gizmos will just be confusing and visually noisy
            drawGizmos = false;

            Tools.hidden = true;
            if (Selection.count == 1 && Selection.activeTransform != null)
            {
                _surrogateHandleTarget =
                    _shadowHierarchyFilter?._shadowBoneHierarchy?.GetTransformIfExists(Selection.activeTransform);
            }
        }

        private void DuringSceneGUI()
        {
            if (activeTarget != null)
            {
                var rot = activeTarget.rotation;
                rot = Handles.RotationHandle(activeTarget.rotation, activeTarget.position);
                if (rot != activeTarget.rotation)
                {
                    Undo.RecordObject(activeTarget, "Rotation");
                    activeTarget.rotation = rot;
                }
            }
            else if (_surrogateHandleTarget != null)
            {
                switch (Tools.current)
                {
                    case Tool.Move:
                    {
                        var pos = Handles.PositionHandle(_surrogateHandleTarget.position,
                            _surrogateHandleTarget.rotation);
                        if (pos != _surrogateHandleTarget.position)
                        {
                            Undo.RecordObject(_surrogateHandleTarget, "Position");
                            _surrogateHandleTarget.position = pos;
                        }

                        break;
                    }
                    case Tool.Rotate:
                    {
                        var rot = Handles.RotationHandle(_surrogateHandleTarget.rotation,
                            _surrogateHandleTarget.position);
                        if (rot != _surrogateHandleTarget.rotation)
                        {
                            Undo.RecordObject(_surrogateHandleTarget, "Rotation");
                            _surrogateHandleTarget.rotation = rot;
                        }

                        break;
                    }
                    case Tool.Scale:
                    {
                        var localScale = Handles.ScaleHandle(_surrogateHandleTarget.localScale,
                            _surrogateHandleTarget.position, _surrogateHandleTarget.rotation);
                        if (localScale != _surrogateHandleTarget.localScale)
                        {
                            Undo.RecordObject(_surrogateHandleTarget, "Scale");
                            _surrogateHandleTarget.localScale = localScale;
                        }

                        break;
                    }
                    case Tool.Transform:
                    {
                        var pos = _surrogateHandleTarget.position;
                        var rot = _surrogateHandleTarget.rotation;
                        var localScale = _surrogateHandleTarget.localScale;

                        Handles.TransformHandle(ref pos, ref rot, ref localScale);

                        if (pos != _surrogateHandleTarget.position)
                        {
                            Undo.RecordObject(_surrogateHandleTarget, "Position");
                            _surrogateHandleTarget.position = pos;
                        }

                        if (rot != _surrogateHandleTarget.rotation)
                        {
                            Undo.RecordObject(_surrogateHandleTarget, "Rotation");
                            _surrogateHandleTarget.rotation = rot;
                        }

                        if (localScale != _surrogateHandleTarget.localScale)
                        {
                            Undo.RecordObject(_surrogateHandleTarget, "Scale");
                            _surrogateHandleTarget.localScale = localScale;
                        }

                        break;
                    }
                }
            }

            DrawSelectionUI();
        }

        private void DrawSelectionUI()
        {
            Ray? clickRay = null;
            switch (Event.current.type)
            {
                case EventType.MouseDown:
                {
                    if (Event.current.button != 0) return;
                    clickRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    break;
                }
                case EventType.Repaint: break;
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(0); // suppress gameobject selection
                    break;
                default: return;
            }

            var target = _targetAvatar?.value as GameObject;
            if (target != null && target.TryGetComponent<Animator>(out var animator))
            {
                HashSet<Transform> shadowBodyBones = new();
                foreach (var bodyBone in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if ((int)bodyBone < 0 || (int)bodyBone >= (int)HumanBodyBones.LastBone) continue;

                    Transform originalBone;
                    try
                    {
                        originalBone = animator.GetBoneTransform((HumanBodyBones)bodyBone);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        break;
                    }

                    if (originalBone == null) continue;

                    var shadowBone = _shadowHierarchyFilter?._shadowBoneHierarchy
                        ?.GetOrCreateTransform(originalBone);
                    if (shadowBone == null)
                    {
                        continue;
                    }

                    shadowBodyBones.Add(shadowBone);
                }

                // Build bone parent hierarchy
                Dictionary<Transform, Transform> boneParents = new();
                foreach (var t in shadowBodyBones)
                {
                    var cursor = t.parent;
                    while (cursor != null && !shadowBodyBones.Contains(cursor)) cursor = cursor.parent;
                    if (cursor != null) boneParents[t] = cursor;
                }

                if (clickRay != null) activeTarget = null;

                var bestDist = float.MaxValue;
                var oldColor = Handles.color;
                foreach (var (child, parent) in boneParents)
                {
                    var p2 = child.position;
                    var screenPos = HandleUtility.WorldToGUIPoint(p2);
                    var screenRay = HandleUtility.GUIPointToWorldRay(screenPos);
                    var radiusRay = HandleUtility.GUIPointToWorldRay(screenPos + Vector2.right * 10);
                    var radiusNormal = radiusRay.direction.normalized;
                    var newRayDist = Vector3.Dot(radiusNormal, p2 - radiusRay.origin);
                    var newRayPoint = radiusRay.origin + radiusNormal * newRayDist;
                    var discRadius = (newRayPoint - p2).magnitude;

                    if (clickRay != null)
                    {
                        var clickNormal = clickRay.Value.direction.normalized;
                        var clickRayDist = Vector3.Dot(clickNormal, p2 - clickRay.Value.origin);
                        var clickRayClosestPoint = clickNormal * clickRayDist + clickRay.Value.origin;
                        var clickRayToTargetDist = (clickRayClosestPoint - p2).magnitude;

                        if (clickRayToTargetDist < discRadius && clickRayToTargetDist < bestDist)
                        {
                            bestDist = clickRayToTargetDist;
                            activeTarget = child;
                            Event.current.Use();
                        }
                    }
                    
                    Handles.color = child == activeTarget ? Color.red : Color.white;
                    Handles.DrawWireDisc(p2, screenRay.direction, discRadius);
                    if (child == activeTarget)
                    {
                        Handles.color = new Color(1, 0, 0, 0.15f);
                        Handles.DrawSolidDisc(p2, screenRay.direction, discRadius);
                    }
                    //Handles.DrawLine(p1, p2, 3);
                }

                if (lastClosestApproach != null)
                {
                    var p = lastClosestApproach.Value;
                    Handles.color = Color.magenta;
                    Handles.DrawLine(p - Vector3.left * 0.05f, p + Vector3.left * 0.05f, 3);
                    Handles.DrawLine(p - Vector3.up * 0.05f, p + Vector3.up * 0.05f, 3);
                    Handles.DrawLine(p - Vector3.forward * 0.05f, p + Vector3.forward * 0.05f, 3);
                }

                Handles.color = oldColor;
            }
        }
    }
}