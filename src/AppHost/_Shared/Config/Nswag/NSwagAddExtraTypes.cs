using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using Reaparr.Application.Contracts;
using Reaparr.SignalR.Contracts;

namespace Reaparr.AppHost;

/// <summary>
///  Adds extra types to the Swagger client that are not automatically added.
/// </summary>
public class NSwagAddExtraTypes : IDocumentProcessor
{
    /// <summary>
    /// Registers classes in the Swagger client.
    /// </summary>
    /// <param name="context">The <see cref="DocumentProcessorContext"/> used to register types in.</param>
    public void Process(DocumentProcessorContext context)
    {
        List<Type> types =
        [
            typeof(MessageTypes),
            typeof(JobTypes),
            typeof(JobStatus),
            typeof(RefreshDataType),
            typeof(DownloadActions),
            typeof(SortDirection),
            typeof(SortType),
            typeof(Sort),
            typeof(NotificationDTO),
            typeof(DesktopMessageDTO),
            typeof(ServerDownloadProgressDTO),
            typeof(DownloadProgressDTO),
            typeof(ServerDownloadProgressMessagePackDTO),
            typeof(DownloadProgressMessagePackDTO),
            typeof(ServerConnectionCheckStatusProgressDTO),
            typeof(MediaQueryFilterDTO),
            // Background job updates
            typeof(CheckAllConnectionStatusUpdateDTO),
            typeof(DownloadJobUpdateDTO),
            typeof(InspectPlexServerJobUpdateDTO),
            typeof(MoveDownloadFileJobUpdateDTO),
            typeof(LibrarySyncJobQueueDTO),
            typeof(LibrarySyncProgressDTO),
            typeof(DownloadPatchDTO),
            typeof(DownloadPatchMessagePackDTO),
            typeof(AppUpdateDownloadProgressDTO),
            typeof(LibraryComparisonCompletedDTO),
            typeof(IntegrationSetupProgressDTO),
        ];

        foreach (var type in types.Where(type => !context.SchemaResolver.HasSchema(type, false)))
            context.SchemaGenerator.Generate(type, context.SchemaResolver);
    }
}
