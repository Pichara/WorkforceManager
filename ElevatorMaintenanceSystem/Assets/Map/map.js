// Global variables
let map = null;
let markers = [];
let currentPopup = null;
let settings = null;

// Marker colors from UI spec
const MARKER_COLORS = {
    elevator: {
        fillColor: '#17324D',
        fillOpacity: 0.90,
        strokeColor: '#17324D',
        strokeWidth: 2,
        radius: 6
    },
    availableWorker: {
        fillColor: '#2F855A',
        fillOpacity: 0.85,
        strokeColor: '#2F855A',
        strokeWidth: 2,
        radius: 6
    },
    unavailableWorker: {
        fillColor: '#94A3B8',
        fillOpacity: 0.75,
        strokeColor: '#94A3B8',
        strokeWidth: 2,
        radius: 6
    },
    selected: {
        radius: 8,
        strokeWidth: 2,
        strokeColor: '#FFFFFF'
    }
};

// Initialize map when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    initializeMap();
});

function initializeMap() {
    // Initialize Leaflet map with performance optimizations per MAP-04
    map = L.map('map', {
        preferCanvas: true,
        zoomAnimation: true,
        fadeAnimation: true,
        markerZoomAnimation: true,
        center: [43.4516, -80.4925], // Waterloo/Kitchener
        zoom: 10
    });

    // Listen for map movement events
    map.on('moveend', onMapMoved);
    map.on('zoomend', onMapMoved);

    // Listen for map clicks (clear selection)
    map.on('click', onMapBackgroundClick);

    // Notify host that map is ready
    postMessage({ type: 'mapReady' });
}

// Create tile layer from settings
function createTileLayer(tileSettings) {
    if (!tileSettings || !tileSettings.urlTemplate) {
        return null;
    }

    const urlTemplate = tileSettings.urlTemplate.replace('{apiKey}', tileSettings.apiKey || '');

    return L.tileLayer(urlTemplate, {
        attribution: tileSettings.attribution || '',
        maxZoom: tileSettings.maxZoom || 19
    });
}

// Load map data from host
window.chrome?.webview?.addEventListener('message', function(event) {
    try {
        const data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        updateMapData(data);
    } catch (error) {
        console.error('Failed to parse map data:', error);
    }
});

// Update map with new data from host
function updateMapData(data) {
    if (!map || !data) return;

    // Store settings
    settings = data;

    // Clear existing markers
    clearMarkers();

    // Create tile layers
    const standardLayer = createTileLayer(data.standardTiles);
    const satelliteLayer = createTileLayer(data.satelliteTiles);

    // Add layers control per D-05
    if (standardLayer && satelliteLayer) {
        const baseLayers = {
            'Standard': standardLayer,
            'Satellite': satelliteLayer
        };

        // Remove existing layer control if any
        map.eachLayer(function(layer) {
            if (layer instanceof L.Control.Layers) {
                map.removeControl(layer);
            }
        });

        L.control.layers(baseLayers, null, { position: 'topright' }).addTo(map);

        // Set default base layer per D-03 and D-05
        const defaultLayerName = data.defaultBaseLayer === 'satellite' ? 'Satellite' : 'Standard';
        const defaultLayer = defaultLayerName === 'Satellite' ? satelliteLayer : standardLayer;
        defaultLayer.addTo(map);
    }

    // Add markers per D-02
    if (data.markers && Array.isArray(data.markers)) {
        data.markers.forEach(markerData => {
            const marker = createMarker(markerData);
            if (marker) {
                markers.push(marker);
                marker.addTo(map);
            }
        });
    }

    // Set view from data or preserve current state
    if (data.centerLat !== undefined && data.centerLng !== undefined && data.zoom !== undefined) {
        // Preserve current center/zoom if map is already initialized and user has moved
        const currentCenter = map.getCenter();
        const currentZoom = map.getZoom();

        // Use provided center/zoom for first load, preserve if already initialized
        if (markers.length === 0 || Math.abs(currentCenter.lat - data.centerLat) > 0.001) {
            map.setView([data.centerLat, data.centerLng], data.zoom, { animate: false });
        }
    }
}

// Create a marker from data
function createMarker(markerData) {
    if (!markerData || !markerData.latitude || !markerData.longitude) {
        return null;
    }

    const config = getMarkerConfig(markerData.kind);
    if (!config) return null;

    // Create circle marker per UI spec
    const marker = L.circleMarker([markerData.latitude, markerData.longitude], {
        radius: config.radius,
        fillColor: config.fillColor,
        fillOpacity: config.fillOpacity,
        color: config.strokeColor,
        weight: config.strokeWidth,
        className: 'map-marker'
    });

    // Create popup content
    const popupContent = createPopupContent(markerData);

    // Add click handler
    marker.on('click', function(e) {
        L.DomEvent.stopPropagation(e); // Prevent map click

        // Close existing popup
        if (currentPopup) {
            currentPopup.closePopup();
        }

        // Open new popup
        currentPopup = marker.bindPopup(popupContent).openPopup();

        // Highlight selected marker
        highlightMarker(marker);

        // Notify host
        postMessage({
            type: 'markerSelected',
            title: markerData.title || '',
            detail: markerData.detailLines ? markerData.detailLines.join('\n') : ''
        });
    });

    return marker;
}

// Get marker config based on kind
function getMarkerConfig(kind) {
    switch (kind) {
        case 'elevator':
        case 'Elevator':
            return MARKER_COLORS.elevator;
        case 'availableWorker':
        case 'AvailableWorker':
            return MARKER_COLORS.availableWorker;
        case 'unavailableWorker':
        case 'UnavailableWorker':
            return MARKER_COLORS.unavailableWorker;
        default:
            return MARKER_COLORS.elevator;
    }
}

// Create popup HTML content
function createPopupContent(markerData) {
    let html = `<h3>${escapeHtml(markerData.title || '')}</h3>`;

    if (markerData.detailLines && Array.isArray(markerData.detailLines)) {
        markerData.detailLines.forEach(line => {
            html += `<p>${escapeHtml(line)}</p>`;
        });
    }

    return html;
}

// Highlight selected marker
function highlightMarker(selectedMarker) {
    // Reset all markers to default
    markers.forEach(marker => {
        const originalConfig = getMarkerConfigFromElement(marker);
        if (originalConfig) {
            marker.setStyle({
                radius: originalConfig.radius,
                color: originalConfig.strokeColor,
                weight: originalConfig.strokeWidth
            });
        }
    });

    // Highlight selected marker
    if (selectedMarker) {
        selectedMarker.setStyle({
            radius: MARKER_COLORS.selected.radius,
            color: MARKER_COLORS.selected.strokeColor,
            weight: MARKER_COLORS.selected.strokeWidth + 1
        });
    }
}

// Get marker config from marker element
function getMarkerConfigFromElement(marker) {
    // This is a simplified approach - in production you'd store original config
    // For now, we'll use the default configs
    return MARKER_COLORS.elevator;
}

// Clear all markers
function clearMarkers() {
    markers.forEach(marker => {
        map.removeLayer(marker);
    });
    markers = [];
    currentPopup = null;
}

// Handle map movement
function onMapMoved() {
    if (!map) return;

    const center = map.getCenter();
    postMessage({
        type: 'mapMoved',
        lat: center.lat,
        lng: center.lng,
        zoom: map.getZoom()
    });
}

// Handle map background click
function onMapBackgroundClick() {
    // Clear selection
    if (currentPopup) {
        currentPopup.closePopup();
        currentPopup = null;
    }

    // Reset marker highlights
    markers.forEach(marker => {
        const originalConfig = getMarkerConfigFromElement(marker);
        if (originalConfig) {
            marker.setStyle({
                radius: originalConfig.radius,
                color: originalConfig.strokeColor,
                weight: originalConfig.strokeWidth
            });
        }
    });

    // Notify host
    postMessage({ type: 'mapCleared' });
}

// Listen for base layer changes
map?.on('baselayerchange', function(event) {
    const layerName = event.layer ? (event.layer._url ? 'satellite' : 'standard') : 'standard';
    postMessage({
        type: 'baseLayerChanged',
        layer: layerName
    });
});

// Post message to host
function postMessage(message) {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
        window.chrome.webview.postMessage(JSON.stringify(message));
    }
}

// Escape HTML to prevent XSS
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Listen for layer control changes (manual detection)
document.addEventListener('baselayerchange', function(event) {
    if (event.layer && event.layer._url) {
        postMessage({
            type: 'baseLayerChanged',
            layer: 'satellite'
        });
    } else {
        postMessage({
            type: 'baseLayerChanged',
            layer: 'standard'
        });
    }
});
