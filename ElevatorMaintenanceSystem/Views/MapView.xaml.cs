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
    private MapViewModel? _viewModel;
    private readonly JsonSerializerOptions _jsonOptions;

    public MapView()
    {
        InitializeComponent();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Get ViewModel from DataContext if not already set
        if (DataContext is MapViewModel viewModel && _viewModel == null)
        {
            _viewModel = viewModel;
        }
        else if (_viewModel == null)
        {
            try
            {
                _viewModel = Ioc.Default.GetRequiredService<MapViewModel>();
                DataContext = _viewModel;
            }
            catch
            {
                // Ioc not configured, rely on DataContext binding
            }
        }

        await InitializeWebViewAsync();
        if (_viewModel != null)
        {
            await _viewModel.LoadMapAsync();
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (_viewModel == null)
        {
            return;
        }

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

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_viewModel == null || string.IsNullOrWhiteSpace(e.WebMessageAsJson))
        {
            return;
        }

        try
        {
            var message = DeserializeMessage(e.WebMessageAsJson);

            if (message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("type", out var typeProperty))
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

                    case "elevatorFocused":
                        if (TryGetGuid(message, "elevatorId", out var focusedElevatorId))
                        {
                            await _viewModel.HandleElevatorFocusedAsync(
                                focusedElevatorId,
                                GetString(message, "elevatorTitle"));
                        }
                        break;

                    case "workerDroppedOnElevator":
                        if (TryGetGuid(message, "workerId", out var workerId) &&
                            TryGetGuid(message, "elevatorId", out var droppedElevatorId))
                        {
                            await _viewModel.HandleWorkerDroppedOnElevatorAsync(
                                workerId,
                                droppedElevatorId,
                                GetString(message, "workerTitle"),
                                GetString(message, "elevatorTitle"));
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

    private JsonElement DeserializeMessage(string webMessageAsJson)
    {
        var message = JsonSerializer.Deserialize<JsonElement>(webMessageAsJson, _jsonOptions);

        if (message.ValueKind == JsonValueKind.String)
        {
            var messageText = message.GetString();
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                message = JsonSerializer.Deserialize<JsonElement>(messageText, _jsonOptions);
            }
        }

        return message;
    }

    private static bool TryGetGuid(JsonElement message, string propertyName, out Guid value)
    {
        value = Guid.Empty;

        if (!message.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return Guid.TryParse(property.GetString(), out value);
    }

    private static string? GetString(JsonElement message, string propertyName)
    {
        if (!message.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void SendMapData()
    {
        if (_viewModel == null || MapWebView.CoreWebView2 == null)
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
