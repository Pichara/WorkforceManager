using CommunityToolkit.Mvvm.DependencyInjection;
using ElevatorMaintenanceSystem.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace ElevatorMaintenanceSystem.Views;

/// <summary>
/// Interaction logic for MapView.xaml
/// </summary>
public partial class MapView : UserControl
{
    private readonly MapViewModel _viewModel;
    private readonly JsonSerializerOptions _jsonOptions;

    public MapView()
    {
        InitializeComponent();

        _viewModel = Ioc.Default.GetRequiredService<MapViewModel>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
        await _viewModel.LoadMapAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await MapWebView.EnsureCoreWebView2Async();

            // Map Assets/Map folder to virtual host
            var mapAssetsPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "Map");

            MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets.example",
                mapAssetsPath,
                CoreWebView2HostResourceAccessKind.Allow);

            // Navigate to map.html
            MapWebView.CoreWebView2.Navigate("https://appassets.example/map.html");

            // Subscribe to web messages
            MapWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = "WebView2 initialization failed.";
            _viewModel.MapErrorMessage = ex.Message;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.WebMessageAsJson))
        {
            return;
        }

        try
        {
            var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson, _jsonOptions);

            if (message.TryGetProperty("type", out var typeProperty))
            {
                var messageType = typeProperty.GetString();

                switch (messageType)
                {
                    case "mapReady":
                        // Map is ready, send initial data
                        SendMapData();
                        break;

                    case "mapMoved":
                        // User moved the map, update viewmodel state
                        if (message.TryGetProperty("lat", out var lat) &&
                            message.TryGetProperty("lng", out var lng) &&
                            message.TryGetProperty("zoom", out var zoom))
                        {
                            _viewModel.ApplyMapState(
                                lat.GetDouble(),
                                lng.GetDouble(),
                                zoom.GetInt32(),
                                _viewModel.SelectedBaseLayer);
                        }
                        break;

                    case "baseLayerChanged":
                        // User changed base layer
                        if (message.TryGetProperty("layer", out var layer))
                        {
                            _viewModel.SelectedBaseLayer = layer.GetString() ?? "standard";
                        }
                        break;

                    case "markerSelected":
                        // User clicked a marker
                        if (message.TryGetProperty("title", out var title) &&
                            message.TryGetProperty("detail", out var detail))
                        {
                            _viewModel.SetSelectedItem(
                                title.GetString() ?? string.Empty,
                                detail.GetString() ?? string.Empty);
                        }
                        break;

                    case "mapCleared":
                        // User clicked empty map space
                        _viewModel.ClearSelectedItem();
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages
        }
    }

    private void SendMapData()
    {
        if (MapWebView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            var snapshotJson = _viewModel.LastSnapshotJson;

            if (!string.IsNullOrWhiteSpace(snapshotJson))
            {
                MapWebView.CoreWebView2.PostWebMessageAsJson(snapshotJson);
            }
        }
        catch (Exception)
        {
            // Ignore post message failures
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (MapWebView?.CoreWebView2 != null)
        {
            MapWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }
    }
}
