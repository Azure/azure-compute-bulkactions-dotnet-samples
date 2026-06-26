# Retry and Fallback Policy for Compute.BulkActions

This guide explains how to configure retry and fallback behavior for VM operations submitted through the **Compute.BulkActions** endpoints. These features help maximize the success rate of your operations by automatically retrying on transient failures and optionally performing a fallback action when all retries are exhausted.

## Overview

When you submit a VM operation (Start, Deallocate, Hibernate, or Delete), the operation may encounter transient errors from the underlying compute platform. The **Retry Policy** controls how the system automatically retries failed operations, and the **Fallback (`OnFailureAction`)** provides a safety net when retries are exhausted.

The retry/fallback configuration is supplied via the `RetryPolicy` property of `ScheduledActionExecutionParameterDetail`, using the `BulkOperationRetryPolicy` type.

---

## Retry Policy

The retry policy is an **optional** configuration specified in the `ExecutionParameters.RetryPolicy` field of your request. It controls how long and how many times the system retries a failed operation. If not provided, the operation is attempted once with no retries.

### `BulkOperationRetryPolicy` fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `RetryCount` | int | No | The number of retry attempts on failure. **Range: 0–7.** |
| `RetryWindowInMinutes` | int | No | The maximum duration (in minutes) during which retries are allowed, measured from when the operation first starts executing. **Range: 5–120.** |
| `OnFailureAction` | `ComputeBulkOperationType` | No | The fallback action to perform when all retry attempts are exhausted. See [Fallback (OnFailureAction)](#fallback-onfailureaction) below. |

### How retries work

1. The system submits the operation to the compute platform.
2. If the operation fails with a **retriable error**, the system waits for a backoff interval and then retries.
3. The system continues retrying while within both the `RetryCount` and `RetryWindowInMinutes` limits.
4. If the limits are reached and a fallback is configured, the fallback action is executed (see below).
5. If no fallback is configured, the operation is marked as **Failed**.

### Understanding the retry window

`RetryWindowInMinutes` governs when the **last retry can be initiated** — it is **not** a timeout and not a hard deadline for the overall operation to complete. Once a retry request has been submitted to the platform, the system waits for the platform to respond, even if the response arrives after the window has elapsed.

> **Important:** The retry window is not a timeout. The VM operation may still be ongoing even after the retry window has elapsed.

### Example: Start operation with retry policy

```csharp
var executionParams = new ScheduledActionExecutionParameterDetail()
{
    RetryPolicy = new BulkOperationRetryPolicy()
    {
        RetryCount = 3,
        RetryWindowInMinutes = 45
    }
};

StartResourceOperationResult result = await resourceGroup.BulkStartOperationAsync(
    new ExecuteStartContent(executionParams, new UserRequestResources(resourceIds)));
```

### Default behavior (no retry)

By default, if you do not provide a `RetryPolicy` — or provide one without a meaningful `RetryWindowInMinutes` / `RetryCount` — **no retries are performed**. The operation is attempted once, and if it fails, it is immediately marked as failed.

---

## Fallback (OnFailureAction)

The fallback action (`OnFailureAction`) provides a safety net when an operation fails and all retry attempts have been exhausted. Instead of simply failing, the system can perform an alternative operation to leave the VM in a known, safe state. The value is a `ComputeBulkOperationType`.

### Supported fallback actions by operation type

Not all operations support fallback actions. The table below shows which fallback action is available for each operation type:

| Operation Type | Supported `OnFailureAction` | Fallback Behavior |
|----------------|----------------------------|-------------------|
| **Start** | `ComputeBulkOperationType.Start` | If a resume (start on a hibernated VM) fails after all retries, the system performs a clean boot — bringing the VM back online from scratch. The hibernated session state is not preserved. |
| **Hibernate** | `ComputeBulkOperationType.Deallocate` | If hibernate fails after all retries, the system deallocates the VM instead. The VM is deallocated (not hibernated), but resources are released. |
| **Deallocate** | _Not supported_ | Deallocate operations do not support fallback actions. |
| **Delete** | _Not supported_ | Delete operations do not support fallback actions. |

> **Note:** Specifying an unsupported `OnFailureAction` for an operation type will result in a `400 Bad Request` error.

### How fallback works

The fallback action is the **last resort** — it only executes after all retries and the retry window have been exhausted. The sequence is:

1. **Initial attempt** — The system executes the requested operation (e.g., Start).
2. **Retries** — On retriable failure, the system retries within the `RetryCount` / `RetryWindowInMinutes` limits.
3. **Fallback** — If all retries fail, and an `OnFailureAction` is configured, the system executes the fallback action. The fallback is always allowed to run to completion, even if the retry window has expired.
4. **Final status** — The operation is reported with the original operation's error, plus a `FallbackOperationInfo` field in the response showing the fallback outcome.

### Fallback behavior details

- **Optional.** If you do not specify `OnFailureAction`, no fallback is executed when retries are exhausted.
- **Executed at most once.** The fallback is a one-time, last-resort action.
- **Not retried.** If the fallback itself fails, the entire operation fails.
- **Ignores the retry window.** The fallback is always allowed to finish.
- **Original error is preserved.** The response includes both the original error (`ComputeBulkOperationDetails.Error`) and the fallback result (`ComputeBulkOperationDetails.FallbackOperationInfo`).

### Example: Start with clean-boot fallback

```csharp
var executionParams = new ScheduledActionExecutionParameterDetail()
{
    RetryPolicy = new BulkOperationRetryPolicy()
    {
        RetryWindowInMinutes = 30,
        OnFailureAction = ComputeBulkOperationType.Start
    }
};

StartResourceOperationResult result = await resourceGroup.BulkStartOperationAsync(
    new ExecuteStartContent(executionParams, new UserRequestResources(resourceIds)));
```

> 📝 **SDK sample:** See [StartWithCleanBootFallback.cs](StartWithCleanBootFallback.cs) for a complete .NET example including status polling and fallback interpretation.

### Example: Hibernate with Deallocate fallback

```csharp
var executionParams = new ScheduledActionExecutionParameterDetail()
{
    RetryPolicy = new BulkOperationRetryPolicy()
    {
        RetryWindowInMinutes = 30,
        OnFailureAction = ComputeBulkOperationType.Deallocate
    }
};

HibernateResourceOperationResult result = await resourceGroup.BulkHibernateOperationAsync(
    new ExecuteHibernateContent(executionParams, new UserRequestResources(resourceIds)));
```

> 📝 **SDK sample:** See [HibernateWithDeallocateFallback.cs](HibernateWithDeallocateFallback.cs) and [HibernateFallbackOnlyNoRetry.cs](HibernateFallbackOnlyNoRetry.cs).

---

## Checking operation status

When you query the status of an operation via `BulkGetOperationsStatus`, the `ComputeBulkOperationDetails` returned includes the retry policy that was applied and, if applicable, the fallback operation result.

### Relevant response fields (`ComputeBulkOperationDetails`)

| Field | Type | Description |
|-------|------|-------------|
| `State` | `ScheduledActionOperationState` | The current state (e.g., `Succeeded`, `Failed`, `Cancelled`). |
| `RetryPolicy` | `BulkOperationRetryPolicy` | The retry policy that was applied to this operation. |
| `Error` | `ComputeBulkOperationError` | The error from the primary operation, if it failed. |
| `FallbackOperationInfo` | `ComputeBulkFallbackOperationInfo` | Present only if a fallback was executed. Contains `LastOperationType`, `Status`, and `Error`. |

### Interpreting a fallback result

```csharp
if (details.State == ScheduledActionOperationState.Failed && details.FallbackOperationInfo is not null)
{
    ComputeBulkFallbackOperationInfo fallback = details.FallbackOperationInfo;
    Console.WriteLine($"Fallback {fallback.LastOperationType}: Status = {fallback.Status}");

    if (fallback.Error is not null)
    {
        Console.WriteLine($"Fallback error: {fallback.Error.ErrorCode} — {fallback.Error.ErrorDetails}");
    }
}
```

In this example, the original operation failed after exhausting all retries, the fallback was executed, and `FallbackOperationInfo.Status` reports whether the fallback recovered the VM.

---

## Running the samples

The `OperationFallback` project is configured through `appsettings.json`. Set the `Scenario` field to one of:

| Scenario | Demonstrates |
|----------|--------------|
| `HibernateFallback` | Hibernate with Deallocate fallback (with a retry window) |
| `StartFallback` | Start with clean-boot fallback (with a retry window) |
| `HibernateFallbackNoRetry` | Hibernate with Deallocate fallback, no retry window |

```bash
dotnet run --project src/OperationFallback/OperationFallback.csproj
```
