/*
 * MIT License
 * 
 * Copyright (c) 2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace net.fushizen.modular_avatar.core.editor
{
    [InitializeOnLoad]
    internal static class ComponentAllowlistPatch
    {
        static ComponentAllowlistPatch()
        {
            // When running on non-VCC versions of the SDK, we can't reference AvatarValidation directly as it's not in
            // an assembly definition. So just search all of the assemblies for the type.
            string typeName = "VRC.SDK3.Validation.AvatarValidation";
            Type avatarValidationType = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                avatarValidationType = assembly.GetType(typeName);
                if (avatarValidationType != null)
                {
                    break;
                }
            }

            if (avatarValidationType == null) return;

            var listField = avatarValidationType.GetField("ComponentTypeWhiteListCommon",
                BindingFlags.Static | BindingFlags.Public);
            var currentList = new List<string>(listField.GetValue(null) as string[]);
            currentList.Add(typeof(AvatarTagComponent).FullName);
            listField.SetValue(null, currentList.ToArray());
        }
    }
}