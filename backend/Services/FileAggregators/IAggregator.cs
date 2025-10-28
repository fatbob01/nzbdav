using NzbWebDAV.Services.FileProcessors;

namespace NzbWebDAV.Services.FileAggregators;

public interface IAggregator
{
    public void UpdateDatabase(List<BaseProcessor.Result> processorResults);
}