using Prometheus;

namespace Lachain.Core.RPC.HTTP
{
    public class MetricsService : IMetricsService
    {
        private readonly MetricServer _server;
        
        public MetricsService()
        {
            _server = new MetricServer(hostname: "*", port: 7071);
        }

        public void Start()
        {
            _server.Start();
        }

        public void Dispose()
        {
            _server.Stop();
        }
    }
}