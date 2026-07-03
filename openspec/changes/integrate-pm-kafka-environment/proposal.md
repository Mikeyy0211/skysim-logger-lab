## Why

The LoggerMiddleware design has been reviewed and accepted by PM. PM has provided a Kafka environment (BootstrapServers: `171.244.49.17:9092`, Topic: `system-event-log`) for testing. Before PM integrates our middleware into their service tomorrow, we need to ensure the Logger module is fully configurable to support the PM Kafka environment without code changes.

Currently, the Kafka topic name is hard-coded in `KafkaLogProducer` (`skysim.action.logs`), preventing seamless switching between local and PM Kafka environments.

## What Changes

1. **Make Kafka topic configurable in Logger.Client**
   - Extract hard-coded `Topic` constant from `KafkaLogProducer` into configuration options
   - Add `Kafka:Producer:Topic` setting support

2. **Add PM Kafka environment configuration**
   - Create `appsettings.PM.json` for SampleService with PM Kafka settings
   - Keep local development config (`appsettings.json`, `appsettings.Development.json`) unchanged

3. **Add extension methods for easy middleware registration**
   - Add `AddSkysimLogger()` extension method to register all Logger services
   - Add `UseSkysimLogger()` extension method for middleware pipeline

4. **Update SampleService to support PM Kafka testing**
   - Add documentation for switching between local and PM Kafka

## Capabilities

### New Capabilities

- `pm-kafka-configuration`: Support for PM-provided Kafka environment through configuration
- `logger-registration-extensions`: Extension methods for simplified middleware registration in consuming services

### Modified Capabilities

- `logger-client`: Update `KafkaLogProducer` to read topic from configuration instead of hard-coded constant

## Impact

### Affected Code

- `backend/Skysim.Logger.Client/Producers/KafkaLogProducer.cs` - Extract topic to config
- `backend/Skysim.Logger.SampleService/` - Add PM environment config and update registration
- `backend/Skysim.Logger.Client/` - Add extension methods for registration

### Unaffected Code

- Frontend components and pages
- Database schema and migrations
- API contracts and DTOs
- Business action logging logic
- Logger API consumer (already configurable)

### Configuration Changes

- Add `Kafka:Producer:Topic` to appsettings (default: `skysim.action.logs` for local)
- Add `appsettings.PM.json` with PM Kafka settings for testing
