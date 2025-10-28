namespace NzbWebDAV.Exceptions;

public class UsenetArticleNotFoundException(string message) : NonRetryableDownloadException(message)
{
}