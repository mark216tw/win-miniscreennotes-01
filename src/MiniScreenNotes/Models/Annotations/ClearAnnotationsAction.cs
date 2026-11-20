namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class ClearAnnotationsAction : IUndoableAction
{
    private readonly List<IAnnotationElement> _snapshot;

    public ClearAnnotationsAction(IEnumerable<IAnnotationElement> snapshot)
    {
        _snapshot = snapshot.ToList();
    }

    public void Execute(IList<IAnnotationElement> elements)
    {
        elements.Clear();
    }

    public void Undo(IList<IAnnotationElement> elements)
    {
        elements.Clear();
        foreach (var item in _snapshot)
        {
            elements.Add(item);
        }
    }
}
