using SmartPacking.Domain;

namespace SmartPacking.Application;

public static class PackingChecklistDefaults
{
    public static IReadOnlyList<ChecklistItem> Create(Guid tripId, Guid? profileId = null) =>
    [
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "DNI o pasaporte", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Tarjetas y reservas", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Seguro de viaje", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Cepillo y pasta de dientes", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Desodorante", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Protector solar", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Móvil y cargador", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Adaptador de enchufe", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Auriculares", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Medicación personal", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Tiritas y básicos", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Gafas de sol", false, profileId),
        new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Botella reutilizable", false, profileId)
    ];
}
