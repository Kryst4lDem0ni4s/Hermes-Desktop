using System;
using System.Linq;
using HermesDesktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly ResourceLoader ResourceLoader = new();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    public string HermesHomePath => HermesEnvironment.DisplayHermesHomePath;

    public string HermesConfigPath => HermesEnvironment.DisplayHermesConfigPath;

    public string HermesLogsPath => HermesEnvironment.DisplayHermesLogsPath;

    public string HermesWorkspacePath => HermesEnvironment.DisplayHermesWorkspacePath;

    public string TelegramStatus => HermesEnvironment.TelegramConfigured
        ? ResourceLoader.GetString("StatusDetected")
        : ResourceLoader.GetString("StatusNotDetected");

    public string DiscordStatus => HermesEnvironment.DiscordConfigured
        ? ResourceLoader.GetString("StatusDetected")
        : ResourceLoader.GetString("StatusNotDetected");

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Pre-populate fields from current config
        var provider = HermesEnvironment.ModelProvider.ToLowerInvariant();
        var matchIndex = provider switch
        {
            "openai" => 0,
            "anthropic" => 1,
            _ => 2
        };
        ProviderCombo.SelectedIndex = matchIndex;

        BaseUrlBox.Text = HermesEnvironment.ModelBaseUrl;
        ModelBox.Text = HermesEnvironment.DefaultModel;
        ApiKeyBox.Password = HermesEnvironment.ModelApiKey ?? "";
    }

    private async void SaveModelConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var providerTag = (ProviderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";
            var baseUrl = BaseUrlBox.Text.Trim();
            var model = ModelBox.Text.Trim();
            var apiKey = ApiKeyBox.Password.Trim();

            if (string.IsNullOrEmpty(model))
            {
                ModelSaveStatus.Text = "Model name is required.";
                ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
                return;
            }

            await HermesEnvironment.SaveModelConfigAsync(providerTag, baseUrl, model, apiKey);
            ModelSaveStatus.Text = "Saved successfully. Restart to apply.";
            ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOnlineBrush"];
        }
        catch (Exception ex)
        {
            ModelSaveStatus.Text = $"Error: {ex.Message}";
            ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
        }
    }

    private void OpenHome_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenHermesHome();
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenConfig();
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenLogs();
    }

    private void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenWorkspace();
    }
}
