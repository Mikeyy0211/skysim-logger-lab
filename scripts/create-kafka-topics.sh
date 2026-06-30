#!/bin/bash
#
# create-kafka-topics.sh
# Creates Kafka topics idempotently for the Skysim Logger pipeline.
# Uses --if-not-exists flag so it is safe to run when topics already exist.
#

set -euo pipefail

KAFKA_BROKER="${KAFKA_BROKER:-localhost:9092}"
KAFKA_CONTAINER="${KAFKA_CONTAINER:-skysim-kafka}"

TOPICS=(
    "skysim.action.logs"
    "skysim.action.logs.dlq"
)

echo "=== Kafka Topic Creation Script ==="
echo "Broker: ${KAFKA_BROKER}"
echo ""

# Function to create a topic idempotently
create_topic() {
    local topic_name="$1"
    echo "Creating topic: ${topic_name}"

    docker exec "${KAFKA_CONTAINER}" kafka-topics.sh \
        --create \
        --if-not-exists \
        --bootstrap-server "${KAFKA_BROKER}" \
        --partitions 3 \
        --replication-factor 1 \
        --topic "${topic_name}"

    echo "Topic '${topic_name}' is ready (created if missing)."
    echo ""
}

# Create each topic
for topic in "${TOPICS[@]}"; do
    create_topic "${topic}"
done

echo "=== All topics created successfully ==="
