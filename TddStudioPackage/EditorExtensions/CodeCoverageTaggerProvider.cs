using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor;
using R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.EditorFrameworkExtensions;
using System.ComponentModel.Composition;

namespace R4nd0mApps.TddStud10.Hosts.VS.EditorExtensions
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("code")]
    [TagType(typeof(CodeCoverageTag))]
    public class CodeCoverageTaggerProvider : ITaggerProvider
    {
        [Import]
        private IBufferTagAggregatorFactoryService _aggregatorFactory;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new CodeCoverageTagger(
                buffer,
                TagAggregator.TagAggregator<SequencePointTag>.NewITagAggregator(
                    _aggregatorFactory.CreateTagAggregator<SequencePointTag>(buffer)),
                TddStud10Package.Instance.DataStore.Server,
                TddStud10Package.Instance.DataStore.Events) as ITagger<T>;
        }
    }
}
