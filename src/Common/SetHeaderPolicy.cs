using Azure.Core;
using Azure.Core.Pipeline;

namespace UtilityMethods;

/// <summary>
/// Example of adding a custom ARM pipeline header policy. The policy below sets a
/// completion-notification header on every outgoing request, demonstrating how to
/// attach custom headers when calling the Compute.BulkActions endpoints.
/// </summary>
public sealed class SetHeaderPolicy : HttpPipelinePolicy
{
    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        // Example: Add or override a custom header for triggering completion notification.
        message.Request.Headers.SetValue("x-ms-sa-completion-notification",
            message.Request.Headers.TryGetValue("x-ms-sa-completion-notification", out var shouldTrigger)
                ? shouldTrigger
                : "false");

        ProcessNext(message, pipeline);
    }

    public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        Process(message, pipeline);
        return ProcessNextAsync(message, pipeline);
    }
}
