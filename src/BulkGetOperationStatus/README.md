# Bulk Get Operation Status

This sample demonstrates how to query the status of previously submitted Compute.BulkActions operations using the `BulkGetOperationsStatus` endpoint.

## Overview

When you submit a Start, Deallocate, Delete, or Hibernate operation, the response contains a per-resource `ComputeBulkOperationResult` whose `Operation.OperationId` identifies the long-running operation. You poll those operation IDs with `BulkGetOperationsStatus` until each reaches a terminal state.

## Endpoint

`BulkGetOperationsStatus` / `BulkGetOperationsStatusAsync` is an extension method on `ResourceGroupResource`. It takes an explicit `AzureLocation` identifying the region where the operations were submitted:

```csharp
GetBulkOperationStatusResult result = await resourceGroup.BulkGetOperationsStatusAsync(
    location, new GetBulkOperationStatusContent(operationIds));
```

| Member | Type | Description |
|--------|------|-------------|
| `GetBulkOperationStatusContent.OperationIds` | `IList<string>` | The operation IDs to query. |
| `GetBulkOperationStatusResult.Results` | `IList<ComputeBulkOperationResult>` | One result per requested operation. |

## Response fields

Each `ComputeBulkOperationResult` exposes:

| Field | Type | Description |
|-------|------|-------------|
| `ResourceId` | `ResourceIdentifier` | The VM the operation targets. |
| `ErrorCode` / `ErrorDetails` | `string` | Set when the request for this resource was rejected up front. |
| `Operation` | `ComputeBulkOperationDetails` | The detailed operation status (present when accepted). |

`ComputeBulkOperationDetails` includes:

| Field | Type | Description |
|-------|------|-------------|
| `OperationId` | `string` | The operation identifier. |
| `ResourceId` | `ResourceIdentifier` | The targeted VM. |
| `OperationKind` | `ComputeBulkOperationKind` | The operation kind (Start, Deallocate, Delete, Hibernate). |
| `State` | `BulkActionOperationState` | Current state. Terminal states are `Succeeded`, `Failed`, `Cancelled`. |
| `Error` | `ComputeBulkOperationError` | Populated when the operation failed. |
| `FallbackOperationInfo` | `ComputeBulkFallbackOperationInfo` | Populated when an `OnFailureAction` fallback ran. |
| `RetryPolicy` | `BulkOperationRetryPolicy` | The retry policy that was applied. |

### Operation states (`BulkActionOperationState`)

`PendingScheduling`, `Scheduled`, `PendingExecution`, `Executing`, `Succeeded`, `Failed`, `Cancelled`, `Blocked`, `Unknown`.

## Polling pattern

The shared `HelperMethods.PollOperationStatus` helper polls until all operations reach a terminal state:

```csharp
HashSet<string> operationIds = HelperMethods.GetPollableOperationIds(result.Results);
Dictionary<string, ComputeBulkOperationDetails> completed =
    await HelperMethods.PollOperationStatus(resourceGroup, location, operationIds, "start");
```

## Run

```bash
dotnet run --project src/BulkGetOperationStatus/BulkGetOperationStatus.csproj
```

Replace the placeholder subscription id, resource group, and operation IDs at the top of `Program.cs` with values from a previously submitted operation.
