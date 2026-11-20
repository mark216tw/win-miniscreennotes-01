namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class RemoveAnnotationsAction : IUndoableAction
{
    private readonly List<(int Index, IAnnotationElement Element)> _removed;

    public RemoveAnnotationsAction(IEnumerable<(int Index, IAnnotationElement Element)> removed)
    {
        _removed = removed.OrderBy(x => x.Index).ToList();
    }

    public void Execute(IList<IAnnotationElement> elements)
    {
        foreach (var item in _removed)
        {
            elements.Remove(item.Element);
        }
    }

    public void Undo(IList<IAnnotationElement> elements)
    {
        foreach (var (index, element) in _removed)
        {
            var insertAt = Math.Clamp(index, 0, elements.Count);
            elements.Insert(insertAt, element);
        }
    }
}
