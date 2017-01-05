using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using R4nd0mApps.TddStud10.Common.Domain;
using R4nd0mApps.TddStud10.Engine.Core;
using R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage;
using R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core;
using R4nd0mApps.TddStud10.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using R4nd0mApps.TddStud10.TestRuntime;
using Process = EnvDTE.Process;
using Task = System.Threading.Tasks.Task;
using Thread = System.Threading.Thread;
using System.ServiceModel.Description;

namespace R4nd0mApps.TddStud10.Hosts.VS
{
    [ProvideBindingPath]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", Constants.ProductVersion, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
    [Guid(PkgGuids.GuidTddStud10Pkg)]
    public sealed class TddStud10Package : Package, IVsSolutionEvents, IEngineCallback
    {
        private static ILogger Logger = R4nd0mApps.TddStud10.Logger.LoggerFactory.logger;
        private static ITelemetryClient TelemetryClient = R4nd0mApps.TddStud10.Logger.TelemetryClientFactory.telemetryClient;

        private SynchronizationContext syncContext = SynchronizationContext.Current;

        private bool _disposed;

        private uint solutionEventsCookie;

        private IVsSolution2 _solution;
        private DTE _dte;
        private Events2 _events;
        private BuildEvents _buildEvents;

        public VsStatusBarIconHost IconHost { get; private set; }

        public static TddStud10Package Instance { get; private set; }

        public HostVersion HostVersion
        {
            get
            {
                return HostVersionExtensions.fromDteVersion(_dte.Version);
            }
        }

        public void InvokeOnUIThread(Action action)
        {
            syncContext.Send(new SendOrPostCallback(_ => action()), null);
        }

        public string GetSolutionPath()
        {
            return _dte.Solution.FullName;
        }

        #region Package Members

        protected override void Initialize()
        {
            base.Initialize();

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainAssemblyResolve;

            _solution = Services.GetService<SVsSolution, IVsSolution2>();
            if (_solution != null)
            {
                _solution.AdviseSolutionEvents(this, out solutionEventsCookie).ThrowOnFailure();
            }

            _dte = Services.GetService<DTE>();
            _events = (Events2)_dte.Events;
            _buildEvents = _events.BuildEvents;
            _buildEvents.OnBuildBegin += OnBuildBegin;
            _buildEvents.OnBuildDone += OnBuildDone;
            new PackageCommands(this).AddCommands();

            IconHost = VsStatusBarIconHost.CreateAndInjectIntoVsStatusBar();

            Instance = this;

            TelemetryClient.Initialize(Constants.ProductVersion, _dte.Version, _dte.Edition);

            Logger.LogInfo("Initialized Package successfully.");
        }

        private Assembly CurrentDomainAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var asmName = new AssemblyName(args.Name);
            if (asmName.Name != "R4nd0mApps.TddStud10.Hosts.CommonUI")
            {
                return null;
            }

            asmName.Name += ".DF";
            return Assembly.Load(asmName.ToString());
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }

                if (_solution != null && solutionEventsCookie != 0)
                {
                    _solution.UnadviseSolutionEvents(solutionEventsCookie);
                }
                _solution = null;

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion

        #region IVsSolutionEvents Members

        int IVsSolutionEvents.OnAfterCloseSolution(object pUnkReserved)
        {
            TelemetryClient.Flush();
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            var cfg = EngineConfigLoader.load(new EngineConfig(), FilePath.NewFilePath(GetSolutionPath()));
            if (!cfg.IsDisabled)
            {
                EngineLoader.Load(
                    this,
                    new EngineParams(
                        HostVersion,
                        cfg,
                        FilePath.NewFilePath(GetSolutionPath()),
                        DateTime.UtcNow
                    ));
                EngineLoader.EnableEngine();

                StartDataStoreSever();
            }
            else
            {
                TelemetryClient.TrackEvent("EngineDisabledOnLoad", new Dictionary<string, string>(), new Dictionary<string, double>());
            }

            Logger.LogInfo("Triggering SnapshotGC on solution load.");
            SnapshotGC.SweepAsync(FilePath.NewFilePath(Environment.ExpandEnvironmentVariables(cfg.SnapShotRoot)));

            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseSolution(object pUnkReserved)
        {
            StopDataStoreSever();

            EngineLoader.DisableEngine();
            EngineLoader.Unload();
            IconHost.RunState = RunState.Initial;

            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            System.Threading.Tasks.Task.Run(() => TelemetryClient.Flush());
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region Events2.BuildEvents

        private void OnBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            EngineLoader.DisableEngine();
        }

        private void OnBuildDone(vsBuildScope scope, vsBuildAction action)
        {
            EngineLoader.EnableEngine();
        }

        #endregion Events2.BuildEvents

        #region IEngineHost Members

        public void OnRunStateChanged(RunState rs)
        {
            IconHost.RunState = rs;
        }

        public void OnRunStarting(RunStartParams rd)
        {
        }

        public void OnRunStepStarting(RunStepStartingEventArg rsea)
        {
        }

        public void OnRunStepError(RunStepErrorEventArg ea)
        {
        }

        public void OnRunStepEnded(RunStepEndedEventArg ea)
        {
        }

        public void OnRunError(Exception e)
        {
        }

        public void OnRunEnded(RunStartParams rsp)
        {
        }

        #endregion

        public static ServiceHost DataStoreServer { get; private set; }

        public static IXDataStore DataStore { get; private set; }

        public static XDataStoreEvents DataStoreEvents { get; private set; }

        private static void StartDataStoreSever()
        {
#if !REMOTE_DATASTORE
            DataStoreEvents = new XDataStoreEvents();
            DataStore = new XDataStore(Engine.Core.DataStore.Instance, FSharpOption<IXDataStoreCallback>.Some(DataStoreEvents));
#else
            Task.Run(() =>
            {
                try
                {
                    var address = CreateDataStoreServerEndpointAddress();
                    Logger.LogInfo("Starting datastore server {0} ...", address);
                    DataStoreServer = new ServiceHost(new XDataStore());
                    DataStoreServer.AddServiceEndpoint(
                        typeof(IXDataStore),
                        new NetNamedPipeBinding(NetNamedPipeSecurityMode.None),
                        address);

                    ServiceDebugBehavior debug = DataStoreServer.Description.Behaviors.Find<ServiceDebugBehavior>();
                    if (debug == null)
                    {
                        DataStoreServer.Description.Behaviors.Add(new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });
                    }
                    else
                    {
                        debug.IncludeExceptionDetailInFaults = true;
                    }

                    DataStoreServer.Open();
                    ConnectToDataStore();
                }
                catch (Exception e)
                {
                    Logger.LogError("Failed to start DS Sever: {0} ...", e);
                }
            });
#endif
        }

        private static void StopDataStoreSever()
        {
#if !REMOTE_DATASTORE
#else
            DisconnectFromDataStore();

            try
            {
                if (DataStoreServer != null)
                {
                    DataStoreServer.Close();
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to close connection to DS Sever: {0} ...", e);
            }
#endif
        }

#if !REMOTE_DATASTORE
#else
        private static void ConnectToDataStore()
        {
            try
            {
                DataStoreEvents = new XDataStoreEvents();

                var address = CreateDataStoreServerEndpointAddress();
                Logger.LogInfo("Initiating connection to {0} ...", address);
                DataStore = DuplexChannelFactory<IXDataStore>.CreateChannel(
                    new InstanceContext(DataStoreEvents),
                    new NetNamedPipeBinding(NetNamedPipeSecurityMode.None),
                    new EndpointAddress(address));
                DataStore.Connect();
                Logger.LogInfo("Connected to server.", address);
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to connect to DS Sever: {0} ...", e);
            }
        }

        private static void DisconnectFromDataStore()
        {
            try
            {
                if (DataStore != null)
                {
                    DataStore.Disconnect();
                    ((IClientChannel)DataStore).Close();
                    ((IDisposable)DataStore).Dispose();
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to close connection to DS Sever: {0} ...", e);
            }
        }

        private static string CreateDataStoreServerEndpointAddress()
        {
            return string.Format(
                "net.pipe://localhost/r4nd0mapps/tddstud10/XDataStore/{0}",
                System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
        }
#endif
    }
}
