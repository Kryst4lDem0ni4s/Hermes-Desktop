using System;
using HermesDesktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Views;

public sealed partial class IntegrationsPage : Page
{
    private static readonly ResourceLoader ResourceLoader = new();

    public IntegrationsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        RefreshTelegramDisplay();
        RefreshDiscordDisplay();
    }

    private void RefreshTelegramDisplay()
    {
        var token = HermesEnvironment.ReadIntegrationSetting("telegram_bot_token");
        var envConfigured = HermesEnvironment.TelegramConfigured;
        var hasToken = !string.IsNullOrWhiteSpace(token) || envConfigured;

        TelegramStatusText.Text = hasToken
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        TelegramMaskedText.Text = !string.IsNullOrWhiteSpace(token)
            ? MaskToken(token)
            : envConfigured ? "Set via environment variable" : "Not configured";
    }

    private void RefreshDiscordDisplay()
    {
        var token = HermesEnvironment.ReadIntegrationSetting("discord_bot_token");
        var envConfigured = HermesEnvironment.DiscordConfigured;
        var hasToken = !string.IsNullOrWhiteSpace(token) || envConfigured;

        DiscordStatusText.Text = hasToken
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        DiscordMaskedText.Text = !string.IsNullOrWhiteSpace(token)
            ? MaskToken(token)
            : envConfigured ? "Set via environment variable" : "Not configured";
    }

    private async void SaveTelegram_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = TelegramTokenBox.Password.Trim();
            if (string.IsNullOrEmpty(token))
            {
                TelegramSaveStatus.Text = "Token cannot be empty.";
                TelegramSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
                return;
            }

            await HermesEnvironment.SaveIntegrationTokenAsync("telegram_bot_token", token);
            TelegramSaveStatus.Text = "Saved. Restart to apply.";
            TelegramSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOnlineBrush"];
            TelegramTokenBox.Password = "";
            RefreshTelegramDisplay();
        }
        catch (Exception ex)
        {
            TelegramSaveStatus.Text = $"Error: {ex.Message}";
            TelegramSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
        }
    }

    private async void SaveDiscord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = DiscordTokenBox.Password.Trim();
            if (string.IsNullOrEmpty(token))
            {
                DiscordSaveStatus.Text = "Token cannot be empty.";
                DiscordSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
                return;
            }

            await HermesEnvironment.SaveIntegrationTokenAsync("discord_bot_token", token);
            DiscordSaveStatus.Text = "Saved. Restart to apply.";
            DiscordSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOnlineBrush"];
            DiscordTokenBox.Password = "";
            RefreshDiscordDisplay();
        }
        catch (Exception ex)
        {
            DiscordSaveStatus.Text = $"Error: {ex.Message}";
            DiscordSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenLogs();
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenConfig();
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "";
        if (token.Length <= 4) return "****";
        return "****" + token[^4..];
    }
}
