using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using MongoDB.Driver;
using Xunit;

namespace ElevatorMaintenanceSystem.Tests.Services;

public class MapDataServiceTests
{
    [Fact]
    public async Task BuildSnapshotAsync_MAP_01_ProjectsElevatorsIntoMarkers()
    {
        // Arrange
        var fixture = CreateFixture();
        var elevator = CreateElevator(
            id: Guid.NewGuid(),
            name: "Alpha Tower",
            address: "100 Main St",
            buildingName: "Alpha",
            floorLabel: "L1",
            latitude: 43.4516,
            longitude: -80.4925);

        fixture.Elevators.Add(elevator);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Single(snapshot.Markers);
        var marker = snapshot.Markers[0];
        Assert.Equal(MapMarkerKind.Elevator, marker.Kind);
        Assert.Equal("Alpha Tower", marker.Title);
        Assert.Contains("100 Main St", marker.DetailLines);
        Assert.Contains("Alpha / L1", marker.DetailLines);
        Assert.Equal(43.4516, marker.Latitude);
        Assert.Equal(-80.4925, marker.Longitude);
    }

    [Fact]
    public async Task BuildSnapshotAsync_MAP_01_InactiveElevatorsBecomeMarkers()
    {
        // Arrange
        var fixture = CreateFixture();
        var inactiveElevator = CreateElevator(
            id: Guid.NewGuid(),
            name: "Inactive Tower",
            address: "200 Main St",
            buildingName: "Inactive",
            floorLabel: "L2",
            latitude: 43.4520,
            longitude: -80.4930);

        fixture.Elevators.Add(inactiveElevator);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        // Inactive elevators (not deleted) still become markers per MAP-01
        Assert.Single(snapshot.Markers);
        Assert.Equal("Inactive Tower", snapshot.Markers[0].Title);
    }

    [Fact]
    public async Task BuildSnapshotAsync_MAP_02_AvailableWorkersBecomeAvailableWorkerMarkers()
    {
        // Arrange
        var fixture = CreateFixture();
        var availableWorker = CreateWorker(
            id: Guid.NewGuid(),
            fullName: "Ava Stone",
            email: "ava@example.com",
            phoneNumber: "555-0100",
            skills: ["Repair", "Inspection", "Maintenance"],
            availabilityStatus: WorkerAvailabilityStatus.Available,
            latitude: 43.4517,
            longitude: -80.4926);

        fixture.Workers.Add(availableWorker);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Single(snapshot.Markers);
        var marker = snapshot.Markers[0];
        Assert.Equal(MapMarkerKind.AvailableWorker, marker.Kind);
        Assert.Equal("Ava Stone", marker.Title);
        Assert.Contains("Status: Available", marker.DetailLines);
        Assert.Contains("Skills: Repair, Inspection, Maintenance", marker.DetailLines);
    }

    [Fact]
    public async Task BuildSnapshotAsync_MAP_02_UnavailableWorkersBecomeUnavailableWorkerMarkers()
    {
        // Arrange
        var fixture = CreateFixture();
        var unavailableWorker = CreateWorker(
            id: Guid.NewGuid(),
            fullName: "Noah Reed",
            email: "noah@example.com",
            phoneNumber: "555-0101",
            skills: ["Repair"],
            availabilityStatus: WorkerAvailabilityStatus.Unavailable,
            latitude: 43.4518,
            longitude: -80.4927);

        fixture.Workers.Add(unavailableWorker);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Single(snapshot.Markers);
        var marker = snapshot.Markers[0];
        Assert.Equal(MapMarkerKind.UnavailableWorker, marker.Kind);
        Assert.Equal("Noah Reed", marker.Title);
        Assert.Contains("Status: Unavailable", marker.DetailLines);
    }

    [Fact]
    public async Task BuildSnapshotAsync_SoftDeletedWorkersAreSkipped()
    {
        // Arrange
        var fixture = CreateFixture();
        var deletedWorker = CreateWorker(
            id: Guid.NewGuid(),
            fullName: "Deleted Worker",
            email: "deleted@example.com",
            phoneNumber: "555-0199",
            skills: ["Repair"],
            availabilityStatus: WorkerAvailabilityStatus.Available,
            latitude: 43.4519,
            longitude: -80.4928,
            deletedAt: DateTime.UtcNow);

        fixture.Workers.Add(deletedWorker);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Empty(snapshot.Markers);
    }

    [Fact]
    public async Task BuildSnapshotAsync_ItemsWithMissingCoordinatesAreSkipped()
    {
        // Arrange
        var fixture = CreateFixture();
        var elevatorWithoutLocation = CreateElevator(
            id: Guid.NewGuid(),
            name: "No Location Tower",
            address: "300 Main St",
            buildingName: "NoLoc",
            floorLabel: "L1",
            latitude: null,
            longitude: null);

        var workerWithoutLocation = CreateWorker(
            id: Guid.NewGuid(),
            fullName: "No Location Worker",
            email: "noloc@example.com",
            phoneNumber: "555-0200",
            skills: ["Repair"],
            availabilityStatus: WorkerAvailabilityStatus.Available,
            latitude: null,
            longitude: null);

        fixture.Elevators.Add(elevatorWithoutLocation);
        fixture.Workers.Add(workerWithoutLocation);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Empty(snapshot.Markers);
    }

    [Fact]
    public async Task BuildSnapshotAsync_WaterlooKitchenerDefaultsFlowFromMapSettings()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Settings.DefaultCenterLatitude = 43.4516;
        fixture.Settings.DefaultCenterLongitude = -80.4925;
        fixture.Settings.DefaultZoom = 10;

        var elevator = CreateElevator(
            id: Guid.NewGuid(),
            name: "Test Tower",
            address: "100 Main St",
            buildingName: "Test",
            floorLabel: "L1",
            latitude: 43.4516,
            longitude: -80.4925);

        fixture.Elevators.Add(elevator);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Equal(43.4516, snapshot.CenterLatitude);
        Assert.Equal(-80.4925, snapshot.CenterLongitude);
        Assert.Equal(10, snapshot.Zoom);
    }

    [Fact]
    public async Task BuildSnapshotAsync_MarkerRowsAreSortedPredictably()
    {
        // Arrange
        var fixture = CreateFixture();
        fixture.Elevators.Add(CreateElevator(
            id: Guid.NewGuid(),
            name: "Zulu Tower",
            address: "300 Main St",
            buildingName: "Zulu",
            floorLabel: "L3",
            latitude: 43.4530,
            longitude: -80.4930));

        fixture.Elevators.Add(CreateElevator(
            id: Guid.NewGuid(),
            name: "Alpha Tower",
            address: "100 Main St",
            buildingName: "Alpha",
            floorLabel: "L1",
            latitude: 43.4516,
            longitude: -80.4925));

        fixture.Workers.Add(CreateWorker(
            id: Guid.NewGuid(),
            fullName: "Zoe Worker",
            email: "zoe@example.com",
            phoneNumber: "555-0300",
            skills: ["Repair"],
            availabilityStatus: WorkerAvailabilityStatus.Available,
            latitude: 43.4531,
            longitude: -80.4931));

        fixture.Workers.Add(CreateWorker(
            id: Guid.NewGuid(),
            fullName: "Adam Worker",
            email: "adam@example.com",
            phoneNumber: "555-0100",
            skills: ["Inspection"],
            availabilityStatus: WorkerAvailabilityStatus.Available,
            latitude: 43.4517,
            longitude: -80.4926));

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Equal(4, snapshot.Markers.Count);
        Assert.Equal("Adam Worker", snapshot.Markers[0].Title);
        Assert.Equal("Alpha Tower", snapshot.Markers[1].Title);
        Assert.Equal("Zoe Worker", snapshot.Markers[2].Title);
        Assert.Equal("Zulu Tower", snapshot.Markers[3].Title);
    }

    [Fact]
    public async Task BuildSnapshotAsync_TopThreeSkillsAreProjected()
    {
        // Arrange
        var fixture = CreateFixture();
        var workerWithFiveSkills = CreateWorker(
            id: Guid.NewGuid(),
            fullName: "Skilled Worker",
            email: "skilled@example.com",
            phoneNumber: "555-0400",
            skills: ["Repair", "Inspection", "Maintenance", "Installation", "Testing"],
            availabilityStatus: WorkerAvailabilityStatus.Available,
            latitude: 43.4516,
            longitude: -80.4925);

        fixture.Workers.Add(workerWithFiveSkills);

        // Act
        var snapshot = await fixture.Service.BuildSnapshotAsync();

        // Assert
        Assert.Single(snapshot.Markers);
        Assert.Contains("Skills: Repair, Inspection, Maintenance", snapshot.Markers[0].DetailLines);
        Assert.DoesNotContain("Installation", snapshot.Markers[0].DetailLines[1]);
    }

    private static MapDataServiceFixture CreateFixture()
    {
        var settings = new MapSettings
        {
            DefaultCenterLatitude = 43.4516,
            DefaultCenterLongitude = -80.4925,
            DefaultZoom = 10,
            StandardTiles = new TileProviderSettings
            {
                Provider = "OpenStreetMap",
                UrlTemplate = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                Attribution = "&copy; OpenStreetMap contributors",
                MaxZoom = 19
            },
            SatelliteTiles = new TileProviderSettings
            {
                Provider = "ArcGIS",
                UrlTemplate = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
                Attribution = "Source: Esri",
                MaxZoom = 19
            }
        };

        var elevatorRepo = new FakeElevatorRepository();
        var workerRepo = new FakeWorkerRepository();
        var service = new MapDataService(elevatorRepo, workerRepo, settings);

        return new MapDataServiceFixture(service, elevatorRepo, workerRepo, settings);
    }

    private static Elevator CreateElevator(
        Guid id,
        string name,
        string address,
        string buildingName,
        string floorLabel,
        double? latitude,
        double? longitude)
    {
        var elevator = new Elevator
        {
            Id = id,
            Name = name,
            Address = address,
            BuildingName = buildingName,
            FloorLabel = floorLabel,
            Manufacturer = "Otis",
            InstallationDate = DateTime.UtcNow,
            IsActive = true
        };

        if (latitude.HasValue && longitude.HasValue)
        {
            elevator.Location = CreateGeoJsonPoint(latitude.Value, longitude.Value);
        }

        return elevator;
    }

    private static Worker CreateWorker(
        Guid id,
        string fullName,
        string email,
        string phoneNumber,
        List<string> skills,
        WorkerAvailabilityStatus availabilityStatus,
        double? latitude,
        double? longitude,
        DateTime? deletedAt = null)
    {
        var worker = new Worker
        {
            Id = id,
            FullName = fullName,
            Email = email,
            PhoneNumber = phoneNumber,
            Skills = skills,
            AvailabilityStatus = availabilityStatus,
            DeletedAt = deletedAt
        };

        if (latitude.HasValue && longitude.HasValue)
        {
            worker.Location = CreateGeoJsonPoint(latitude.Value, longitude.Value);
        }

        return worker;
    }

    private static GeoJsonPoint<GeoJson2DGeographicCoordinates> CreateGeoJsonPoint(double latitude, double longitude)
    {
        return new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
            new GeoJson2DGeographicCoordinates(longitude, latitude));
    }

    private sealed record MapDataServiceFixture(
        MapDataService Service,
        FakeElevatorRepository Elevators,
        FakeWorkerRepository Workers,
        MapSettings Settings);

    private sealed class FakeElevatorRepository : IElevatorRepository
    {
        private readonly List<Elevator> _items = [];

        public IReadOnlyList<Elevator> Items => _items;
        public void Add(Elevator entity) => _items.Add(entity);

        public Task<Elevator?> GetByIdAsync(Guid id) => Task.FromResult(_items.FirstOrDefault(e => e.Id == id));
        public Task<IEnumerable<Elevator>> GetAllAsync() => Task.FromResult<IEnumerable<Elevator>>(_items);

        public Task<IEnumerable<Elevator>> FindAsync(FilterDefinition<Elevator> filter)
        {
            return Task.FromResult<IEnumerable<Elevator>>(_items.Where(e => e.DeletedAt == null));
        }

        public Task AddAsync(Elevator entity)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Elevator entity)
        {
            var index = _items.FindIndex(e => e.Id == entity.Id);
            if (index >= 0) _items[index] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id)
        {
            _items.RemoveAll(e => e.Id == id);
            return Task.CompletedTask;
        }

        public Task<long> CountAsync(FilterDefinition<Elevator>? filter = null) => Task.FromResult<long>(_items.Count);

        public Task<IEnumerable<Elevator>> GetActiveAsync() => Task.FromResult<IEnumerable<Elevator>>(_items.Where(e => e.DeletedAt == null));
    }

    private sealed class FakeWorkerRepository : IWorkerRepository
    {
        private readonly List<Worker> _items = [];

        public IReadOnlyList<Worker> Items => _items;
        public void Add(Worker entity) => _items.Add(entity);

        public Task<Worker?> GetByIdAsync(Guid id) => Task.FromResult(_items.FirstOrDefault(w => w.Id == id));
        public Task<IEnumerable<Worker>> GetAllAsync() => Task.FromResult<IEnumerable<Worker>>(_items);

        public Task<IEnumerable<Worker>> FindAsync(FilterDefinition<Worker> filter)
        {
            return Task.FromResult<IEnumerable<Worker>>(_items.Where(w => w.DeletedAt == null));
        }

        public Task AddAsync(Worker entity)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Worker entity)
        {
            var index = _items.FindIndex(w => w.Id == entity.Id);
            if (index >= 0) _items[index] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id)
        {
            _items.RemoveAll(w => w.Id == id);
            return Task.CompletedTask;
        }

        public Task<long> CountAsync(FilterDefinition<Worker>? filter = null) => Task.FromResult<long>(_items.Count);

        public Task<IEnumerable<Worker>> GetActiveAsync() => Task.FromResult<IEnumerable<Worker>>(_items.Where(w => w.DeletedAt == null));
    }
}
