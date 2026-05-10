using Restoran.Features.Supervisor.Dtos;
using Restoran.Models;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Supervisor.Services
{
    public interface IAssetLogService
    {
        Task<IReadOnlyList<AssetLookupItem>> GetAvailableAssetsAsync(CancellationToken cancellationToken = default);
        Task<OperationResult> CreateAsync(AssetLogViewModel model, int reporterUserId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AssetLogListViewModel>> GetLogsAsync(DateTime? fromDate, DateTime? toDate, LogStatus? status, CancellationToken cancellationToken = default);
        Task<OperationResult> ApproveAsync(int id, int approverUserId, CancellationToken cancellationToken = default);
        Task<AssetDamageReportViewModel> GetReportAsync(CancellationToken cancellationToken = default);
    }
}
