﻿using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor;

namespace R4nd0mApps.TddStud10.Hosts.VS.EditorExtensions
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("code")]
    [TagType(typeof(FailurePointTag))]
    public class FailurePointTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new FailurePointTagger(
                buffer,
                TddStud10Package.Instance.TddStud10Host.GetDataStore(),
                TddStud10Package.Instance.TddStud10Host.GetDataStoreEvents()) as ITagger<T>;
        }
    }
}
