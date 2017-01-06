using System;
using System.Globalization;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using R4nd0mApps.TddStud10.Logger;

namespace R4nd0mApps.TddStud10.Hosts.VS
{
    public class RemoteHost<TServer, TServerIFace, TServerEvents>
        where TServer : class, TServerIFace, new()
        where TServerEvents : class, new()
    {
        private readonly ILogger _logger = LoggerFactory.logger;

        private readonly string _serverNs;

        private ServiceHost ServiceHost { get; set; }

        public TServerIFace Server { get; private set; }

        public TServerEvents Events { get; private set; }

        public RemoteHost(string serverNs)
        {
            _serverNs = serverNs;
        }

        public void StartServer()
        {
            Task.Run(() =>
            {
                try
                {
                    var address = CreateServerEndpointAddress();
                    _logger.LogInfo("Starting remote server {0} ...", address);
                    ServiceHost = new ServiceHost(new TServer());
                    ServiceHost.AddServiceEndpoint(
                        typeof(TServerIFace),
                        new NetNamedPipeBinding(NetNamedPipeSecurityMode.None),
                        address);

                    var debug = ServiceHost.Description.Behaviors.Find<ServiceDebugBehavior>();
                    if (debug == null)
                    {
                        ServiceHost.Description.Behaviors.Add(new ServiceDebugBehavior { IncludeExceptionDetailInFaults = true });
                    }
                    else
                    {
                        debug.IncludeExceptionDetailInFaults = true;
                    }

                    ServiceHost.Open();
                    ConnectClient();
                }
                catch (Exception e)
                {
                    _logger.LogError("Failed to start DS Sever: {0} ...", e);
                }
            });

            while (Server == null)
            {
                System.Threading.Thread.Sleep(500);
            }
        }

        public void StopSever()
        {
            DisconnectClient();

            try
            {
                if (ServiceHost != null)
                {
                    ServiceHost.Close();
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to close connection to DS Sever: {0} ...", e);
            }
        }

        private void ConnectClient()
        {
            try
            {
                Events = new TServerEvents();

                var address = CreateServerEndpointAddress();
                _logger.LogInfo("Initiating connection to {0} ...", address);
                Server = DuplexChannelFactory<TServerIFace>.CreateChannel(
                    new InstanceContext(Events),
                    new NetNamedPipeBinding(NetNamedPipeSecurityMode.None),
                    new EndpointAddress(address));
                ((dynamic)Server).Connect();
                _logger.LogInfo("Connected to server.", address);
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to connect to DS Sever: {0} ...", e);
            }
        }

        private void DisconnectClient()
        {
            try
            {
                if (Server != null)
                {
                    ((dynamic)Server).Disconnect();
                    ((IClientChannel)Server).Close();
                    ((IDisposable)Server).Dispose();
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to close connection to DS Sever: {0} ...", e);
            }
        }

        private string CreateServerEndpointAddress()
        {
            return string.Format(
                "net.pipe://localhost/r4nd0mapps/tddstud10/{0}/{1}",
                _serverNs,
                System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
        }
    }
}