using Microsoft.EntityFrameworkCore;
using packettransfer.Domain.Entities;
using packettransfer.Domain.Interfaces;
using packettransfer.Infrastructure.Data;

namespace packettransfer.Infrastructure.Repositories;

public class OtpThrottleRepository : IOtpThrottleRepository
{
    private readonly packettransferDbContext _context;

    public OtpThrottleRepository(packettransferDbContext context) => _context = context;

    public Task<OtpThrottle?> GetByPhoneAsync(string phoneNumber) =>
        _context.OtpThrottles.FirstOrDefaultAsync(t => t.PhoneNumber == phoneNumber);

    public async Task AddAsync(OtpThrottle throttle)
    {
        await _context.OtpThrottles.AddAsync(throttle);
    }

    public Task<int> DeleteStaleAsync(DateTime cutoffUtc) =>
        _context.OtpThrottles
            .Where(t => (t.FailedWindowStartUtc == null || t.FailedWindowStartUtc < cutoffUtc)
                     && (t.GenerationWindowStartUtc == null || t.GenerationWindowStartUtc < cutoffUtc))
            .ExecuteDeleteAsync();
}
