using Microsoft.EntityFrameworkCore;
using packettransfer.Domain.Entities;
using packettransfer.Domain.Interfaces;
using packettransfer.Infrastructure.Data;

namespace packettransfer.Infrastructure.Repositories;

public class NumberSequenceRepository : INumberSequenceRepository
{
    private readonly packettransferDbContext _context;

    public NumberSequenceRepository(packettransferDbContext context) => _context = context;

    public Task<NumberSequence?> GetAsync(string name) =>
        _context.NumberSequences.FirstOrDefaultAsync(s => s.Name == name);

    public async Task AddAsync(NumberSequence sequence)
    {
        await _context.NumberSequences.AddAsync(sequence);
    }
}
