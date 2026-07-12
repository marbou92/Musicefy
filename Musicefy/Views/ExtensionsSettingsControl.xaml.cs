using System;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class ExtensionsSettingsControl : UserControl
    {
        public ExtensionsSettingsControl()
        {
            InitializeComponent();

            // DataContext is normally set externally by SettingsPage.ShowExtensions()
            // via App.Services.GetService<ExtensionsSettingsViewModel>(). Provide a
            // fallback here so the control also works standalone (e.g. designer).
            if (DataContext == null)
            {
                try
                {
                    DataContext = App.Services?.GetService<ExtensionsSettingsViewModel>();
                }
                catch
                {
                    // Designer / very early init — ignore.
                }
            }
        }
    }
}
