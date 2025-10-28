namespace NzbWebDAV.Services.FileProcessors;

public abstract class BaseProcessor
{
    public abstract Task<Result> ProcessAsync();
    public class Result { }
}