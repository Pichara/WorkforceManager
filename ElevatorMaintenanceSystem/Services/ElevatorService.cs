using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public class ElevatorService : IElevatorService
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly GpsCoordinateValidator _gpsCoordinateValidator;

    public ElevatorService(
        IElevatorRepository elevatorRepository,
        GpsCoordinateValidator gpsCoordinateValidator)
    {
        _elevatorRepository = elevatorRepository ?? throw new ArgumentNullException(nameof(elevatorRepository));
        _gpsCoordinateValidator = gpsCoordinateValidator ?? throw new ArgumentNullException(nameof(gpsCoordinateValidator));
    }

    public async Task<IReadOnlyList<Elevator>> GetActiveAsync()
    {
        var elevators = await _elevatorRepository.GetActiveAsync();
        return elevators.ToList();
    }

    public async Task<Elevator> CreateAsync(Elevator elevator, double latitude, double longitude)
    {
        ArgumentNullException.ThrowIfNull(elevator);

        var timestamp = DateTime.UtcNow;
        elevator.Id = elevator.Id == Guid.Empty ? Guid.NewGuid() : elevator.Id;
        elevator.CreatedAt = timestamp;
        elevator.UpdatedAt = timestamp;
        elevator.DeletedAt = null;
        elevator.Location = _gpsCoordinateValidator.CreatePoint(latitude, longitude);

        await _elevatorRepository.AddAsync(elevator);
        return elevator;
    }

    public async Task<Elevator> UpdateAsync(Elevator elevator, double latitude, double longitude)
    {
        ArgumentNullException.ThrowIfNull(elevator);

        var existing = await _elevatorRepository.GetByIdAsync(elevator.Id)
            ?? throw new KeyNotFoundException($"Elevator '{elevator.Id}' was not found.");

        existing.Name = elevator.Name;
        existing.Address = elevator.Address;
        existing.BuildingName = elevator.BuildingName;
        existing.FloorLabel = elevator.FloorLabel;
        existing.Manufacturer = elevator.Manufacturer;
        existing.InstallationDate = elevator.InstallationDate;
        existing.IsActive = elevator.IsActive;
        existing.Location = _gpsCoordinateValidator.CreatePoint(latitude, longitude);
        existing.UpdatedAt = DateTime.UtcNow;

        await _elevatorRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteInactiveAsync(Guid id)
    {
        var existing = await _elevatorRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Elevator '{id}' was not found.");

        if (existing.IsActive)
        {
            throw new InvalidOperationException("Active elevators must be deactivated before deletion.");
        }

        await _elevatorRepository.DeleteAsync(id);
    }
}
