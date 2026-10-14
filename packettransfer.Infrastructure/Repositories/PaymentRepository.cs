using Microsoft.EntityFrameworkCore;
using packettransfer.Domain.Entities;
using packettransfer.Domain.Interfaces;
using packettransfer.Infrastructure.Data;

namespace packettransfer.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly packettransferDbContext _context;

    public PaymentRepository(packettransferDbContext context) => _context = context;

    public async Task AddAsync(Payment payment)
    {
        await _context.Payments.AddAsync(payment);
    }

    public Task<Payment?> GetByAuthorityAsync(string authority) =>
        _context.Payments.FirstOrDefaultAsync(p => p.Authority == authority);

    public Task<Payment?> GetByIdForUserAsync(int paymentId, string userId) =>
        _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId);

    public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetPagedAsync(
        PaymentStatus? status, int page, int pageSize)
    {
        var query = _context.Payments.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
