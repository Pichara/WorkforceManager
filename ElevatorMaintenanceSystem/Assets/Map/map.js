let map = null;
let markerEntries = [];
let currentPopupEntry = null;
let satelliteLayer = null;

const DROP_TARGET_RADIUS_PX = 24;

const MARKER_STYLES = {
    elevator: {
        fillColor: '#17324D',
        fillOpacity: 0.90,
        strokeColor: '#17324D',
        strokeWidth: 2,
        radius: 6,
        assigned: false,
        markerType: 'circle'
    },
    availableWorker: {
        fillColor: '#2F855A',
        fillOpacity: 0.85,
        strokeColor: '#2F855A',
        strokeWidth: 2,
        radius: 6,
        assigned: false,
        markerType: 'worker'
    },
    assignedAvailableWorker: {
        fillColor: '#2F855A',
        fillOpacity: 0.85,
        strokeColor: '#FFFFFF',
        strokeWidth: 3,
        radius: 6,
        assigned: true,
        markerType: 'worker'
    },
    unavailableWorker: {
        fillColor: '#94A3B8',
        fillOpacity: 0.75,
        strokeColor: '#94A3B8',
        strokeWidth: 2,
        radius: 6,
        assigned: false,
        markerType: 'worker'
    },
    assignedUnavailableWorker: {
        fillColor: '#94A3B8',
        fillOpacity: 0.75,
        strokeColor: '#FFFFFF',
        strokeWidth: 3,
        radius: 6,
        assigned: true,
        markerType: 'worker'
    },
    selected: {
        radius: 8,
        strokeColor: '#FFFFFF',
        strokeWidth: 3,
        workerScale: 1.2,
        workerRingColor: '#FFFFFF',
        workerRingWidth: 3
    }
};

document.addEventListener('DOMContentLoaded', function () {
    initializeMap();
});

window.chrome?.webview?.addEventListener('message', function (event) {
    const data = parseHostMessage(event.data);
    if (data) {
        updateMapData(data);
    }
});

function initializeMap() {
    map = L.map('map', {
        preferCanvas: true,
        zoomAnimation: true,
        fadeAnimation: true,
        markerZoomAnimation: true,
        center: [43.4516, -80.4925],
        zoom: 10
    });

    map.on('moveend', onMapMoved);
    map.on('zoomend', onMapMoved);
    map.on('click', onMapBackgroundClick);
    postMessage({ type: 'mapReady' });
}

function parseHostMessage(rawData) {
    try {
        if (typeof rawData === 'string') {
            return JSON.parse(rawData);
        }

        if (rawData && typeof rawData === 'object') {
            return rawData;
        }
    } catch (error) {
        console.error('Failed to parse host message:', error);
    }

    return null;
}

function updateMapData(data) {
    if (!map || !data) {
        return;
    }

    clearMarkers();
    updateBaseLayers(data);

    if (Array.isArray(data.markers)) {
        for (const markerData of data.markers) {
            const entry = createMarkerEntry(markerData);
            if (!entry) {
                continue;
            }

            markerEntries.push(entry);
            entry.marker.addTo(map);
        }
    }

    if (isFiniteNumber(data.centerLat) && isFiniteNumber(data.centerLng) && Number.isInteger(data.zoom)) {
        map.setView([data.centerLat, data.centerLng], data.zoom, { animate: false });
    }
}

function updateBaseLayers(data) {
    if (!map) {
        return;
    }

    if (satelliteLayer) {
        map.removeLayer(satelliteLayer);
        satelliteLayer = null;
    }

    satelliteLayer = createTileLayer(data.satelliteTiles);

    if (satelliteLayer) {
        satelliteLayer.addTo(map);
    }
}

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

function createMarkerEntry(markerData) {
    const latitude = Number(markerData?.latitude);
    const longitude = Number(markerData?.longitude);

    if (!isFiniteNumber(latitude) || !isFiniteNumber(longitude)) {
        return null;
    }

    const markerId = String(markerData?.id || '').trim();
    if (!markerId) {
        return null;
    }

    const kind = normalizeMarkerKind(markerData?.kind);
    const config = getMarkerStyle(kind);

    const entry = {
        id: markerId,
        kind: kind,
        markerData: markerData,
        config: config,
        originalLatLng: L.latLng(latitude, longitude),
        marker: null,
        selected: false
    };

    if (config.markerType === 'worker') {
        entry.marker = L.marker(entry.originalLatLng, {
            icon: createWorkerIcon(config, false),
            draggable: true,
            keyboard: false,
            autoPan: true,
            riseOnHover: true
        });
    } else {
        entry.marker = L.circleMarker(entry.originalLatLng, {
            radius: config.radius,
            fillColor: config.fillColor,
            fillOpacity: config.fillOpacity,
            color: config.strokeColor,
            weight: config.strokeWidth,
            className: 'map-marker'
        });
    }

    attachCommonHandlers(entry);

    if (config.markerType === 'worker') {
        attachWorkerDragHandlers(entry);
    }

    return entry;
}

function attachCommonHandlers(entry) {
    entry.marker.on('click', function (event) {
        L.DomEvent.stopPropagation(event);

        openMarkerPopup(entry);
        selectMarker(entry.id);

        postMessage({
            type: 'markerSelected',
            id: entry.id,
            kind: entry.kind,
            title: entry.markerData.title || '',
            detail: getDetailText(entry.markerData)
        });

        if (entry.kind === 'elevator') {
            postMessage({
                type: 'elevatorFocused',
                elevatorId: entry.id,
                elevatorTitle: entry.markerData.title || ''
            });
        }
    });
}

function attachWorkerDragHandlers(entry) {
    entry.marker.on('dragstart', function () {
        selectMarker(entry.id);
    });

    entry.marker.on('dragend', function () {
        const workerPosition = entry.marker.getLatLng();
        const targetElevator = findElevatorDropTarget(workerPosition);

        entry.marker.setLatLng(entry.originalLatLng);

        if (!targetElevator) {
            return;
        }

        postMessage({
            type: 'workerDroppedOnElevator',
            workerId: entry.id,
            elevatorId: targetElevator.id,
            workerTitle: entry.markerData.title || '',
            elevatorTitle: targetElevator.markerData.title || ''
        });
    });
}

function findElevatorDropTarget(workerLatLng) {
    if (!map) {
        return null;
    }

    const workerPoint = map.latLngToContainerPoint(workerLatLng);
    let bestTarget = null;
    let bestDistance = Number.POSITIVE_INFINITY;

    for (const entry of markerEntries) {
        if (entry.kind !== 'elevator') {
            continue;
        }

        const elevatorPoint = map.latLngToContainerPoint(entry.marker.getLatLng());
        const distance = workerPoint.distanceTo(elevatorPoint);

        if (distance <= DROP_TARGET_RADIUS_PX && distance < bestDistance) {
            bestTarget = entry;
            bestDistance = distance;
        }
    }

    return bestTarget;
}

function openMarkerPopup(entry) {
    if (currentPopupEntry && currentPopupEntry.marker) {
        currentPopupEntry.marker.closePopup();
    }

    const popupContent = createPopupContent(entry.markerData);
    entry.marker.bindPopup(popupContent).openPopup();
    currentPopupEntry = entry;
}

function selectMarker(markerId) {
    for (const entry of markerEntries) {
        applyMarkerSelection(entry, entry.id === markerId);
    }
}

function applyMarkerSelection(entry, isSelected) {
    entry.selected = isSelected;

    if (entry.config.markerType === 'worker') {
        entry.marker.setIcon(createWorkerIcon(entry.config, isSelected));
        return;
    }

    if (isSelected) {
        entry.marker.setStyle({
            radius: MARKER_STYLES.selected.radius,
            color: MARKER_STYLES.selected.strokeColor,
            weight: MARKER_STYLES.selected.strokeWidth
        });
    } else {
        entry.marker.setStyle({
            radius: entry.config.radius,
            fillColor: entry.config.fillColor,
            fillOpacity: entry.config.fillOpacity,
            color: entry.config.strokeColor,
            weight: entry.config.strokeWidth
        });
    }
}

function createWorkerIcon(config, isSelected) {
    const scale = isSelected ? MARKER_STYLES.selected.workerScale : 1;
    const size = Math.max(12, Math.round(config.radius * 2 * scale));

    const borderColor = isSelected
        ? MARKER_STYLES.selected.workerRingColor
        : config.strokeColor;

    const borderWidth = isSelected
        ? Math.max(config.strokeWidth, MARKER_STYLES.selected.workerRingWidth)
        : config.strokeWidth;

    const assignedInset = config.assigned
        ? 'inset 0 0 0 2px rgba(0, 0, 0, 0.08)'
        : 'none';

    const html = `<div style="width:${size}px;height:${size}px;border-radius:50%;background:${toRgba(config.fillColor, config.fillOpacity)};border:${borderWidth}px solid ${borderColor};box-shadow:${assignedInset};"></div>`;

    return L.divIcon({
        className: 'map-worker-marker',
        html: html,
        iconSize: [size, size],
        iconAnchor: [size / 2, size / 2],
        popupAnchor: [0, -(size / 2)]
    });
}

function getMarkerStyle(kind) {
    switch (kind) {
        case 'availableWorker':
            return MARKER_STYLES.availableWorker;
        case 'assignedAvailableWorker':
            return MARKER_STYLES.assignedAvailableWorker;
        case 'unavailableWorker':
            return MARKER_STYLES.unavailableWorker;
        case 'assignedUnavailableWorker':
            return MARKER_STYLES.assignedUnavailableWorker;
        case 'elevator':
        default:
            return MARKER_STYLES.elevator;
    }
}

function normalizeMarkerKind(kind) {
    if (typeof kind === 'number') {
        switch (kind) {
            case 1:
                return 'availableWorker';
            case 2:
                return 'assignedAvailableWorker';
            case 3:
                return 'unavailableWorker';
            case 4:
                return 'assignedUnavailableWorker';
            case 0:
            default:
                return 'elevator';
        }
    }

    const kindValue = String(kind || '').trim();
    switch (kindValue) {
        case 'AvailableWorker':
            return 'availableWorker';
        case 'AssignedAvailableWorker':
            return 'assignedAvailableWorker';
        case 'UnavailableWorker':
            return 'unavailableWorker';
        case 'AssignedUnavailableWorker':
            return 'assignedUnavailableWorker';
        case 'Elevator':
            return 'elevator';
    }

    const kindText = kindValue.toLowerCase();

    switch (kindText) {
        case 'availableworker':
        case 'available_worker':
            return 'availableWorker';
        case 'assignedavailableworker':
        case 'assigned_available_worker':
            return 'assignedAvailableWorker';
        case 'unavailableworker':
        case 'unavailable_worker':
            return 'unavailableWorker';
        case 'assignedunavailableworker':
        case 'assigned_unavailable_worker':
            return 'assignedUnavailableWorker';
        case 'elevator':
        default:
            return 'elevator';
    }
}

function createPopupContent(markerData) {
    let html = `<h3>${escapeHtml(markerData?.title || '')}</h3>`;

    if (Array.isArray(markerData?.detailLines)) {
        for (const line of markerData.detailLines) {
            html += `<p>${escapeHtml(line)}</p>`;
        }
    }

    return html;
}

function getDetailText(markerData) {
    if (!Array.isArray(markerData?.detailLines)) {
        return '';
    }

    return markerData.detailLines.join('\n');
}

function onMapMoved() {
    if (!map) {
        return;
    }

    const center = map.getCenter();
    postMessage({
        type: 'mapMoved',
        lat: center.lat,
        lng: center.lng,
        zoom: map.getZoom()
    });
}

function onMapBackgroundClick() {
    if (currentPopupEntry && currentPopupEntry.marker) {
        currentPopupEntry.marker.closePopup();
    }
    currentPopupEntry = null;

    for (const entry of markerEntries) {
        applyMarkerSelection(entry, false);
    }

    postMessage({ type: 'mapCleared' });
}

function clearMarkers() {
    if (!map) {
        return;
    }

    for (const entry of markerEntries) {
        map.removeLayer(entry.marker);
    }

    markerEntries = [];
    currentPopupEntry = null;
}

function postMessage(message) {
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
        window.chrome.webview.postMessage(message);
    }
}

function escapeHtml(text) {
    if (!text) {
        return '';
    }

    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function isFiniteNumber(value) {
    return typeof value === 'number' && Number.isFinite(value);
}

function toRgba(hexColor, opacity) {
    const normalized = String(hexColor || '').replace('#', '');
    if (normalized.length !== 6) {
        return hexColor;
    }

    const red = parseInt(normalized.substring(0, 2), 16);
    const green = parseInt(normalized.substring(2, 4), 16);
    const blue = parseInt(normalized.substring(4, 6), 16);

    return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
}
