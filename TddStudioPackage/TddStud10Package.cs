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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace R4nd0mApps.TddStud10.Hosts.VS
{
    [ProvideBindingPath]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", Constants.ProductVersion, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
    [Guid(PkgGuids.GuidTddStud10Pkg)]
    public sealed class TddStud10Package : Package, IVsSolutionEvents
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

        public RemoteHost<XDataStore, IXDataStore, XDataStoreEvents> DataStore { get; private set; }

        public RemoteHost<Engine.Core.Engine, IEngine, EngineEvents> Engine { get; private set; }

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

            DataStore = new RemoteHost<XDataStore, IXDataStore, XDataStoreEvents>("datastore");
            Engine = new RemoteHost<Engine.Core.Engine, IEngine, EngineEvents>("engine");

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
                DataStore.StartServer();
                Engine.StartServer();

                EngineLoader.Load(
                    this,
                    new EngineParams(
                        HostVersion,
                        cfg,
                        FilePath.NewFilePath(GetSolutionPath()),
                        DateTime.UtcNow
                    ));
                EngineLoader.EnableEngine();
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
            IconHost.RunState = RunState.Initial;

            EngineLoader.DisableEngine();
            EngineLoader.Unload();

            Engine.StopSever();
            DataStore.StopSever();

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

        #region Engine Events

        public void OnRunStateChanged(object _, RunState rs)
        {
            IconHost.RunState = rs;
        }

        public void OnRunStarting(object _, RunStartParams rd)
        {
        }

        public void OnRunStepStarting(object _, RunStepStartingEventArg rsea)
        {
        }

        public void OnRunStepError(object _, RunStepErrorEventArg ea)
        {
        }

        public void OnRunStepEnded(object _, RunStepEndedEventArg ea)
        {
        }

        public void OnRunError(object _, RunFailureInfo rfi)
        {
        }

        public void OnRunEnded(object _, RunStartParams rsp)
        {
        }

        #endregion
    }
}
