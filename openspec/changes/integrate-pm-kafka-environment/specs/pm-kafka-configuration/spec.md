# pm-kafka-configuration Specification

## ADDED Requirements

### Requirement: KafkaLogProducer shall read topic from configuration

The `KafkaLogProducer` SHALL read the Kafka topic name from configuration option `Kafka:Producer:Topic` instead of using a hard-coded constant. If the configuration is not set, the producer SHALL use `"skysim.action.logs"` as the default topic name.

#### Scenario: Producer uses topic from configuration when set

- **GIVEN** `KafkaLogProducer` is instantiated with `topic` parameter set to `"system-event-log"`
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the Kafka message SHALL be published to topic `"system-event-log"`

#### Scenario: Producer uses default topic when configuration is not set

- **GIVEN** `KafkaLogProducer` is instantiated with `topic` parameter set to `null` or empty
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the Kafka message SHALL be published to topic `"skysim.action.logs"`

#### Scenario: Producer logs the topic name on successful delivery

- **GIVEN** `KafkaLogProducer` is configured with `topic` = `"system-event-log"`
- **WHEN** a message is successfully published
- **THEN** the log entry SHALL include `Topic=system-event-log`

### Requirement: Logger.Client shall support PM Kafka environment via appsettings

The `Skysim.Logger.Client` SHALL be usable with a PM-provided Kafka environment (BootstrapServers: `149.28.132.56:9092`, Topic: `system-event-log`) through standard .NET configuration without code changes.

#### Scenario: SampleService runs against PM Kafka via appsettings.PM.json

- **GIVEN** `appsettings.PM.json` is created with PM Kafka settings
- **AND** the service is started with `ASPNETCORE_ENVIRONMENT=PM`
- **WHEN** the service publishes a log event
- **THEN** the event SHALL be published to `149.28.132.56:9092` with topic `system-event-log`

#### Scenario: Local development uses default local Kafka settings

- **GIVEN** the service is started with default environment
- **WHEN** the service publishes a log event
- **THEN** the event SHALL be published to `localhost:9092` with topic `skysim.action.logs`
