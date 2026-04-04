using HermesDesktop.Services;
using Hermes.Agent.LLM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Views;

public sealed partial class DashboardPage : Page
{
    private static readonly ResourceLoader ResourceLoader = new();

    public DashboardPage()
    {
        InitializeComponent();
    }

    // ── Data Properties ──

    public string ModelProvider => HermesEnvironment.DisplayModelProvider;
    public string DefaultModel => HermesEnvironment.DisplayDefaultModel;
    public string BaseUrl => HermesEnvironment.DisplayModelBaseUrl;
    public string HermesHomePath => HermesEnvironment.DisplayHermesHomePath;
    public string HermesConfigPath => HermesEnvironment.DisplayHermesConfigPath;
    public string HermesLogsPath => HermesEnvironment.DisplayHermesLogsPath;
    public string HermesWorkspacePath => HermesEnvironment.DisplayHermesWorkspacePath;
    public string ModelPort => HermesEnvironment.DisplayModelPort;

    public string CliState => HermesEnvironment.HermesInstalled
        ? ResourceLoader.GetString("StatusInstalled")
        : ResourceLoader.GetString("StatusMissing");

    public string InstallSummary => HermesEnvironment.HermesInstalled
        ? ResourceLoader.GetString("CliReadySummary")
        : ResourceLoader.GetString("CliMissingSummary");

    public string MessagingState => HermesEnvironment.HasAnyMessagingToken
        ? ResourceLoader.GetString("StatusReady")
        : ResourceLoader.GetString("StatusNeedsSetup");

    public string MessagingSummary => HermesEnvironment.HasAnyMessagingToken
        ? ResourceLoader.GetString("MessagingReadySummary")
        : ResourceLoader.GetString("MessagingSetupSummary");

    public string ApiKeyMasked
    {
        get
        {
            var key = HermesEnvironment.ModelApiKey;
            if (string.IsNullOrEmpty(key)) return "Not configured";
            return key.Length > 4
                ? "****" + key[^4..]
                : "****";
        }
    }

    // ── Tab Switching ──

    private void StatusTab_Click(object sender, TappedRoutedEventArgs e) => SwitchTab("status");
    private void ModelTab_Click(object sender, TappedRoutedEventArgs e) => SwitchTab("model");
    private void ShellTab_Click(object sender, TappedRoutedEventArgs e) => SwitchTab("shell");

    private void SwitchTab(string tab)
    {
        var accent = (SolidColorBrush)Application.Current.Resources["AppAccentTextBrush"];
        var muted = (SolidColorBrush)Application.Current.Resources["AppTextSecondaryBrush"];
        var accentBar = (SolidColorBrush)Application.Current.Resources["AppAccentBrush"];
        var transparent = new SolidColorBrush(Colors.Transparent);

        StatusTabText.Foreground = tab == "status" ? accent : muted;
        StatusTabIndicator.Background = tab == "status" ? accentBar : transparent;
        StatusPanel.Visibility = tab == "status" ? Visibility.Visible : Visibility.Collapsed;

        ModelTabText.Foreground = tab == "model" ? accent : muted;
        ModelTabIndicator.Background = tab == "model" ? accentBar : transparent;
        ModelPanel.Visibility = tab == "model" ? Visibility.Visible : Visibility.Collapsed;

        ShellTabText.Foreground = tab == "shell" ? accent : muted;
        ShellTabIndicator.Background = tab == "shell" ? accentBar : transparent;
        ShellPanel.Visibility = tab == "shell" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Test Connection ──

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        TestConnectionResult.Text = "Testing...";
        TestConnectionResult.Foreground = (SolidColorBrush)Application.Current.Resources["AppTextSecondaryBrush"];

        try
        {
            var chatClient = App.Services?.GetService<IChatClient>();
            if (chatClient is null)
            {
                TestConnectionResult.Text = "Chat client not configured";
                return;
            }

            var messages = new List<Hermes.Agent.Core.Message>
            {
                new() { Role = "user", Content = "Reply with exactly: OK" }
            };
            var result = await chatClient.CompleteAsync(messages, CancellationToken.None);
            TestConnectionResult.Text = $"Connected — response: {result.Trim()}";
            TestConnectionResult.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 73, 194, 125));
        }
        catch (Exception ex)
        {
            TestConnectionResult.Text = $"Failed: {ex.Message}";
            TestConnectionResult.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 100, 100));
        }
    }

    // ── Actions ──

    private void LaunchHermesChat_Click(object sender, RoutedEventArgs e) => HermesEnvironment.LaunchHermesChat();
    private void OpenLogs_Click(object sender, RoutedEventArgs e) => HermesEnvironment.OpenLogs();
    private void OpenConfig_Click(object sender, RoutedEventArgs e) => HermesEnvironment.OpenConfig();
}
