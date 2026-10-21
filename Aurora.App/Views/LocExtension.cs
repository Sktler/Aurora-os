using System;
using System.Windows.Markup;
using System.Windows.Data;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    /// <summary>XAML markup extension: <c>Text="{local:Loc Settings.Title}"</c>.</summary>
    [MarkupExtensionReturnType(typeof(Binding))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key) => Key = key;

        [ConstructorArgument("key")]
        public string Key { get; set; } = "";

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new Binding($"[{Key}]")
            {
                Source = Localizer.Resources,
                Mode = BindingMode.OneWay
            };
    }
}
