using Azure.Core;
using Azure.ResourceManager.Compute.BulkActions;
using Azure.ResourceManager.Compute.BulkActions.Models;
using Azure.ResourceManager.Resources;

namespace UtilityMethods;

/// <summary>
/// Thin wrappers around the six <c>Azure.ResourceManager.Compute.BulkActions</c> endpoints
/// (extension methods on <see cref="ResourceGroupResource"/>). The execute-style operations
/// submit the request, collect the accepted operation IDs, and poll until completion.
///
/// Each operation executes in the region of the supplied <see cref="ResourceGroupResource"/>;
/// the operation location is derived from the resource group and is not specified separately.
/// </summary>
public static class BulkActionsOperations
{
    /// <summary>
    /// Submits a <c>BulkStartOperation</c> and polls the resulting operations to completion.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkStartOperationAsync(
        ResourceGroupResource resourceGroup,
        ScheduledActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine($"[start] Submitting BulkStartOperation for {resourceIds.Count} resource(s)...");
        StartResourceOperationResult response = await resourceGroup.BulkStartOperationAsync(
            new ExecuteStartContent(executionParameters, new UserRequestResources(resourceIds)));

        return await SubmitResultToCompletionAsync(resourceGroup, response.Results, "start");
    }

    /// <summary>
    /// Submits a <c>BulkDeallocateOperation</c> and polls the resulting operations to completion.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkDeallocateOperationAsync(
        ResourceGroupResource resourceGroup,
        ScheduledActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine($"[deallocate] Submitting BulkDeallocateOperation for {resourceIds.Count} resource(s)...");
        DeallocateResourceOperationResult response = await resourceGroup.BulkDeallocateOperationAsync(
            new ExecuteDeallocateContent(executionParameters, new UserRequestResources(resourceIds)));

        return await SubmitResultToCompletionAsync(resourceGroup, response.Results, "deallocate");
    }

    /// <summary>
    /// Submits a <c>BulkHibernateOperation</c> and polls the resulting operations to completion.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkHibernateOperationAsync(
        ResourceGroupResource resourceGroup,
        ScheduledActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine($"[hibernate] Submitting BulkHibernateOperation for {resourceIds.Count} resource(s)...");
        HibernateResourceOperationResult response = await resourceGroup.BulkHibernateOperationAsync(
            new ExecuteHibernateContent(executionParameters, new UserRequestResources(resourceIds)));

        return await SubmitResultToCompletionAsync(resourceGroup, response.Results, "hibernate");
    }

    /// <summary>
    /// Submits a <c>BulkDeleteOperation</c> and polls the resulting operations to completion.
    /// Set <paramref name="forceDeletion"/> to request a force delete of the virtual machines.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkDeleteOperationAsync(
        ResourceGroupResource resourceGroup,
        ScheduledActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds,
        bool forceDeletion = false)
    {
        Console.WriteLine(
            $"[delete] Submitting BulkDeleteOperation for {resourceIds.Count} resource(s) (forceDeletion={forceDeletion})...");
        DeleteResourceOperationResult response = await resourceGroup.BulkDeleteOperationAsync(
            new ExecuteDeleteContent(executionParameters, new UserRequestResources(resourceIds))
            {
                IsForceDeletion = forceDeletion
            });

        return await SubmitResultToCompletionAsync(resourceGroup, response.Results, "delete");
    }

    /// <summary>
    /// Cancels in-flight operations by their operation IDs via <c>BulkCancelOperations</c>.
    /// Cancellation is best-effort: an operation that is already in progress cannot be cancelled —
    /// only operations that are still pending are eligible for cancellation.
    /// </summary>
    public static async Task BulkCancelOperationsAsync(
        ResourceGroupResource resourceGroup,
        IEnumerable<string> operationIds)
    {
        var ids = operationIds.ToList();
        Console.WriteLine($"[cancel] Submitting BulkCancelOperations for {ids.Count} operation(s)...");

        CancelBulkOperationsResult result = await resourceGroup.BulkCancelOperationsAsync(
            new CancelBulkOperationsContent(ids));

        foreach (ComputeBulkOperationResult operationResult in result.Results)
        {
            if (!string.IsNullOrWhiteSpace(operationResult.ErrorCode))
            {
                Console.WriteLine(
                    $"[cancel] operationId={operationResult.Operation?.OperationId} could not be cancelled. errorCode={operationResult.ErrorCode}, errorDetails={operationResult.ErrorDetails}");
                continue;
            }

            Console.WriteLine(
                $"[cancel] operationId={operationResult.Operation?.OperationId}, state={operationResult.Operation?.State}");
        }
    }

    /// <summary>
    /// Queries the current status of the supplied operation IDs via <c>BulkGetOperationsStatus</c>.
    /// </summary>
    public static async Task<GetBulkOperationStatusResult> BulkGetOperationsStatusAsync(
        ResourceGroupResource resourceGroup,
        IEnumerable<string> operationIds)
    {
        var ids = operationIds.ToList();
        Console.WriteLine($"[status] Submitting BulkGetOperationsStatus for {ids.Count} operation(s)...");

        GetBulkOperationStatusResult result = await resourceGroup.BulkGetOperationsStatusAsync(
            new GetBulkOperationStatusContent(ids));

        foreach (ComputeBulkOperationResult operationResult in result.Results)
        {
            ComputeBulkOperationDetails? operation = operationResult.Operation;
            Console.WriteLine(
                $"[status] operationId={operation?.OperationId}, resourceId={operation?.ResourceId}, opType={operation?.OperationType}, state={operation?.State}");

            if (operation?.Error is not null)
            {
                Console.WriteLine(
                    $"[status] errorCode={operation.Error.ErrorCode}, errorDetails={operation.Error.ErrorDetails}");
            }

            if (operation?.FallbackOperationInfo is not null)
            {
                ComputeBulkFallbackOperationInfo fallback = operation.FallbackOperationInfo;
                Console.WriteLine(
                    $"[status] fallback lastOpType={fallback.LastOperationType}, status={fallback.Status}");
            }
        }

        return result;
    }

    private static async Task<Dictionary<string, ComputeBulkOperationDetails>> SubmitResultToCompletionAsync(
        ResourceGroupResource resourceGroup,
        IEnumerable<ComputeBulkOperationResult> results,
        string operationLabel)
    {
        HashSet<string> operationIds = HelperMethods.GetPollableOperationIds(results);

        if (operationIds.Count == 0)
        {
            Console.WriteLine($"[{operationLabel}] No operations were accepted for polling.");
            return new Dictionary<string, ComputeBulkOperationDetails>();
        }

        Console.WriteLine($"[{operationLabel}] {operationIds.Count} operation(s) accepted. Polling for completion...");
        foreach (string operationId in operationIds)
        {
            Console.WriteLine($"[{operationLabel}] accepted operationId={operationId}");
        }

        Dictionary<string, ComputeBulkOperationDetails> completed =
            await HelperMethods.PollOperationStatus(resourceGroup, operationIds, operationLabel);

        PrintSummary(completed, operationLabel);
        return completed;
    }

    private static void PrintSummary(
        Dictionary<string, ComputeBulkOperationDetails> completedOperations,
        string operationLabel)
    {
        foreach ((string operationId, ComputeBulkOperationDetails details) in completedOperations)
        {
            Console.WriteLine($"[{operationLabel}] Completed operationId={operationId}, finalState={details.State}");
        }
    }
}
