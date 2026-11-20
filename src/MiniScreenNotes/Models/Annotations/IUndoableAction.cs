namespace Com.MiniScreenNotes.Models.Annotations;

public interface IUndoableAction
{
    void Execute(IList<IAnnotationElement> elements);
    void Undo(IList<IAnnotationElement> elements);
}
