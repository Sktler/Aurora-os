using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using Aurora.App.Services;

namespace Aurora.App.Views;

/// <summary>Refreshes an explicit UI status or formatted label when its value or UI language changes.
/// Apply only to application labels, never to user content such as names or messages.</summary>
[MarkupExtensionReturnType(typeof(MultiBinding))]
public sealed class LocBindingExtension : MarkupExtension
{
    public LocBindingExtension() { }
    public LocBindingExtension(string path) => Path = path;
    [ConstructorArgument("path")]
    public string Path { get; set; } = "";
    public string? Template { get; set; }
    public string TrueText { get; set; } = "Yes";
    public string FalseText { get; set; } = "No";
    public RelativeSource? RelativeSource { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) => CreateBinding().ProvideValue(serviceProvider);

    internal MultiBinding CreateBinding()
    {
        var binding = new MultiBinding { Mode = BindingMode.OneWay, Converter = new LabelConverter(Template, TrueText, FalseText) };
        var value = new Binding(Path);
        if (RelativeSource != null) value.RelativeSource = RelativeSource;
        binding.Bindings.Add(value);
        binding.Bindings.Add(new Binding(nameof(LocalizationResources.Revision)) { Source = Localizer.Resources });
        return binding;
    }
    private sealed class LabelConverter(string? template, string trueText, string falseText) : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] == System.Windows.DependencyProperty.UnsetValue) return "";
            var value = values[0] is bool flag ? Localizer.GetUiText(flag ? trueText : falseText) : values[0];
            return template == null ? Localizer.GetUiText(value?.ToString() ?? "") : Localizer.FormatUiText(template, value);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
