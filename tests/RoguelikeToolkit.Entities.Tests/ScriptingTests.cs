using DefaultEcs;
using Jint.Runtime;
using RoguelikeToolkit.Entities.Scripting.Jint;

// ReSharper disable ExceptionNotDocumented
#pragma warning disable CS1591
namespace RoguelikeToolkit.Entities.Tests;

public sealed class ScriptingTests : IDisposable
{
    private readonly World _world = new();
    private readonly ScriptHost _host = new(new ScriptHostOptions { ReferenceTypes = new[] { typeof(Health), typeof(Inventory) } });

    public ScriptingTests() => EntityScripting.Register();

    public void Dispose()
    {
        _host.Dispose();
        _world.Dispose();
    }

    [Fact]
    public void Component_script_mutates_value_component_and_writes_it_back()
    {
        var entity = _world.CreateEntity();
        entity.Set(new Health { Max = 10, Current = 10 });

        var script = new EntityComponentScript("component.Current -= damage;");

        Assert.True(script.TryExecuteOn<Health>(_host, entity, ("damage", 3)));
        Assert.Equal(7, entity.Get<Health>().Current);
        Assert.Equal(10, entity.Get<Health>().Max);
    }

    [Fact]
    public void Component_script_reports_missing_component()
    {
        var entity = _world.CreateEntity();
        var script = new EntityComponentScript("throw new Error('must not run');");

        Assert.False(script.TryExecuteOn<Health>(_host, entity));
    }

    [Fact]
    public void Component_script_runs_on_component_pair()
    {
        var attacker = _world.CreateEntity();
        var defender = _world.CreateEntity();
        attacker.Set(new Health { Max = 10, Current = 10 });
        defender.Set(new Health { Max = 8, Current = 8 });

        var script = new EntityComponentScript("target.Current -= 5; source.Current += 1;");

        Assert.True(script.TryExecuteOn<Health, Health>(_host, attacker, defender));
        Assert.Equal(11, attacker.Get<Health>().Current);
        Assert.Equal(3, defender.Get<Health>().Current);
    }

    [Fact]
    public void Entity_script_reads_and_writes_components_through_non_generic_interop()
    {
        var entity = _world.CreateEntity();
        entity.Set(new Health { Max = 10, Current = 2 });

        var script = new EntityScript(
            """
            if (!entity.HasComponent(Health)) throw new Error('no health');
            var h = entity.GetComponent(Health);
            h.Current = h.Max;
            entity.SetComponent(Health, h);
            if (entity.HasComponent(Inventory)) entity.RemoveComponent(Inventory);
            """);

        entity.Set(new Inventory { Items = new List<string> { "torch" } });
        script.ExecuteOn(_host, ref entity);

        Assert.Equal(10, entity.Get<Health>().Current);
        Assert.False(entity.Has<Inventory>());
    }

    [Fact]
    public void Entity_script_mutates_reference_component_in_place()
    {
        var entity = _world.CreateEntity();
        var inventory = new Inventory { Items = new List<string>() };
        entity.Set(inventory);

        new EntityScript("entity.GetComponent(Inventory).Items.Add(item);").ExecuteOn(_host, ref entity, new ScriptArgument("item", "sword"));

        Assert.Equal(new[] { "sword" }, inventory.Items);
    }

    [Fact]
    public void Interaction_script_binds_source_and_target()
    {
        var source = _world.CreateEntity();
        var target = _world.CreateEntity();
        source.Set(new Health { Max = 5, Current = 5 });
        target.Set(new Health { Max = 5, Current = 5 });

        var script = new EntityInteractionScript(
            """
            var h = target.GetComponent(Health);
            h.Current -= source.GetComponent(Health).Max;
            target.SetComponent(Health, h);
            """);
        script.ExecuteOn(_host, ref source, ref target);

        Assert.Equal(0, target.Get<Health>().Current);
        Assert.Equal(5, source.Get<Health>().Current);
    }

    [Fact]
    public void Declarations_do_not_leak_between_executions_and_bindings_are_cleared()
    {
        var entity = _world.CreateEntity();
        entity.Set(new Health { Max = 1, Current = 1 });

        new EntityScript("var leaked = 42; function helper() { return 1; }").ExecuteOn(_host, ref entity, ("extra", 1));
        var probe = new Health();
        new Script("target.Current = (typeof leaked === 'undefined' && typeof helper === 'undefined') ? 1 : 0;")
            .ExecuteOn(_host, ref probe);

        Assert.Equal(1, probe.Current);
        Assert.True(_host.Engine.GetValue("entity") == Jint.Native.JsValue.Undefined);
        Assert.True(_host.Engine.GetValue("extra") == Jint.Native.JsValue.Undefined);
        Assert.True(_host.Engine.GetValue("target") == Jint.Native.JsValue.Undefined);
    }

    [Fact]
    public void Return_exits_early()
    {
        var health = new Health { Current = 1 };
        new Script("if (target.Current > 0) return; target.Current = 99;").ExecuteOn(_host, ref health);
        Assert.Equal(1, health.Current);
    }

    [Fact]
    public void Statement_limit_aborts_runaway_script_and_host_stays_usable()
    {
        using var host = new ScriptHost(new ScriptHostOptions { MaxStatements = 100 });
        var health = new Health();

        Assert.Throws<StatementsCountOverflowException>(() => new Script("while (true) { }").ExecuteOn(host, ref health));

        new Script("target.Current = 3;").ExecuteOn(host, ref health);
        Assert.Equal(3, health.Current);
    }

    [Fact]
    public void Reassigning_binding_to_incompatible_value_throws()
    {
        var health = new Health();
        Assert.Throws<InvalidCastException>(() => new Script("target = 'nope';").ExecuteOn(_host, ref health));
    }

    [Fact]
    public void Identical_sources_share_the_parsed_program()
    {
        var first = new Script("target.Current = 1;");
        var second = new Script("target.Current = 1;");

        Assert.Same(first.Prepared.Program, second.Prepared.Program);
    }

    [Fact]
    public void Invalid_source_fails_at_construction()
    {
        Assert.ThrowsAny<Exception>(() => new EntityScript("this is not javascript ((("));
    }

    [Fact]
    public void Scripts_are_shareable_across_hosts()
    {
        using var otherHost = new ScriptHost();
        var script = new Script("target.Current += 1;");
        var health = new Health();

        script.ExecuteOn(_host, ref health);
        script.ExecuteOn(otherHost, ref health);

        Assert.Equal(2, health.Current);
    }

    public struct Health
    {
        public int Max { get; set; }

        public int Current { get; set; }
    }

    public sealed class Inventory
    {
        public List<string> Items { get; set; } = new();
    }
}
