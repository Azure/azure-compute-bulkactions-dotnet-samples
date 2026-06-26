# Azure Compute.BulkActions .NET Samples

This repository contains .NET samples for Azure Resource Manager **Compute.BulkActions** operations such as start, deallocate, delete, and hibernate, plus operation management (cancel and status) and retry/fallback policies.

The samples are intentionally small and focused. Each project folder represents a self-contained sample built on the [`Azure.ResourceManager.Compute.BulkActions`](https://www.nuget.org/packages/Azure.ResourceManager.Compute.BulkActions) package.

## Available endpoints

All operations are extension methods on `ResourceGroupResource` from the `Azure.ResourceManager.Compute.BulkActions` package:

| Endpoint (sync / async) | Request content | Result type |
|---|---|---|
| `BulkStartOperation` / `BulkStartOperationAsync` | `ExecuteStartContent` | `StartResourceOperationResult` |
| `BulkDeallocateOperation` / `BulkDeallocateOperationAsync` | `ExecuteDeallocateContent` | `DeallocateResourceOperationResult` |
| `BulkDeleteOperation` / `BulkDeleteOperationAsync` | `ExecuteDeleteContent` (`IsForceDeletion`) | `DeleteResourceOperationResult` |
| `BulkHibernateOperation` / `BulkHibernateOperationAsync` | `ExecuteHibernateContent` | `HibernateResourceOperationResult` |
| `BulkCancelOperations` / `BulkCancelOperationsAsync` | `CancelBulkOperationsContent` | `CancelBulkOperationsResult` |
| `BulkGetOperationsStatus` / `BulkGetOperationsStatusAsync` | `GetBulkOperationStatusContent` | `GetBulkOperationStatusResult` |

## Projects

| Project | What it demonstrates |
|---|---|
| `BulkStart` | Start existing VMs via `BulkStartOperation` |
| `BulkDeallocate` | Deallocate existing VMs via `BulkDeallocateOperation` |
| `BulkDelete` | Delete existing VMs via `BulkDeleteOperation` (with optional force delete) |
| `BulkHibernate` | Hibernate existing VMs via `BulkHibernateOperation` |
| `BulkCancelOperations` | Cancel in-flight operations via `BulkCancelOperations` |
| `BulkGetOperationStatus` | Query operation status via `BulkGetOperationsStatus` |
| `OperationFallback` | Retry policy and `OnFailureAction` fallback scenarios |
| `AllScenarios` | Combined example flow (start → hibernate → deallocate → delete) using shared helpers |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An Azure subscription
- An existing Azure resource group
- Azure CLI installed and signed in with `az login`
- NuGet feeds configured through `src/NuGet.config`

`DefaultAzureCredential` is used throughout the samples, so `az login` is the simplest way to authenticate locally.

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/Azure/azure-compute-bulkactions-dotnet-samples.git
cd azure-compute-bulkactions-dotnet-samples
```

### 2. Sign in to Azure

```bash
az login
```

### 3. Choose and configure a sample

Most samples use static placeholder values (subscription id, resource group, VM names) declared at the top of their `Program.cs`. Replace those with your own values before running.

The `OperationFallback` sample is configured through its `appsettings.json` file instead.

## Build

From the repository root:

```bash
dotnet build src/azure-compute-bulkactions-dotnet-samples.sln
```

From `src`:

```bash
dotnet build
```

## Run Samples

### From the repository root

```bash
dotnet run --project src/BulkStart/BulkStart.csproj
dotnet run --project src/BulkDeallocate/BulkDeallocate.csproj
dotnet run --project src/BulkDelete/BulkDelete.csproj
dotnet run --project src/BulkHibernate/BulkHibernate.csproj
dotnet run --project src/BulkCancelOperations/BulkCancelOperations.csproj
dotnet run --project src/BulkGetOperationStatus/BulkGetOperationStatus.csproj
dotnet run --project src/OperationFallback/OperationFallback.csproj
dotnet run --project src/AllScenarios/AllScenarios.csproj
```

### From an individual project directory

In any sample folder under `src/<ProjectName>`, you can usually run:

```bash
dotnet run
```

## Project Structure

```text
src/
├── Common/
├── BulkStart/
├── BulkDeallocate/
├── BulkDelete/
├── BulkHibernate/
├── BulkCancelOperations/
├── BulkGetOperationStatus/
├── OperationFallback/
├── AllScenarios/
├── NuGet.config
└── azure-compute-bulkactions-dotnet-samples.sln
```

## Documentation

- [src/OperationFallback/README.md](./src/OperationFallback/README.md): retry policy and `OnFailureAction` fallback scenarios
- [src/BulkGetOperationStatus/README.md](./src/BulkGetOperationStatus/README.md): querying operation status and interpreting the response
- [src/BulkDeallocate/docs/deallocate-preempts-start.md](./src/BulkDeallocate/docs/deallocate-preempts-start.md): behavior and API semantics when deallocate preempts a pending or in-progress start

## Shared Code

All sample projects reference `src/Common`, which contains the shared helper layer used across the repository:

- `BulkActionsOperations.cs`: wrappers around the six Compute.BulkActions endpoints (submit + poll to completion)
- `HelperMethods.cs`: subscription/resource-group resolution, VM resource-id builders, operation-id extraction, and status polling
- `ConsoleProgressRenderer.cs`: single-line progress updates for longer-running flows
- `SetHeaderPolicy.cs`: example of adding a custom ARM pipeline header policy
