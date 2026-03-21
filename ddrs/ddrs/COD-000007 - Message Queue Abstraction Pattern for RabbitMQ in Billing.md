# Metadata
ID: COD-000007
Title: Message Queue Abstraction Pattern for RabbitMQ
Type: Referential
Summary: Captures the transport-neutral message queue pattern introduced in Billing, including Core contracts, RabbitMQ implementation, DI registration, topology mapping, testing strategy, and practical guidance for repeating the pattern in future domains.
Status: Approved
Creator: Kevin D. Wolf
Creation Date: 2026-03-15T00:00:00.000Z
Last Updated Date: 2026-03-15T00:00:00.000Z
Last Updated By: Kevin D. Wolf

# Body

## 1. Purpose

This DDR records the lightweight message queue abstraction pattern created while refactoring Billing away from direct RabbitMQ usage embedded in publishers and consumers.

The goal of the pattern is to:

- remove RabbitMQ-specific plumbing from business-facing classes
- centralize queue configuration and topology decisions
- make publisher and consumer implementations small and testable
- fail fast on misconfiguration
- provide a repeatable template for future domains and future services

This is a reference DDR, not a workflow or policy DDR. It exists so the pattern can be rediscovered and repeated later without re-deriving the design from scratch.

---

## 2. Problem Summary

Before this refactor, Billing publishers and consumers directly owned RabbitMQ concerns such as:

- `ConnectionFactory`
- connection and channel lifecycle
- retry loops and delays
- host, username, password, virtual host binding
- serialization and publish calls
- queue consume setup and ACK/NACK flow

This caused several problems:

- brittle setup and configuration code repeated across classes
- reduced testability because infrastructure and business logic were mixed together
- higher likelihood of drift between queue consumers and publishers
- difficulty repeating the pattern consistently in future services

The strongest design smell was that business classes were forced to know too much about transport details.

---

## 3. Decision Summary

The solution was to split responsibilities into three layers:

### 3.1 Core contracts

A transport-neutral message queue abstraction was added to Core using `IMessageQueue...` naming.

Key concepts:

- `IMessageQueuePublisher`
- `IMessageQueueHandler<T>`
- `IMessageQueueTopology`
- `IMessageQueueTypeRegistry`
- `MessageQueueContext<T>`
- `MessageQueuePublishRoute`
- `MessageQueueSubscriptionRoute`

Important naming decision:

- use `MessageQueue` rather than broad `Messaging` names because the platform already uses messaging concepts for email and other communication channels

Important routing decision:

- use CLR types for registration and ownership lookup
- do **not** use CLR types directly as exchange, queue, or routing names
- keep transport topology explicit via route models

### 3.2 RabbitMQ implementation package

A dedicated RabbitMQ implementation package was introduced under:

- `LagoVista.MessageQueue.RabbitMQ`

This package owns:

- connection settings adaptation
- type-to-service registration
- service registration validation
- RabbitMQ publishing implementation
- DI registration extension methods
- integration tests against real RabbitMQ in Docker

This package is the only place that should directly know about `RabbitMQ.Client`.

### 3.3 Billing adoption

Billing publishers and consumers were trimmed down to business-facing adapters and handlers.

Publishers now:

- validate input
- wrap payload when needed
- call `IMessageQueuePublisher`
- map exceptions to `InvokeResult`

Consumers now:

- implement `IMessageQueueHandler<T>`
- validate context
- call the domain collaborator or pipeline
- log and rethrow only when needed
- no longer manage channels, consumers, ACK/NACK, or Rabbit setup directly

---

## 4. Key Design Rules

### 4.1 Keep Rabbit out of business code

Business classes should not use:

- `ConnectionFactory`
- `IConnection`
- `IChannel`
- queue declarations
- direct publish calls
- Rabbit-specific retry loops

All such concerns belong in the RabbitMQ implementation package.

### 4.2 Use CLR types for ownership, not topology

CLR types are a good fit for:

- message contract registration
- handler binding
- service ownership lookup
- duplicate registration detection

CLR types are **not** a good fit for broker topology naming.

Topology should stay explicit:

- `DestinationName`
- `RouteKey`
- `QueueName`

This prevents accidental broker contract changes caused by namespace or class refactors.

### 4.3 Fail fast on configuration errors

Any registration or topology error should throw during setup rather than during first use.

Examples:

- missing service registrations
- missing message type registrations
- duplicate service names
- duplicate message contract registrations
- message type mapped to unknown service
- missing host, username, password, or virtual host

This was considered an important part of the design, not just a convenience.

### 4.4 Keep connection configuration implementation-specific

Core should not define a universal queue connection shape.

Instead:

- Core owns behavior contracts and route models
- Rabbit package owns `RabbitMqConnectionSettings`
- existing connection objects such as `IConnectionSettings` can be adapted into the Rabbit-specific shape during registration

This keeps the abstraction transport-neutral.

---

## 5. Naming and Structure

### 5.1 Chosen naming

The chosen naming convention is:

- `IMessageQueuePublisher`
- `IMessageQueueHandler<T>`
- `IMessageQueueTopology`
- `MessageQueuePublishRoute`
- `MessageQueueSubscriptionRoute`

This naming was chosen because it is explicit and avoids confusion with broader platform messaging concepts.

### 5.2 Route model language

RabbitMQ-specific names such as `ExchangeName` were removed from Core.

Transport-neutral names were chosen instead:

- `DestinationName`
- `RouteKey`

In RabbitMQ, these map naturally to:

- destination → exchange
- route key → routing key

### 5.3 Project separation

Typical project split:

- `LagoVista.Core` → abstractions and route models
- `LagoVista.MessageQueue.RabbitMQ` → RabbitMQ implementation
- Billing project → composition root and domain-specific topology/usage

This means the Billing project references the RabbitMQ package in its startup or composition root, but its business classes only depend on Core abstractions.

---

## 6. Billing Pattern Captured

The Billing refactor produced four application-facing implementations:

### Publishers

- `ImportedTransactionPublisher`
- `PlaidEventPublisher`

### Consumers / handlers

- `PlaidSyncConsumer`
- `TransactionIngestConsumer`

These classes became dramatically smaller after RabbitMQ code was removed.

A notable adaptation was `PlaidEventPublisher`, which publishes a wrapped message type:

- `PlaidSyncRequestedMessage`

This wrapper holds:

- routing key
- original payload

This allowed the current abstraction, which routes by contract type, to support the Plaid publishing scenario without reintroducing RabbitMQ logic into the publisher class.

---

## 7. Registration Pattern

A Billing-specific startup/registration class was created under the Billing messaging namespace.

The registration pattern performs the following:

- resolves Billing settings
- adapts connection settings into `RabbitMqConnectionSettings`
- explicitly sets the virtual host
- creates a Billing-specific topology
- registers RabbitMQ services by logical service name
- maps message contract types to the owning MQ service
- registers domain publishers and consumers/handlers

One important observation from the session:

- registration worked, but it was recognized as a little heavy
- this was accepted as a reasonable first pass because the real goal was to simplify workers and transport usage
- future cleanup can reduce the weight without changing the overall pattern

Potential future cleanup:

- move nested topology/constants out of startup
- reduce service-provider assembly work inside registration
- wrap setup in domain-specific extension methods such as `AddBillingMessaging(...)`

---

## 8. Testing Strategy

The pattern is backed by both integration and unit tests.

### 8.1 Integration tests

Integration tests were added against a real RabbitMQ instance running in Docker using Testcontainers.

Purpose:

- prove the implementation can publish to an actual RabbitMQ broker
- validate connection mapping, routing, and payload delivery
- keep infrastructure confidence grounded in reality

This was especially useful for the RabbitMQ implementation package.

### 8.2 Unit tests for RabbitMQ implementation

Unit tests were added for:

- `RabbitMqConnectionSettings`
- `RabbitMqServiceRegistration`
- `RabbitMqMessageQueueTypeRegistry`
- `RabbitMqServiceCollectionExtensions`
- `RabbitMqTypeRegistration`
- `RabbitMqMessageQueueBuilder`
- `RabbitMqMessageQueuePublisher`

Main testing themes:

- misconfiguration should throw
- duplicates should throw
- unknown mappings should throw
- service ownership lookup should be deterministic
- registration should fail fast

### 8.3 Unit tests for Billing implementations

After the abstraction was in place, Billing publishers and consumers became small enough that their tests became almost trivial.

Examples:

- publisher wraps and forwards payload
- publisher returns error when required inputs missing
- consumer invokes correct collaborator
- pipeline runner gets correct envelope
- dead-letter sink is written when requested

This was viewed as one of the major practical wins of the refactor.

---

## 9. Practical Guidance for Reuse

This pattern should be reused when adding MQ-based behavior to other domains.

### Repeatable recipe

1. Define a small transport-neutral contract in Core if needed.
2. Keep broker-specific behavior inside the implementation package.
3. Define domain topology explicitly.
4. Register message types to MQ services by CLR contract type.
5. Keep publishers and handlers thin.
6. Make misconfiguration throw early.
7. Add integration tests for real broker behavior.
8. Add unit tests for registry, configuration, and domain wrappers.

### Warning signs to avoid

- direct RabbitMQ usage appearing in managers or workers
- broker topology names inferred automatically from CLR type names
- startup registrations that silently allow duplicate type mappings
- one-off exceptions that bypass the pattern

The pattern is expected to scale to future services and larger codebases specifically because it standardizes these moves.

---

## 10. Follow-Up Ideas

The following were identified as future opportunities, but intentionally left out of this chapter:

- hosted consumer runner abstraction
- first-class route override support for dynamic routing cases
- full dead-letter queue publishing strategy
- retry/poison-message policy standardization
- reducing registration ceremony further
- creating a short internal field guide for repeating the pattern later

---

## 11. Outcome

This refactor successfully:

- removed brittle RabbitMQ setup code from Billing publishers and consumers
- introduced a clean transport-neutral abstraction
- isolated RabbitMQ implementation details in a dedicated package
- established a repeatable registration and topology approach
- added meaningful integration and unit coverage
- created a pattern expected to be reusable later in larger codebases

This chapter can be considered complete, with future work focused on refinement rather than foundational redesign.

# Approval
Approver: Kevin D. Wolf
Approval Timestamp: 2026-03-15T00:00:00.000Z
