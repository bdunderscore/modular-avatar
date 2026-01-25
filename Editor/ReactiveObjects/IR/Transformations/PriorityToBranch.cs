namespace nadena.dev.modular_avatar.core.editor.rc.transformations
{
    internal static class PriorityToBranch
    {
        public static void Transform(ref IMotionNode root)
        {
            if (root is PriorityNode pn)
            {
                if (pn.Conditions.Count == 1)
                {
                    var branch = pn.Conditions[0].Item1;
                    root = branch.Flatten(pn.DefaultMotion, pn.Conditions[0].Item2);
                }
            }

            root.WalkTree(Transform);
        }
    }
}