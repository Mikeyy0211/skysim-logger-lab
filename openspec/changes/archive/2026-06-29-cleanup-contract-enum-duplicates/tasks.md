## 1. Update LogEventMessage to Use String Constants

- [x] 1.1 Change `LogEventMessage.FlowType` property type from `Constants.FlowType` to `string`
- [x] 1.2 Change `LogEventMessage.ActionType` property type from `Constants.ActionType` to `string`
- [x] 1.3 Change `LogEventMessage.Status` property type from `Constants.Status` to `string`
- [x] 1.4 Change `LogEventMessage.CheckoutType` property type from `Constants.CheckoutType?` to `string?`

## 2. Update FlowDomainService

- [x] 2.1 Change `using ActionType` alias from `Skysim.Logger.Contracts.Constants.ActionType` to `Skysim.Logger.Contracts.Constants.ActionTypes`
- [x] 2.2 Change `using Status` alias from `Skysim.Logger.Contracts.Constants.Status` to `Skysim.Logger.Contracts.Constants.StatusTypes`
- [x] 2.3 Change `TerminalActionTypes` HashSet from `HashSet<ActionType>` to `HashSet<string>` with string values (e.g., `ActionTypes.OrderFailed` instead of `ActionType.OrderFailed`)
- [x] 2.4 Update `IsTerminalAction` method signature parameter type from `ActionType` to `string` and from `Status` to `string`

## 3. Update LoggerMiddleware

- [x] 3.1 Change `using FlowType` alias to `Skysim.Logger.Contracts.Constants.FlowTypes`
- [x] 3.2 Change `using Status` alias to `Skysim.Logger.Contracts.Constants.StatusTypes`
- [x] 3.3 Update `FlowType = FlowType.HttpAction` to `FlowType = FlowTypes.HttpAction`
- [x] 3.4 Update `MapStatus` method to return `string` instead of `Status`, returning `StatusTypes.Success` or `StatusTypes.Failed`

## 4. Update KafkaLogConsumerService

- [x] 4.1 Remove `using ActionType` and `using FlowType` aliases (not needed as direct string constants)
- [x] 4.2 Update `flow.LastActionType = message.ActionType.ToString()` to `flow.LastActionType = message.ActionType`
- [x] 4.3 Update `flow.FlowType = message.FlowType.ToString()` to `flow.FlowType = message.FlowType`
- [x] 4.4 Update `flow.CheckoutType = message.CheckoutType?.ToString()` to `flow.CheckoutType = message.CheckoutType`
- [x] 4.5 Update `flow.Status = message.Status.ToString()` to `flow.Status = message.Status`
- [x] 4.6 Update `message.Status == Skysim.Logger.Contracts.Constants.Status.Success` to `message.Status == StatusTypes.Success`
- [x] 4.7 Update `message.Status == Skysim.Logger.Contracts.Constants.Status.Failed` to `message.Status == StatusTypes.Failed`
- [x] 4.8 Update `Status = message.Status.ToString()` to `Status = message.Status`

## 5. Update Test Files

- [x] 5.1 Update `LogFlowTerminalActionTests.cs`: change `using ActionType` and `using Status` aliases, replace all enum references with string constant references
- [x] 5.2 Update `LogActionRepositoryTests.cs`: change `using ActionType` and `using Status` aliases, replace enum values with string constants
- [x] 5.3 Update `LogActionQueryServiceTests.cs`: change all using aliases, replace enum values and `.ToString()` calls with string constants
- [x] 5.4 Update `LogFlowQueryServiceTests.cs`: change all using aliases, replace enum values and `.ToString()` calls with string constants
- [x] 5.5 Update `LogEventMessageValidatorTests.cs`: change all using aliases, replace enum references with string constant references
- [x] 5.6 Update `KafkaLogConsumerServiceTests.cs`: change all using aliases, replace enum references with string constant references, remove `.ToString()` calls
- [x] 5.7 Update `KafkaLogConsumerServicePersistenceTests.cs`: change all using aliases, replace enum values with string constants
- [x] 5.8 Update `KafkaProducerTests.cs`: change all using aliases, replace enum references with string constants
- [x] 5.9 Update `LoggerMiddlewareTests.cs`: change all using aliases, replace enum references with string constants

## 6. Delete Duplicate Enum Files

- [x] 6.1 Delete `Skysim.Logger.Contracts/Constants/ActionType.cs`
- [x] 6.2 Delete `Skysim.Logger.Contracts/Constants/CheckoutType.cs`
- [x] 6.3 Delete `Skysim.Logger.Contracts/Constants/FlowType.cs`
- [x] 6.4 Delete `Skysim.Logger.Contracts/Constants/Status.cs`

## 7. Verify Build and Tests

- [x] 7.1 Run `dotnet build` and verify success
- [x] 7.2 Run `dotnet test` and verify all tests pass
- [x] 7.3 Confirm no remaining references to `ActionType`, `CheckoutType`, `FlowType`, or `Status` as enum types in backend code
