## Why

During the `extract-logger-contracts` change, enum definitions (`ActionType`, `CheckoutType`, `FlowType`, `Status`) were created alongside static string constant classes (`ActionTypes`, `CheckoutTypes`, `FlowTypes`, `StatusTypes`) for the same concepts. This duplication violates the agreed minimal architecture and creates confusion about which type to use. The Contracts project should only contain static string constants to avoid duplicate type definitions for the same concept.

## What Changes

- **Remove** duplicate enum files from `Skysim.Logger.Contracts/Constants`:
  - `ActionType.cs`
  - `CheckoutType.cs`
  - `FlowType.cs`
  - `Status.cs`
- **Update** all usages of enum types to their corresponding static string constants:
  - `ActionType.*` → `ActionTypes.*`
  - `CheckoutType.*` → `CheckoutTypes.*`
  - `FlowType.*` → `FlowTypes.*`
  - `Status.*` → `StatusTypes.*`
- **Update** validators, repositories, services, tests, and any mapping code affected by this cleanup.
- **Verify** build and all tests continue to pass.

## Capabilities

### New Capabilities

- `logger-contracts`: Updates the existing `logger-contracts` capability spec to add a requirement that enum definitions are not allowed alongside string constants for the same concept.

### Modified Capabilities

- `logger-contracts`: Clarifies that static string constant classes (`ActionTypes`, `CheckoutTypes`, `FlowTypes`, `StatusTypes`) are the canonical type definitions. Enum variants are removed.

## Impact

- **Skysim.Logger.Contracts/Constants**: 4 enum files deleted, no new files added.
- **Skysim.Logger.Api**: Any usages of removed enum types updated to string constants.
- **Skysim.Logger.Api.Tests**: Any usages of removed enum types updated to string constants.
- **No runtime behavior change**: String constants have identical values to enum names (e.g., `ActionTypes.OrderCreated == "ORDER_CREATED"` matches `ActionType.OrderCreated.ToString()`).
