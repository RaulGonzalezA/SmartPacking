using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Client;

/// <summary>API operations required by native clients in the first mobile iteration.</summary>
public interface ISmartPackingClient
{
    Task<UserProfile> GetCurrentUserAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken);
    Task<TripDashboard?> GetTripDashboardAsync(Guid tripId, Guid? selectedProfileId, CancellationToken cancellationToken);
    Task SetChecklistPackedAsync(Guid itemId, bool isPacked, CancellationToken cancellationToken);
}
