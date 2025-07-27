using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class MaterialSlotSelector
    {
        internal static void Setup(SerializedProperty objectProperty, PropertyField objectField, IntegerField materialIndexField, DropdownField  materialIndexDropdownField, ObjectField materialIndexOriginalField)
        {
            objectField.RegisterValueChangeCallback(_ => EditorApplication.delayCall += UpdateMaterialDropdown);
            UpdateMaterialDropdown();

            // Link dropdown and original field to material index field
            materialIndexField.RegisterValueChangedCallback(evt =>
            {
                materialIndexDropdownField.SetValueWithoutNotify(evt.newValue.ToString());
                UpdateOriginalMaterial();
            });
            materialIndexDropdownField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null && int.TryParse(evt.newValue, out var i))
                {
                    materialIndexField.value = i;
                }
            });
            materialIndexOriginalField.SetEnabled(false);

            void UpdateMaterialDropdown()
            {
                var sharedMaterials = GetSharedMaterials();
                if (sharedMaterials != null)
                {
                    materialIndexDropdownField.SetEnabled(true);

                    materialIndexDropdownField.choices.Clear();
                    for (var i = 0; i < sharedMaterials.Length; i++)
                    {
                        materialIndexDropdownField.choices.Add(i.ToString());
                    }

                    materialIndexDropdownField.formatListItemCallback = value =>
                    {
                        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

                        var index = int.Parse(value);
                        if (index < 0 || index >= sharedMaterials.Length)
                        {
                            return $"<color=\"red\">Element {value}: <???></color>";
                        }
                        else if (sharedMaterials[index] == null)
                        {
                            return $"Element {value}: <None>";
                        }
                        else
                        {
                            return $"Element {value}: {sharedMaterials[index].name}";
                        }
                    };
                    materialIndexDropdownField.formatSelectedValueCallback = value =>
                    {
                        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

                        var index = int.Parse(value);
                        if (index < 0 || index >= sharedMaterials.Length)
                        {
                            return $"<color=\"red\">Element {value}</color>";
                        }
                        else
                        {
                            return $"Element {value}";
                        }
                    };
                }
                else
                {
                    materialIndexDropdownField.SetEnabled(false);

                    if (materialIndexDropdownField.choices.Count == 0)
                    {
                        materialIndexDropdownField.choices.Add("0");
                    }

                    materialIndexDropdownField.formatListItemCallback = _ => "<Missing Renderer>";
                    materialIndexDropdownField.formatSelectedValueCallback = materialIndexDropdownField.formatListItemCallback;
                }

                UpdateOriginalMaterial();
            }

            void UpdateOriginalMaterial()
            {
                var sharedMaterials = GetSharedMaterials();
                if (sharedMaterials != null)
                {
                    var index = materialIndexField.value;
                    if (index < 0 || index >= sharedMaterials.Length)
                    {
                        materialIndexOriginalField.SetValueWithoutNotify(null);
                    }
                    else
                    {
                        materialIndexOriginalField.SetValueWithoutNotify(sharedMaterials[index]);
                    }
                }
                else
                {
                    materialIndexOriginalField.SetValueWithoutNotify(null);
                }
            }

            Material[] GetSharedMaterials()
            {
                var targetObject = AvatarObjectReference.Get(objectProperty);
                try
                {
                    return targetObject?.GetComponent<Renderer>()?.sharedMaterials;
                }
                catch (MissingComponentException)
                {
                    return null;
                }
            }
        }
    }
}
