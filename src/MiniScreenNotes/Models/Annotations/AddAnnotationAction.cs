namespace Com.MiniScreenNotes.Models.Annotations;

public sealed class AddAnnotationAction : IUndoableAction
{
    private readonly IAnnotationElement _element;

    public AddAnnotationAction(IAnnotationElement element)
    {
        _element = element;
    }

    public void Execute(IList<IAnnotationElement> elements)
    {
        elements.Add(_element);
    }

    public void Undo(IList<IAnnotationElement> elements)
    {
        elements.Remove(_element);
    }
}
