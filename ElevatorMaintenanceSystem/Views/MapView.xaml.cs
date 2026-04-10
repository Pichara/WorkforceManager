using CommunityToolkit.Mvvm.DependencyInjection;
using ElevatorMaintenanceSystem.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace ElevatorMaintenanceSystem.Views;

/// <summary>
/// Interaction logic for MapView.xaml
/// </summary>
public partial class MapView : UserControl
{
    private static readonly TimeSpan SettledLayoutDelay = TimeSpan.FromMilliseconds(120);

    private MapViewModel? _viewModel;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _isWebViewInitialized;
    private bool _isMapReady;
    private CancellationTokenSource? _layoutRefreshCts;
    private Size _lastQueuedSurfaceSize = Size.Empty;
    private Size _lastRefreshedSurfaceSize = Size.Empty;

    public MapView()
    {
        InitializeComponent();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
        MapWebView.SizeChanged += OnMapWebViewSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MapViewModel viewModel)
        {
            AttachViewModel(viewModel);
        }
        else if (_viewModel == null)
        {
            try
            {
                var resolvedViewModel = Ioc.Default.GetRequiredService<MapViewModel>();
                DataContext = resolvedViewModel;
                AttachViewModel(resolvedViewModel);
            }
            catch
            {
                // Ioc not configured, rely on DataContext binding
            }
        }

        await InitializeWebViewAsync();

        if (_viewModel != null && !_viewModel.IsInitialized)
        {
            await _viewModel.LoadMapAsync();
        }

        SendMapData();
        await QueueMapLayoutRefreshAsync(requireMeasuredSurface: true, force: true);
    }

    private async Task InitializeWebViewAsync()
    {
        if (_viewModel == null)
        {
            return;
        }

        if (_isWebViewInitialized)
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

            MapWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            MapWebView.NavigationCompleted += OnNavigationCompleted;
            _isMapReady = false;
            _isWebViewInitialized = true;

            // Navigate to map.html
            MapWebView.CoreWebView2.Navigate("https://appassets.example/map.html");
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
                        _isMapReady = true;
                        SendMapData();
                        await QueueMapLayoutRefreshAsync(requireMeasuredSurface: true, force: true);
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
                                detail.GetString() ?? string.Empty,
                                GetString(message, "kind"));
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

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            await QueueMapLayoutRefreshAsync(requireMeasuredSurface: true, force: true);
        }
    }

    private async void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            SendMapData();
            await QueueMapLayoutRefreshAsync(requireMeasuredSurface: false, force: true);
        }
    }

    private void OnMapWebViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!HasMeasuredSurface(e.NewSize))
        {
            return;
        }

        if (AreSizesEquivalent(_lastQueuedSurfaceSize, e.NewSize))
        {
            return;
        }

        _lastQueuedSurfaceSize = e.NewSize;
        _ = QueueMapLayoutRefreshAsync(requireMeasuredSurface: true, force: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.LastSnapshotJson))
        {
            SendMapData();
        }
    }

    private void AttachViewModel(MapViewModel viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
        if (_viewModel == null || MapWebView.CoreWebView2 == null || !_isMapReady)
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

    private async Task RefreshMapLayoutAsync()
    {
        if (MapWebView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            await MapWebView.CoreWebView2.ExecuteScriptAsync("window.codexHostRefreshMap?.();");
        }
        catch (Exception)
        {
            // Ignore layout refresh failures
        }
    }

    private async Task QueueMapLayoutRefreshAsync(bool requireMeasuredSurface, bool force)
    {
        var currentSurfaceSize = GetCurrentSurfaceSize();
        if (requireMeasuredSurface && !HasMeasuredSurface(currentSurfaceSize))
        {
            return;
        }

        if (!force && HasMeasuredSurface(currentSurfaceSize) && AreSizesEquivalent(_lastRefreshedSurfaceSize, currentSurfaceSize))
        {
            return;
        }

        _layoutRefreshCts?.Cancel();
        _layoutRefreshCts?.Dispose();

        var refreshCts = new CancellationTokenSource();
        _layoutRefreshCts = refreshCts;

        try
        {
            await Task.Delay(SettledLayoutDelay, refreshCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var settledSurfaceSize = GetCurrentSurfaceSize();
        if (requireMeasuredSurface && !HasMeasuredSurface(settledSurfaceSize))
        {
            return;
        }

        if (!force && HasMeasuredSurface(settledSurfaceSize) && AreSizesEquivalent(_lastRefreshedSurfaceSize, settledSurfaceSize))
        {
            return;
        }

        _lastRefreshedSurfaceSize = settledSurfaceSize;
        SendMapData();
        await RefreshMapLayoutAsync();
    }

    private Size GetCurrentSurfaceSize() => new(MapWebView.ActualWidth, MapWebView.ActualHeight);

    private static bool HasMeasuredSurface(Size size) => size.Width > 1 && size.Height > 1;

    private static bool AreSizesEquivalent(Size left, Size right)
    {
        return Math.Abs(left.Width - right.Width) < 0.5 &&
               Math.Abs(left.Height - right.Height) < 0.5;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _layoutRefreshCts?.Cancel();
        _layoutRefreshCts?.Dispose();
        _layoutRefreshCts = null;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }
}
