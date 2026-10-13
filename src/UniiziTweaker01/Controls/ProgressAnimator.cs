using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace UniiziTweaker01.Controls;

/// <summary>Attached behaviour: animates a ProgressBar smoothly to a target value.</summary>
public static class ProgressAnimator
{
    /// <summary>Set to false (reduced-motion) to jump to values without animation.</summary>
    public static bool AnimationsEnabled { get; set; } = true;

    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(double), typeof(ProgressAnimator), new PropertyMetadata(0.0, OnTargetChanged));

    public static double GetTarget(DependencyObject o) => (double)o.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject o, double value) => o.SetValue(TargetProperty, value);

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProgressBar bar) return;
        double target = (double)e.NewValue;
        if (!AnimationsEnabled)
        {
            bar.BeginAnimation(RangeBase.ValueProperty, null);
            bar.Value = target;
            return;
        }
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        bar.BeginAnimation(RangeBase.ValueProperty, animation);
    }
}
