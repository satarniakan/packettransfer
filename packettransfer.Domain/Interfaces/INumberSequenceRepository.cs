using packettransfer.Domain.Entities;

namespace packettransfer.Domain.Interfaces;

public interface INumberSequenceRepository
{
    Task<NumberSequence?> GetAsync(string name);

    Task AddAsync(NumberSequence sequence);
}
