using Equipment_Service.Models;

namespace Equipment_Service.Repositories.Interfaces;

public interface IEquipmentRequestRepository : IGenericRepository<EquipmentRequest>
{
    Task<EquipmentRequest?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<EquipmentRequest>> GetByUserAsync(int employeeId);
    Task<IEnumerable<EquipmentType>> GetEquipmentTypesAsync();
    Task CreateAsync(EquipmentRequest request);
}
