namespace UniiziTweaker01.ViewModels;

/// <summary>Honest placeholder page for a module that is not implemented yet (it has no buttons).</summary>
public sealed class PendingModuleViewModel
{
    public PendingModuleViewModel(string title, string description, string plannedStep)
    {
        Title = title;
        Description = description;
        PlannedStep = plannedStep;
    }

    public string Title { get; }
    public string Description { get; }
    public string PlannedStep { get; }
}
