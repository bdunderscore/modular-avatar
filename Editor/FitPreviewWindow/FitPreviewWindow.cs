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
        private static int initFrameNumber;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            initFrameNumber = Time.frameCount;
            EditorApplication.update += () =>
            {
                if (_activeWindow != null)
                {
                    ClearSceneDirtiness?.Invoke(null, new object[] { _activeWindow._scene });
                }
            };
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
            duringSceneGui -= DuringSceneGUI;
            _shadowHierarchyFilter?.Dispose();
            _previewSession?.Dispose();
            _previewSession = null!;
            _activeWindow = null;
            DestroyImmediate(_pbManager);
            if (_scene.IsValid())
            {
                ClearSceneDirtiness?.Invoke(null, new object[] { _scene });
                SceneManager.UnloadSceneAsync(_scene);
            }
        }

        [UsedImplicitly] [SerializeField] private GameObject m_targetAvatarRoot;
        
        private Scene _scene;
        private AssemblyReloadEvents.AssemblyReloadCallback _onReload;
        private VisualElement _parentVisualElement;
        private PreviewSession _previewSession;
        private HideOtherAvatarsFilter _hideOtherAvatarsFilter;
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

            if (Time.frameCount - initFrameNumber < 3)
            {
                EditorApplication.delayCall += PostCreate;
                return;
            }

            try
            {
                var scenes = EditorSceneManager.sceneCount;
                for (var i = 0; i < scenes; i++)
                {
                    var scene = EditorSceneManager.GetSceneAt(i);
                    if (scene.name == "MA Fit Preview")
                    {
                        _scene = scene;
                        foreach (var root in _scene.GetRootGameObjects())
                        {
                            DestroyImmediate(root);
                        }

                        break;
                    }
                }

                if (!_scene.IsValid())
                {
                    // TODO - use a persistent scene (same reasons as the NDMF preview scene - can only have one
                    // untitled scene, and need to unload it after domain reloads)
                    // Maybe create an API for aux scenes?
                    _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    _scene.name = "MA Fit Preview";
                }
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
            SceneManager.MoveGameObjectToScene(_pbManager, _scene);
            var mgr = _pbManager.AddComponent<EditModePBManager>();
            mgr.IsSDK = true;
            PhysBoneManager.Inst = mgr;
            mgr.Init();

            _previewSession = PreviewSession.Current?.Fork() ?? new PreviewSession();
            _previewSession.OverrideCamera(camera);

            _targetAvatar = _parentVisualElement.Q<ObjectField>("target-avatar-field");
            _targetAvatar.Bind(new SerializedObject(this));

            var targetFilter = new HideOtherAvatarsFilter();
            _shadowHierarchyFilter = new ShadowHierarchyFilter(_scene);
            _previewSession.AddMutator(new SequencePoint(), targetFilter);
            _previewSession.AddMutator(new SequencePoint(), _shadowHierarchyFilter);
            _targetAvatar.RegisterCallback<ChangeEvent<Object>>(ev =>
            {
                targetFilter.targetAvatar.Value = ev.newValue;
                _shadowHierarchyFilter.targetAvatarRoot.Value = ev.newValue as GameObject;
            });
            targetFilter.targetAvatar.Value = m_targetAvatarRoot;
            _shadowHierarchyFilter.targetAvatarRoot.Value = m_targetAvatarRoot;

            duringSceneGui += DuringSceneGUI;
        }

        private FitPreviewWindow()
        {
        }

        private Vector3? lastClosestApproach;
        private Transform? activeTarget;
        private GameObject _pbManager;

        private void DuringSceneGUI(SceneView sv)
        {
            if (sv != this) return;

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