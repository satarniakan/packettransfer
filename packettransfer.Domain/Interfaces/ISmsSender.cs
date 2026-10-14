namespace packettransfer.Domain.Interfaces;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message);
}