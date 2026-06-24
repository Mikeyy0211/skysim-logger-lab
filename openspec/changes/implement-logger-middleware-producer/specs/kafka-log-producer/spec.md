## ADDED Requirements

### Requirement: Kafka Log Producer Interface

The system SHALL provide an abstraction `IKafkaLogProducer` with a `PublishAsync` method that accepts a `LogEventMessage` and a `CancellationToken`, and returns `Task`.

#### Scenario: Publish a log event successfully
- **WHEN** `PublishAsync` is called with a valid `LogEventMessage`
- **THEN** the message is serialized to JSON bytes, sent to the `skysim.action.logs` topic with key set to `flowId` (or `eventId` when `flowId` is empty), and the task completes without error

#### Scenario: Publish fails and retry succeeds
- **WHEN** `PublishAsync` is called and the first Kafka delivery fails with a transient error
- **THEN** the retry policy re-attempts delivery with exponential backoff per `Kafka:Retry` config, and the call succeeds on a subsequent attempt

#### Scenario: Publish exhausts retries and fails silently
- **WHEN** `PublishAsync` is called and all retry attempts are exhausted
- **THEN** the failure is logged via `ILogger` at `Warning` level and no exception is thrown to the caller

### Requirement: Producer Configuration

The system SHALL bind producer settings from the `Kafka:Producer` section of `appsettings.json` and expose them via `IKafkaLogProducerOptions`.

#### Scenario: Producer reads acks configuration
- **WHEN** `appsettings.json` contains `"Kafka": { "Producer": { "Acks": "all" } }`
- **THEN** the producer is created with `acks=all`

#### Scenario: Producer uses configured bootstrap servers
- **WHEN** `appsettings.json` contains `"Kafka": { "Producer": { "BootstrapServers": "localhost:9092" } }`
- **THEN** the producer client connects to `localhost:9092`

### Requirement: Graceful Shutdown

The system SHALL flush pending Kafka messages within a 5-second timeout when the producer is disposed.

#### Scenario: Producer disposes with pending messages
- **WHEN** the `IKafkaLogProducer` instance is disposed while messages are in-flight
- **THEN** the producer calls `Flush(TimeSpan.FromSeconds(5))`, waits up to 5 seconds, logs any messages that could not be flushed, and then releases resources
