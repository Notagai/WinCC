namespace WinCC.Layout;

/// <summary>All geometry mutations pass through this transaction. Accessors return detached snapshots.</summary>
public sealed class EditorSession
{
    private LayoutDocument saved;
    private LayoutDocument? draft;
    private readonly List<LayoutDocument> undo = [];
    private readonly List<LayoutDocument> redo = [];
    public bool IsEditing => draft is not null;
    public bool CanUndo => IsEditing && undo.Count > 0;
    public bool CanRedo => IsEditing && redo.Count > 0;
    public bool IsDirty => IsEditing && !LayoutJson.Serialize(saved).SequenceEqual(LayoutJson.Serialize(draft!));
    public LayoutDocument Current => (draft ?? saved).Copy();
    public EditorSession(LayoutDocument initial) => saved = initial.Copy();
    public void Enter()
    {
        if (IsEditing) return;
        draft = saved.Copy(); undo.Clear(); redo.Clear();
    }
    public void Change(Action<LayoutDocument> mutation)
    {
        RequireEdit();
        var next = draft!.Copy();
        mutation(next);
        LayoutValidator.Validate(next);
        if (LayoutJson.Serialize(next).SequenceEqual(LayoutJson.Serialize(draft))) return;
        undo.Add(draft); if (undo.Count > 100) undo.RemoveAt(0);
        draft = next.Copy(); redo.Clear(); // Do not retain references supplied by a mutation callback.
    }
    public void Update(Widget widget) => Change(d =>
    {
        int i = d.Active.Widgets.FindIndex(w => w.Id == widget.Id);
        if (i < 0) throw new InvalidDataException("Unknown widget.");
        d.Active.Widgets[i] = widget;
    });
    public void Undo()
    {
        RequireEdit(); if (!CanUndo) return;
        redo.Add(draft!); draft = undo[^1]; undo.RemoveAt(undo.Count - 1);
    }
    public void Redo()
    {
        RequireEdit(); if (!CanRedo) return;
        undo.Add(draft!); draft = redo[^1]; redo.RemoveAt(redo.Count - 1);
    }
    public void Cancel() { RequireEdit(); draft = null; undo.Clear(); redo.Clear(); }
    public void Done(ILayoutStore store)
    {
        RequireEdit(); var committed = draft!.Copy();
        store.Save(committed.Copy()); // Failure deliberately leaves the complete edit session intact.
        saved = committed; draft = null; undo.Clear(); redo.Clear();
    }
    private void RequireEdit()
    {
        if (!IsEditing) throw new InvalidOperationException("Layout changes require Edit Mode.");
    }
}
