#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    internal sealed class ExpressionEvaluationContext
    {
        private readonly Func<string, float> _parameterResolver;
        private readonly Func<GameObject, bool>? _objectActiveResolver;

        public ExpressionEvaluationContext(
            Func<string, float> parameterResolver,
            Func<GameObject, bool>? objectActiveResolver = null)
        {
            _parameterResolver = parameterResolver ?? throw new ArgumentNullException(nameof(parameterResolver));
            _objectActiveResolver = objectActiveResolver;
        }

        public float GetParameter(string parameterName)
        {
            return _parameterResolver(parameterName);
        }

        public bool GetObjectActive(GameObject target)
        {
            return _objectActiveResolver?.Invoke(target) ?? target.activeSelf;
        }
    }
}