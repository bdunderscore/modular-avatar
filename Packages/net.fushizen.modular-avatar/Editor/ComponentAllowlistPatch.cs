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
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.modular_avatar.core.editor
{
    [InitializeOnLoad]
    internal static class ComponentAllowlistPatch
    {
        static ComponentAllowlistPatch()
        {
            try
            {
                PatchAllowlist();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static void PatchAllowlist()
        {
            // When running on non-VCC versions of the SDK, we can't reference AvatarValidation directly as it's not in
            // an assembly definition. So just search all of the assemblies for the type.
            var avatarValidation = FindType("VRC.SDK3.Validation.AvatarValidation");
            var validationUtils = FindType("VRC.SDKBase.Validation.ValidationUtils");
            if (avatarValidation == null)
            {
                Debug.LogError("Failed to find AvatarValidation type");
                return;
            }

            if (validationUtils == null)
            {
                Debug.LogError("Failed to find ValidationUtils type");
                return;
            }

            var getWhitelistForSDK =
                avatarValidation.GetMethod("GetComponentWhitelist", BindingFlags.Static | BindingFlags.NonPublic);
            var addDerivedClasses =
                validationUtils.GetMethod("AddDerivedClasses", BindingFlags.Static | BindingFlags.NonPublic);

            if (getWhitelistForSDK == null)
            {
                Debug.LogError("Failed to find GetWhitelistForSDK method");
                return;
            }

            if (addDerivedClasses == null)
            {
                Debug.LogError("Failed to find AddDerivedClasses method");
                return;
            }

            if (getWhitelistForSDK.Invoke(null, new object[] { }) is HashSet<Type> allowlist)
            {
                // The allowlist is cached, so we can inject our own type into the cached hashset (and then invoke the
                // AddDerivedClasses method to find all derived types from the AvatarTagComponent automatically).
                allowlist.Add(typeof(AvatarTagComponent));
                addDerivedClasses.Invoke(null, new object[] {allowlist});
            }
        }

        private static Type FindType(string typeName)
        {
            Type avatarValidation = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                avatarValidation = assembly.GetType(typeName);
                if (avatarValidation != null)
                {
                    break;
                }
            }

            return avatarValidation;
        }
    }
}