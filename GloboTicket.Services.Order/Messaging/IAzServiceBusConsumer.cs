using System.Threading.Tasks;

namespace GloboTicket.Services.Ordering.Messaging
{
    public interface IAzServiceBusConsumer
    {
        // void Start();
        // void Stop();
        Task StartAsync();
        Task StopAsync();
    }
}