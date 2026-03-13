# RoguelikeTools.Entities

This is a library to allow resolve entity templates for Entity Component System in a similar way [Ultimate Adom](https://www.ultimate-adom.com/index.php/2018/10/25/making-ultimate-adom-moddable-by-using-entity-component-systems/) is doing.

Supports entity template inheritance and creation of entity hierarchies.  
It is using the awesome [DefaultEcs](https://github.com/Doraku/DefaultEcs) library to construct the entities.

## Features

- **YAML and JSON support:** Entity templates can be written in human-readable `.yaml` or `.json` formats.
- **Inheritance:** Templates can inherit from one or more base templates, mixing components and tags together.
- **Embedded templates:** Hierarchies are fully supported, you can embed child entities inside templates.
- **Scripts:** Components can include scripts, which allow data-driven behavior.
- **Dice parsing:** The system seamlessly creates component variables from standard dice notations (`2d6+3`).

## Quick Start

### 1. Define Templates

Create a file named `templates.yaml` in your project containing your entity definitions:

```yaml
# A base template with generic components
EnemyBase:
  Tags:
    - AI
    - CanMove
  Components:
    Health:
      Max: 10
      Current: 10
    Speed:
      Value: 5

# A concrete template that inherits the EnemyBase
Goblin:
  Inherits:
    - EnemyBase
  Tags:
    - Goblin
  Components:
    Attack:
      Damage: "1d4" # This will automatically be parsed into a Dice object!
    Health:
      Max: 15 # Overrides the EnemyBase's Health Max value
```

### 2. Loading Templates & Creating Entities

Load these templates and generate DefaultEcs entities easily:

```csharp
using DefaultEcs;
using RoguelikeToolkit.Entities.Repository;
using RoguelikeToolkit.Entities.Factory;

var world = new World();
var repository = new EntityTemplateRepository();

// Load the templates
repository.LoadTemplateFolder("path/to/my/templates");

// Initialize the factory
var factory = new EntityFactory(repository, world);

// Create the entity!
if (factory.TryCreate("Goblin", out Entity goblinEntity))
{
    // The goblinEntity now has:
    // - Health component (Max: 15, Current: 10)
    // - Speed component (Value: 5)
    // - Attack component (Damage: 1d4)
    // - Tags: "AI", "CanMove", "Goblin"
}
```

## Advanced Examples

### Embedded Entities

You can embed child entities into another template. When `EntityFactory` creates an `OrcRider`, it also creates the embedded `Wolf` and automatically assigns it as a child using `DefaultEcs.Entity.SetAsParentOf`.

```yaml
Wolf:
  Components:
    Speed:
      Value: 10

OrcRider:
  Components:
    Mount:
      Type: "Wolf"
  # Embeds the 'Wolf' template
  Wolf: {}
```

## Architecture

For more details on the internal architecture and execution flow, see the [Architecture Documentation](docs/architecture.md).