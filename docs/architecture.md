# Architecture and Design

## Introduction

`RoguelikeToolkit.Entities` provides an advanced Entity Component System (ECS) factory implementation built on top of [DefaultEcs](https://github.com/Doraku/DefaultEcs). It allows you to construct entities using JSON or YAML templates with support for inheritance and hierarchies.

## Entity Creation Flow

The following sequence diagram illustrates the flow when a user requests an entity to be created via `EntityFactory`:

```mermaid
sequenceDiagram
    participant App as Application
    participant EF as EntityFactory
    participant ER as EntityTemplateRepository
    participant IR as EntityInheritanceResolver
    participant CF as ComponentFactory
    participant W as DefaultEcs World

    App->>EF: TryCreate("Enemy")
    EF->>ER: TryGetByName("Enemy")
    ER-->>EF: Returns raw EnemyTemplate
    EF->>IR: GetEffectiveTemplate(EnemyTemplate)
    note right of IR: Merges components<br/>from all inherited templates
    IR-->>EF: Returns EffectiveTemplate

    EF->>W: CreateEntity()
    W-->>EF: Returns empty Entity

    loop for each Component in EffectiveTemplate
        EF->>CF: TryCreateInstance(ComponentType, ComponentData)
        CF-->>EF: Returns ComponentInstance
        EF->>EF: SetComponent(Entity, ComponentInstance)
    end

    loop for each EmbeddedTemplate
        EF->>EF: TryCreate(EmbeddedTemplate)
        EF->>EF: RootEntity.SetAsParentOf(ChildEntity)
    end

    EF-->>App: Returns constructed Entity
```

## Template Inheritance Flow

When templates inherit from one another, `EntityInheritanceResolver` uses a caching mechanism to build a flattened effective template.

```mermaid
sequenceDiagram
    participant IR as EntityInheritanceResolver
    participant ER as EntityTemplateRepository

    IR->>IR: GetEffectiveTemplate("GoblinWarrior")
    alt Exists in Cache
        IR-->>IR: Return cached EffectiveTemplate
    else Not in Cache
        IR->>ER: TryGetByName("Goblin")
        ER-->>IR: Return BaseTemplate
        IR->>IR: GetEffectiveTemplate("Goblin") (recursive)
        note right of IR: Traverses all base templates
        IR->>IR: Merge Base components into GoblinWarrior
        IR->>IR: Cache EffectiveTemplate
        IR-->>IR: Return new EffectiveTemplate
    end
```
