using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public class WorkerService : IWorkerService
{
    private readonly IWorkerRepository _workerRepository;
    private readonly GpsCoordinateValidator _gpsCoordinateValidator;

    public WorkerService(
        IWorkerRepository workerRepository,
        GpsCoordinateValidator gpsCoordinateValidator)
    {
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _gpsCoordinateValidator = gpsCoordinateValidator ?? throw new ArgumentNullException(nameof(gpsCoordinateValidator));
    }

    public async Task<IReadOnlyList<Worker>> GetActiveAsync()
    {
        var workers = await _workerRepository.GetActiveAsync();
        return workers.ToList();
    }

    public async Task<Worker> CreateAsync(Worker worker, double latitude, double longitude)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var timestamp = DateTime.UtcNow;
        worker.Id = worker.Id == Guid.Empty ? Guid.NewGuid() : worker.Id;
        worker.CreatedAt = timestamp;
        worker.UpdatedAt = timestamp;
        worker.DeletedAt = null;
        worker.Location = _gpsCoordinateValidator.CreatePoint(latitude, longitude);

        await _workerRepository.AddAsync(worker);
        return worker;
    }

    public async Task<Worker> UpdateAsync(Worker worker, double latitude, double longitude)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var existing = await _workerRepository.GetByIdAsync(worker.Id)
            ?? throw new KeyNotFoundException($"Worker '{worker.Id}' was not found.");

        existing.FullName = worker.FullName;
        existing.Email = worker.Email;
        existing.PhoneNumber = worker.PhoneNumber;
        existing.Skills = worker.Skills.ToList();
        existing.AvailabilityStatus = worker.AvailabilityStatus;
        existing.Location = _gpsCoordinateValidator.CreatePoint(latitude, longitude);
        existing.UpdatedAt = DateTime.UtcNow;

        await _workerRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task<Worker> DeactivateAsync(Guid id)
    {
        var existing = await _workerRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Worker '{id}' was not found.");

        if (existing.DeletedAt.HasValue)
        {
            return existing;
        }

        existing.AvailabilityStatus = WorkerAvailabilityStatus.Unavailable;
        existing.DeletedAt = DateTime.UtcNow;
        existing.UpdatedAt = existing.DeletedAt.Value;

        await _workerRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task<Worker> UpdateLocationAsync(Guid id, double latitude, double longitude)
    {
        var existing = await _workerRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Worker '{id}' was not found.");

        if (existing.DeletedAt.HasValue)
        {
            throw new InvalidOperationException("Cannot update location for a deactivated worker.");
        }

        existing.Location = _gpsCoordinateValidator.CreatePoint(latitude, longitude);
        existing.UpdatedAt = DateTime.UtcNow;

        await _workerRepository.UpdateAsync(existing);
        return existing;
    }
}
