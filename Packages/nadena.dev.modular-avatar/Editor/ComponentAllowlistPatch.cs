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
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [InitializeOnLoad]
    internal static class ComponentAllowlistPatch
    {
        internal static readonly bool PATCH_OK;

        static ComponentAllowlistPatch()
        {
            try
            {
                PatchAllowlist();
                PATCH_OK = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PATCH_OK = false;
            }
        }

        static void PatchAllowlist()
        {
            // The below APIs are all public, but undocumented and likely to change in the future.
            // As such, we use reflection to access them (allowing us to catch exceptions instead of just breaking the
            // build - and allowing the user to manually bake as a workaround).

            // The basic idea is to retrieve the HashSet of whitelisted components, and add all components extending
            // from AvatarTagComponent to it. This HashSet is cached on first access, but the lists of allowed
            // components used to initially populate it are private. So, we'll start off by making a call that (as a
            // side-effect) causes the list to be initially cached. This call will throw a NPE because we're passing
            // a null GameObject, but that's okay.

            var avatarValidation = Util.FindType("VRC.SDK3.Validation.AvatarValidation");
            var findIllegalComponents =
                avatarValidation?.GetMethod("FindIllegalComponents", BindingFlags.Public | BindingFlags.Static);

            if (findIllegalComponents == null)
            {
                Debug.LogError(
                    "[ModularAvatar] Unsupported VRCSDK version: Failed to find AvatarValidation.FindIllegalComponents");
                return;
            }

            try
            {
                findIllegalComponents.Invoke(null, new[] {(object) null});
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException is NullReferenceException)
                {
                    // ok!
                }
                else
                {
                    System.Diagnostics.Debug.Assert(e.InnerException != null, "e.InnerException != null");
                    throw e.InnerException;
                }
            }

            // Now fetch the cached allowlist and add our components to it.
            var validationUtils = Util.FindType("VRC.SDKBase.Validation.ValidationUtils");
            var whitelistedTypes = validationUtils?.GetMethod(
                "WhitelistedTypes",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] {typeof(string), typeof(IEnumerable<Type>)},
                null
            );

            if (whitelistedTypes == null)
            {
                Debug.LogError(
                    "[ModularAvatar] Unsupported VRCSDK version: Failed to find ValidationUtils.WhitelistedTypes");
                return;
            }

            var allowlist = whitelistedTypes.Invoke(null, new object[] {"avatar-sdk3", null}) as HashSet<Type>;
            if (allowlist == null)
            {
                Debug.LogError("[ModularAvatar] Unsupported VRCSDK version: Failed to retrieve component whitelist");
                return;
            }

            allowlist.Add(typeof(AvatarTagComponent));

            // We'll need to find all types which derive from AvatarTagComponent and inject them into the allowlist
            // as well.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var ty in assembly.GetTypes())
                {
                    if (typeof(AvatarTagComponent).IsAssignableFrom(ty))
                    {
                        allowlist.Add(ty);
                    }
                }
            }
        }
    }
}