namespace nadena.dev.ndmf.animation
{
    /// <summary>
    /// This interface tags components which supply additional animation controllers for merging. They will be given
    /// an opportunity to apply animation path updates when the TrackObjectRenamesContext is committed.
    /// </summary>
    public interface IOnCommitObjectRenames
    {
        void OnCommitObjectRenames(BuildContext buildContext, TrackObjectRenamesContext renameContext);
    }
}