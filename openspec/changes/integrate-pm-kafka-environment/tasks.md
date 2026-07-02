# Tasks: integrate-pm-kafka-environment

## 1. Update KafkaLogProducer to support configurable topic

- [x] 1.1 Add `topic` parameter to `KafkaLogProducer` constructor
- [x] 1.2 Replace hard-coded `Topic` constant with configurable `_topic` field
- [x] 1.3 Update `PublishAsync` to use `_topic` instead of constant
- [x] 1.4 Update log messages to include `_topic` instead of constant
- [x] 1.5 Update internal constructor for testing to pass topic

## 2. Update SampleService registration

- [x] 2.1 Update `Program.cs` to read `Kafka:Producer:Topic` from configuration
- [x] 2.2 Pass topic to `KafkaLogProducer` constructor
- [ ] 2.3 Verify service starts with local Kafka and default topic

## 3. Add extension methods for Logger registration

- [x] 3.1 Create `Skysim.Logger.Client/Extensions/ServiceCollectionExtensions.cs`
- [x] 3.2 Add `AddSkysimLogger(this IServiceCollection, IConfiguration)` method
- [x] 3.3 Create `Skysim.Logger.Client/Extensions/ApplicationBuilderExtensions.cs`
- [x] 3.4 Add `UseSkysimLogger(this IApplicationBuilder)` method

## 4. Create PM environment configuration

- [x] 4.1 Create `backend/Skysim.Logger.SampleService/appsettings.PM.json`
- [x] 4.2 Add PM Kafka settings (BootstrapServers: 149.28.132.56:9092, Topic: system-event-log)
- [x] 4.3 Add `appsettings.PM.json` to `.gitignore` or document it should not be committed

## 5. Add documentation

- [x] 5.1 Create `KAFKA_SWITCH.md` with instructions to switch between local and PM Kafka
- [x] 5.2 Document environment variable approach (ASPNETCORE_ENVIRONMENT=PM)
- [x] 5.3 Add Postman/Kafka verification checklist for testing

## 6. Update tests (if applicable)

- [x] 6.1 Update `KafkaLogProducerTests` to account for configurable topic
- [x] 6.2 Verify existing tests pass after refactoring

## Verification Checklist

### Local Kafka Testing
- [ ] SampleService starts successfully with default `appsettings.json`
- [ ] HTTP request to SampleService publishes message to `localhost:9092` topic `skysim.action.logs`
- [ ] Message appears in local Kafka (verify with kafka-console-consumer)

### PM Kafka Testing
- [ ] SampleService starts with `ASPNETCORE_ENVIRONMENT=PM`
- [ ] HTTP request to SampleService publishes message to `149.28.132.56:9092` topic `system-event-log`
- [ ] Verify message appears in PM's Kafka topic

### Extension Methods Testing
- [ ] Consuming service can use `AddSkysimLogger()` to register all services
- [ ] Consuming service can use `UseSkysimLogger()` to add middleware
- [ ] Middleware works without separate `FlowIdSeedingMiddleware` registration
