using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ScaleAdjusterTool
    {
        private const string UNDO_STRING = "Adjust scale";
        public static bool AdjustChildPositions = true;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
        }

        abstract class ScaleHolder
        {
            public abstract Object Obj { get; }

            public Vector3 InitialScale { get; protected set; }
            public Vector3 LastScale { get; protected set; }

            public abstract bool IsValid { get; }
            public abstract Vector3 Scale { get; set; }

            public abstract Matrix4x4 ParentLocalToWorld { get; }
            public abstract Matrix4x4 LocalBaseTransform { get; }
        }

        class GameObjectScaler : ScaleHolder
        {
            private readonly GameObject _obj;

            public override Object Obj => _obj;

            public GameObjectScaler(GameObject obj)
            {
                _obj = obj;
                LastScale = InitialScale = obj.transform.localScale;
            }

            public override bool IsValid => _obj != null;

            public override Vector3 Scale
            {
                get => _obj.transform.localScale;
                set
                {
                    Undo.RecordObject(_obj.transform, UNDO_STRING);

                    _obj.transform.localScale = value;
                    LastScale = value;
                }
            }

            public override Matrix4x4 ParentLocalToWorld => _obj.transform.parent.localToWorldMatrix;

            public override Matrix4x4 LocalBaseTransform => Matrix4x4.TRS(
                _obj.transform.localPosition,
                _obj.transform.localRotation,
                Vector3.one
            );
        }

        class AdjusterScaler : ScaleHolder
        {
            private readonly ModularAvatarScaleAdjuster _obj;
            private string UNDO_STRING;

            public override Object Obj => _obj;

            public AdjusterScaler(ModularAvatarScaleAdjuster obj)
            {
                _obj = obj;
                LastScale = InitialScale = obj.Scale;
            }

            public override bool IsValid => _obj != null;

            public override Vector3 Scale
            {
                get => _obj.Scale;
                set
                {
                    Undo.RecordObject(_obj, UNDO_STRING);

                    var targetL2W = _obj.transform.localToWorldMatrix;
                    var baseToScaleCoord = (targetL2W * Matrix4x4.Scale(_obj.Scale)).inverse * targetL2W;

                    _obj.Scale = value;
                    LastScale = value;

                    var scaleToBaseCoord = Matrix4x4.Scale(_obj.Scale);

                    PrefabUtility.RecordPrefabInstancePropertyModifications(_obj);

                    // Update child positions
                    if (AdjustChildPositions)
                    {
                        var updateTransform = scaleToBaseCoord * baseToScaleCoord;
                        foreach (Transform child in _obj.transform)
                        {
                            Undo.RecordObject(child, UNDO_STRING);
                            child.localPosition = updateTransform.MultiplyPoint(child.localPosition);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                        }
                    }
                }
            }

            public override Matrix4x4 ParentLocalToWorld => _obj.transform.parent.localToWorldMatrix;

            public override Matrix4x4 LocalBaseTransform => Matrix4x4.TRS(
                _obj.transform.localPosition,
                _obj.transform.localRotation,
                _obj.transform.localScale
            );
        }

        private static List<ScaleHolder> _selection = new List<ScaleHolder>();
        private static bool? _active = null;
        private static Vector3 _gizmoScale;
        private static Quaternion _handleRotation;

        private static bool _toolHidden;

        private static bool ToolHidden
        {
            get => _toolHidden;
            set
            {
                if (_toolHidden && !value)
                {
                    Tools.hidden = false;
                } else if (value)
                {
                    Tools.hidden = true;
                }

                _toolHidden = value;
            }
        }
        
        
        private static void OnSelectionChanged()
        {
            _selection.Clear();
            ToolHidden = false;
            _active = null;
        }

        private static void OnSceneGUI(SceneView obj)
        {
            if (Tools.current != Tool.Scale)
            {
                if (_active == true)
                {
                    ToolHidden = false;
                    _active = null;
                }

                return;
            }

            if (ShouldEnable())
            {
                ToolHidden = true;
            }
            else
            {
                ToolHidden = false;

                return;
            }

            var handleSize = HandleUtility.GetHandleSize(Tools.handlePosition);
            _handleRotation = Tools.handleRotation;

            EditorGUI.BeginChangeCheck();
            _gizmoScale = Handles.ScaleHandle(_gizmoScale, Tools.handlePosition, _handleRotation, handleSize);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var target in _selection)
                {
                    UpdateScale(target, _gizmoScale);
                }
            }
        }

        private static void UpdateScale(ScaleHolder target, Vector3 gizmoScale)
        {
            var refTransformInv = Matrix4x4.TRS(
                Tools.handlePosition,
                _handleRotation,
                Vector3.one
            ).inverse;
            var gizmoTransform = Matrix4x4.TRS(
                Tools.handlePosition,
                _handleRotation,
                gizmoScale
            );

            Matrix4x4 initialTransform = target.ParentLocalToWorld * target.LocalBaseTransform;

            Matrix4x4 initialScale = Matrix4x4.Scale(target.InitialScale);

            float scaleX = TransformVec(Vector3.right);
            float scaleY = TransformVec(Vector3.up);
            float scaleZ = TransformVec(Vector3.forward);

            target.Scale = new Vector3(scaleX, scaleY, scaleZ);

            float TransformVec(Vector3 vec)
            {
                // first, place our measurement vector into world spoce
                vec = (initialTransform * initialScale).MultiplyVector(vec);
                // now put it into reference space
                vec = refTransformInv.MultiplyVector(vec);
                // and return using the adjusted scale
                vec = gizmoTransform.MultiplyVector(vec);
                // now come back into local space
                vec = (initialTransform.inverse).MultiplyVector(vec);

                return vec.magnitude;
            }
        }

        private static bool ShouldEnable()
        {
            if (_selection.Any(s => !s.IsValid))
            {
                _active = null;
            }

            if (_selection.Any(s => (s.Scale - s.LastScale).sqrMagnitude > 0.00001f))
            {
                _active = null;
            }

            if (_active.HasValue)
            {
                return _active.Value;
            }

            _selection.Clear();
            _gizmoScale = Vector3.one;
            _handleRotation = Tools.handleRotation;
            bool anyAdjuster = false;

            foreach (var obj in Selection.gameObjects)
            {
                var adjuster = obj.GetComponent<ModularAvatarScaleAdjuster>();
                if (adjuster != null)
                {
                    _selection.Add(new AdjusterScaler(adjuster));
                    anyAdjuster = true;
                }
                else
                {
                    _selection.Add(new GameObjectScaler(obj));
                }
            }

            _active = anyAdjuster;
            return anyAdjuster;
        }
    }
}