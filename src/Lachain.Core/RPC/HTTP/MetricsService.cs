using Lachain.Storage;
using Lachain.Core.Config;
using Prometheus;

namespace Lachain.Core.RPC.HTTP
{
    public class MetricsService : IMetricsService
    {
        private readonly MetricServer _server;
        private readonly IRocksDbContext _context;

        private static readonly Gauge DbKeys = Metrics.CreateGauge(
            "lachain_rocksdb_keys_count",
            "Estimated number of keys in database"
        );

        private static readonly Gauge DbSize = Metrics.CreateGauge(
            "lachain_rocksdb_size_bytes",
            "Size of database folder"
        );

        public MetricsService(IRocksDbContext context, IConfigManager config)
        {
            _context = context;
            var rpcConfig = config.GetConfig<RpcConfig>("rpc") ?? RpcConfig.Default;
            _server = new MetricServer(hostname: "*", port: rpcConfig.MetricsPort);
        }

        public void Start()
        {
            _server.Start();
            Metrics.DefaultRegistry.AddBeforeCollectCallback( () =>
            {
                DbKeys.Set(_context.EstimateNumberOfKeys());
                DbSize.Set(_context.EstimateDirSize());
            });
        }

        public void Dispose()
        {
            _server.Stop();
        }
    }
}