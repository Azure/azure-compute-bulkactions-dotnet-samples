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
/// Each operation targets an explicit <see cref="AzureLocation"/> supplied by the caller. A
/// resource group's location can differ from the location of the resources it contains, so the
/// operation location must be provided separately rather than inferred from the resource group.
/// </summary>
public static class BulkActionsOperations
{
    /// <summary>
    /// Submits a <c>BulkStartOperation</c> and polls the resulting operations to completion.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkStartOperationAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
        BulkActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine($"[start] Submitting BulkStartOperation for {resourceIds.Count} resource(s)...");
        StartResourceOperationResult response = await resourceGroup.BulkStartOperationAsync(
            location, new ExecuteStartContent(executionParameters, new UserRequestResources(resourceIds)));

        return await SubmitResultToCompletionAsync(resourceGroup, location, response.Results, "start");
    }

    /// <summary>
    /// Submits a <c>BulkDeallocateOperation</c> and polls the resulting operations to completion.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkDeallocateOperationAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
        BulkActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine($"[deallocate] Submitting BulkDeallocateOperation for {resourceIds.Count} resource(s)...");
        DeallocateResourceOperationResult response = await resourceGroup.BulkDeallocateOperationAsync(
            location, new ExecuteDeallocateContent(executionParameters, new UserRequestResources(resourceIds)));

        return await SubmitResultToCompletionAsync(resourceGroup, location, response.Results, "deallocate");
    }

    /// <summary>
    /// Submits a <c>BulkHibernateOperation</c> and polls the resulting operations to completion.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkHibernateOperationAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
        BulkActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine($"[hibernate] Submitting BulkHibernateOperation for {resourceIds.Count} resource(s)...");
        HibernateResourceOperationResult response = await resourceGroup.BulkHibernateOperationAsync(
            location, new ExecuteHibernateContent(executionParameters, new UserRequestResources(resourceIds)));

        return await SubmitResultToCompletionAsync(resourceGroup, location, response.Results, "hibernate");
    }

    /// <summary>
    /// Submits a <c>BulkDeleteOperation</c> and polls the resulting operations to completion.
    /// Set <paramref name="forceDeletion"/> to request a force delete of the virtual machines.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> BulkDeleteOperationAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
        BulkActionExecutionParameterDetail executionParameters,
        List<ResourceIdentifier> resourceIds,
        bool forceDeletion = false)
    {
        Console.WriteLine(
            $"[delete] Submitting BulkDeleteOperation for {resourceIds.Count} resource(s) (forceDeletion={forceDeletion})...");
        DeleteResourceOperationResult response = await resourceGroup.BulkDeleteOperationAsync(
            location, new ExecuteDeleteContent(executionParameters, new UserRequestResources(resourceIds))
            {
                IsForceDeletion = forceDeletion
            });

        return await SubmitResultToCompletionAsync(resourceGroup, location, response.Results, "delete");
    }

    /// <summary>
    /// Cancels in-flight operations by their operation IDs via <c>BulkCancelOperations</c>.
    /// Cancellation is best-effort: an operation that is already in progress cannot be cancelled —
    /// only operations that are still pending are eligible for cancellation.
    /// </summary>
    public static async Task BulkCancelOperationsAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
        IEnumerable<string> operationIds)
    {
        var ids = operationIds.ToList();
        Console.WriteLine($"[cancel] Submitting BulkCancelOperations for {ids.Count} operation(s)...");

        CancelBulkOperationsResult result = await resourceGroup.BulkCancelOperationsAsync(
            location, new CancelBulkOperationsContent(ids));

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
        AzureLocation location,
        IEnumerable<string> operationIds)
    {
        var ids = operationIds.ToList();
        Console.WriteLine($"[status] Submitting BulkGetOperationsStatus for {ids.Count} operation(s)...");

        GetBulkOperationStatusResult result = await resourceGroup.BulkGetOperationsStatusAsync(
            location, new GetBulkOperationStatusContent(ids));

        foreach (ComputeBulkOperationResult operationResult in result.Results)
        {
            ComputeBulkOperationDetails? operation = operationResult.Operation;
            Console.WriteLine(
                $"[status] operationId={operation?.OperationId}, resourceId={operation?.ResourceId}, opKind={operation?.OperationKind}, state={operation?.State}");

            if (operation?.Error is not null)
            {
                Console.WriteLine(
                    $"[status] errorCode={operation.Error.ErrorCode}, errorDetails={operation.Error.ErrorDetails}");
            }

            if (operation?.FallbackOperationInfo is not null)
            {
                ComputeBulkFallbackOperationInfo fallback = operation.FallbackOperationInfo;
                Console.WriteLine(
                    $"[status] fallback lastOpKind={fallback.LastOperationKind}, status={fallback.Status}");
            }
        }

        return result;
    }

    private static async Task<Dictionary<string, ComputeBulkOperationDetails>> SubmitResultToCompletionAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
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
            await HelperMethods.PollOperationStatus(resourceGroup, location, operationIds, operationLabel);

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
