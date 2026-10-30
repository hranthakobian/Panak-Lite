using System;
using System.Windows.Data;
using System.Windows.Markup;
using Panak.Services;

namespace Panak.Markup {
    [MarkupExtensionReturnType(typeof(string))]
    public class TranslateExtension : MarkupExtension {
        public string Key { get; set; } = string.Empty;

        public TranslateExtension() { }

        public TranslateExtension(string key) {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) {
            if (string.IsNullOrEmpty(Key)) {
                return string.Empty;
            }

            var binding = new Binding($"[{Key}]") {
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
