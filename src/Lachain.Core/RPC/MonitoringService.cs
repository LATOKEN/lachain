using App.Metrics;

namespace Lachain.Core.RPC
{
    public class MonitoringService
    {
        private readonly IMetricsRoot _metrics;
        
        public MonitoringService()
        {
            _metrics = new MetricsBuilder()
                .OutputMetrics.AsPrometheusProtobuf()
                .Build();
        }
    }
}