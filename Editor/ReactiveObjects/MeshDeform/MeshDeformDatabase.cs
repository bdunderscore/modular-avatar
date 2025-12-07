#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    internal class MeshDeformDatabase
    {
        private static Dictionary<Type, ConstructorInfo>? _builders;

        private static Dictionary<Type, ConstructorInfo> Builders
        {
            get
            {
                if (_builders == null)
                {
                    _builders = new Dictionary<Type, ConstructorInfo>();
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type == typeof(ToroidalDeformation))
                            {
                                Debug.Log("Found ToroidalDeformation");
                            }

                            var attr = type.GetCustomAttributes<MeshDeformationProvider>().FirstOrDefault();
                            if (attr == null) continue;

                            if (!typeof(IMeshDeformComponent).IsAssignableFrom(attr.ComponentType))
                            {
                                Debug.LogError("ComponentType on " + type +
                                               " is not assignable from IMeshDeformComponent");
                                continue;
                            }

                            if (!typeof(IMeshDeformation).IsAssignableFrom(type))
                            {
                                Debug.LogError("Type " + type + " does not implement IMeshDeformation");
                                continue;
                            }

                            if (_builders.ContainsKey(attr.ComponentType))
                            {
                                Debug.LogError("Multiple MeshDeformationProvider attributes for " + attr.ComponentType);
                                continue;
                            }

                            var ctor = type.GetConstructor(new[]
                            {
                                typeof(ComputeContext),
                                attr.ComponentType
                            });
                            if (ctor == null)
                            {
                                Debug.LogError("No constructor for " + type);
                                continue;
                            }

                            _builders[attr.ComponentType] = ctor;
                        }
                    }
                }

                return _builders;
            }
        }

        public static IMeshDeformation? GetDeformer(ComputeContext context, IMeshDeformComponent component)
        {
            if (!Builders.TryGetValue(component.GetType(), out var ctor))
            {
                return null;
            }

            return (IMeshDeformation)ctor.Invoke(new object[] { context, component });
        }
    }
}