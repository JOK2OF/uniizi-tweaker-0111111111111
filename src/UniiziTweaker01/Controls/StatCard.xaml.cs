using System.Windows;
using System.Windows.Controls;

namespace UniiziTweaker01.Controls;

public partial class StatCard : UserControl
{
    public StatCard() => InitializeComponent();

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(StatCard), new PropertyMetadata(""));
    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(nameof(Percent), typeof(double), typeof(StatCard), new PropertyMetadata(0.0));
    public static readonly DependencyProperty HasBarProperty =
        DependencyProperty.Register(nameof(HasBar), typeof(bool), typeof(StatCard), new PropertyMetadata(false));
    public static readonly DependencyProperty ValueSizeProperty =
        DependencyProperty.Register(nameof(ValueSize), typeof(double), typeof(StatCard), new PropertyMetadata(26.0));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public double Percent { get => (double)GetValue(PercentProperty); set => SetValue(PercentProperty, value); }
    public bool HasBar { get => (bool)GetValue(HasBarProperty); set => SetValue(HasBarProperty, value); }
    public double ValueSize { get => (double)GetValue(ValueSizeProperty); set => SetValue(ValueSizeProperty, value); }
}
