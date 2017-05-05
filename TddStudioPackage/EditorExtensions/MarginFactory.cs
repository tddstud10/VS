using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using R4nd0mApps.TddStud10.Engine.Core;
using R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core;
using R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor;
using R4nd0mApps.TddStud10.Logger;
using System;
using System.ComponentModel.Composition;

namespace R4nd0mApps.TddStud10.Hosts.VS.EditorExtensions
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(MarginConstants.Name)]
    [Order(After = PredefinedMarginNames.Outlining)]
    [MarginContainer(PredefinedMarginNames.Left)]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    public sealed class MarginFactory : IWpfTextViewMarginProvider
    {
        [Import]
        private IBufferTagAggregatorFactoryService _aggregatorFactory = null;

        [Import]
        private SVsServiceProvider _sp = null;

        private readonly Lazy<FSharpOption<RunStartParams>> _rsp = new Lazy<FSharpOption<RunStartParams>>(GetRunStartParams);

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
        {
            return Margin.Create(
                _rsp.Value,
                _sp.GetService<EnvDTE.DTE>(),
                _sp.GetService<IVsDebugger, IVsDebugger3>(),
                textViewHost.TextView,
                _aggregatorFactory.CreateTagAggregator<IMarginGlyphTag>(textViewHost.TextView.TextBuffer));
        }

        private static FSharpOption<RunStartParams> GetRunStartParams()
        {
            try
            {
                return TddStud10Package.Instance.TddStud10Host.GetDataStore().GetRunStartParams().RunSynchronously();
            }
            catch (Exception e)
            {
                LoggerFactory.logger.LogError("MarginFactory: Unable to get RunStartParams. Some features will be disabled!: {0}", e);
                return FSharpOption<RunStartParams>.None;
            }
        }
    }
}
