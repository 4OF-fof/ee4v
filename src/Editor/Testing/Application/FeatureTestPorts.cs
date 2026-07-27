using System;
using System.Collections.Generic;
using Ee4v.Testing.Contracts;

namespace Ee4v.Testing.Application
{
    public interface IFeatureTestCatalog
    {
        IReadOnlyList<FeatureTestDescriptor> Refresh();
    }

    public interface IFeatureTestRunner
    {
        event Action Changed;

        bool IsRunInProgress { get; }

        FeatureTestRunRecord GetRecord(string featureScope);

        bool TryRun(
            FeatureTestDescriptor descriptor,
            out string errorMessage);

        bool TryRunAll(
            IReadOnlyList<FeatureTestDescriptor> descriptors,
            out string errorMessage);
    }
}
