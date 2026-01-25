using nadena.dev.ndmf.animator;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    public interface IAction
    {
        public object TargetKey { get; }
        public void ToMotion(BakeContext context, VirtualClip motion);
    }
}