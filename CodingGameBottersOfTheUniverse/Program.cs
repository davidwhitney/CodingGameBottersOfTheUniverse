using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodingGameBottersOfTheUniverse
{
    #region gameloop
    public class Player
    {
        public static void Main(string[] args)
        {
            var outputChannel = new ConsoleOutput();
            var allTactics = typeof(ITactic).Assembly.GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(ITactic)))
                .Select(Activator.CreateInstance)
                .Cast<ITactic>()
                .ToList();

            var hero = new HeroController(HeroType.Hulk, allTactics, outputChannel);

            var game = InputParser.GetGameState();
            
            while (true)
            {
                var turn = InputParser.GetTurnState(game);
                if (turn.InitilisationRound)
                {
                    hero.Spawn();
                    continue;
                }
                
                hero.Unit = turn.MyHero;
                hero.ExecuteTactics(turn);
            }
        }
    }
    #endregion

    public class HeroController
    {
        public Unit Unit { get; set; }
        public List<ITactic> Tactics { get; set; }

        private readonly HeroType _heroType;
        private readonly IOutputChannel _output;
        
        public HeroController(HeroType heroType, List<ITactic> tactics, IOutputChannel output)
        {
            _heroType = heroType;
            _output = output;
            Tactics = tactics;
        }

        public ITactic SelectTactic(TurnState turn)
        {
            return Tactics.OrderBy(x => x.RankTactic(turn)).First();
        }

        public void ExecuteTactics(TurnState turn)
        {
            SelectTactic(turn).Execute(this, turn);
        }

        public void Spawn() => _output.WriteLine(_heroType.ToString().ToUpper());
        public void Wait() => _output.WriteLine("WAIT");
        public void Move(int x, int y) => _output.WriteLine($"MOVE {x} {y}");
        public void MoveAttack(int x, int y, int unitId) => _output.WriteLine($"MOVE_ATTACK {x} {y} {unitId}");
        public void MoveAttack(int x, int y, Unit unit) => _output.WriteLine($"MOVE_ATTACK {x} {y} {unit.Id}");
        public void Attack(int unitId) => _output.WriteLine($"ATTACK {unitId}");
        public void Attack(Unit unit) => _output.WriteLine($"ATTACK {unit.Id}");
        public void AttackNearest(string unitType) => _output.WriteLine($"ATTACK_NEAREST {unitType}");
        public int TimeToMove(int distance) => distance / Unit.MovementSpeed;

    }

    public class EvadeHeroWhenWeak : ITactic
    {
        public int RankTactic(TurnState turn)
        {
            if (turn.MyHero.Health < 25)
            {
                
            }
            return 1;
        }

        public void Execute(HeroController controller, TurnState turn)
        {
            controller.AttackNearest("UNIT");
        }
    }

    public class OnlyAttackTrashMobs : ITactic
    {
        public int RankTactic(TurnState turn)
        {
            return 1;
        }

        public void Execute(HeroController controller, TurnState turn)
        {
            controller.AttackNearest("UNIT");
        }
    }

    public class AttackHero : ITactic
    {
        public int RankTactic(TurnState turn)
        {
            if (turn.EnemyHero.HealthPercentage <= 50 && turn.MyHero.HealthPercentage > 80)
            {
                return 1000;
            }

            if (turn.Enemies.Count() == 2 && turn.EnemyHero.HealthPercentage <= 25)
            {
                return 100;
            }

            if (turn.Enemies.Count() < 5)
            {
                return 1;
            }

            return 0;
        }

        public void Execute(HeroController controller, TurnState turn)
        {
            controller.AttackNearest("HERO");
        }
    }

    #region Domain
    public interface IOutputChannel
    {
        void WriteLine(string msg);
        void Debug(string msg);
    }

    public enum UnitType
    {
        Unit, Hero, Tower, Groot
    }

    public class ConsoleOutput : IOutputChannel
    {
        public void WriteLine(string msg)
        {
            Console.WriteLine(msg);
        }

        public void Debug(string msg)
        {
            Console.Error.WriteLine(msg);
        }
    }

    public enum HeroType
    {
        Deadpool, Doctor_Strange, Hulk, Ironman, Valkrie
    }

    public class GameState
    {
        public int MyTeam { get; set; }
        public List<Entity> Entities { get; set; } = new List<Entity>();
        public List<Item> Items { get; set; } = new List<Item>();
    }

    public class TurnState
    {
        public GameState Game { get; set; }
        public int EntityCount { get; set; }
        public int Gold { get; set; }
        public int EnemyGold { get; set; }
        public int RoundType { get; set; }
        public List<Unit> Units { get; set; } = new List<Unit>();
        public bool InitilisationRound => RoundType < 0;
        public int NumberOfHerosToOrder => RoundType;
        public IEnumerable<Unit> Enemies => Units.Where(x => x.Team != Game.MyTeam);
        public IEnumerable<Unit> MyUnits => Units.Where(x => x.Team == Game.MyTeam);
        public Unit MyHero => MyUnits.SingleOrDefault(x => x.UnitType == UnitType.Hero);
        public Unit EnemyHero => Enemies.SingleOrDefault(x => x.UnitType == UnitType.Hero);

        public override string ToString() => this.ToDebugString();
    }

    public class Unit : WorldObject
    {
        public int Id { get; set; }
        public int Team { get; set; }
        public UnitType UnitType { get; set; }
        public int AttackRange { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Shield { get; set; }
        public int AttackDamage { get; set; }
        public int MovementSpeed { get; set; }
        public int StunDuration { get; set; }
        public int GoldValue { get; set; }
        public int CountDown1 { get; set; }
        public int CountDown2 { get; set; }
        public int CountDown3 { get; set; }
        public int Mana { get; set; }
        public int MaxMana { get; set; }
        public int ManaRegeneration { get; set; }
        public string HeroType { get; set; }
        public int IsVisible { get; set; }
        public int ItemsOwned { get; set; }

        public int HealthPercentage => (Health / MaxHealth) * 100;

        public bool InAttackRange(Unit other)
        {
            return false;   
        }

        public override string ToString() => this.ToDebugString();
    }

    public class Item
    {
        public string ItemName { get; set; }
        public int ItemCost { get; set; }
        public int Damage { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Mana { get; set; }
        public int MaxMana { get; set; }
        public int MoveSpeed { get; set; }
        public int ManaRegeneration { get; set; }
        public int IsPotion { get; set; }
    }

    public class Entity : WorldObject
    {
        public string EntityType { get; set; }
        public int Radius { get; set; }
    }

    public class WorldObject
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public interface ITactic
    {
        int RankTactic(TurnState turn);
        void Execute(HeroController controller, TurnState turn);
    }
    #endregion

    #region Debugging
    public static class Extensions
    {
        public static string ToDebugString(this object src)
        {
            var builder = new StringBuilder(src.GetType().Name + ": {");
            foreach (var prop in src.GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(CollectionBase))
                {
                    var value = (CollectionBase)prop.GetValue(src);
                    foreach (var item in value)
                    {
                        builder.AppendLine(item.ToDebugString());
                    }
                }
                else
                {
                    try
                    {
                        builder.AppendLine(prop.Name + ":" + prop.GetValue(src));
                    }
                    catch (Exception ex)
                    {
                        builder.AppendLine(prop.Name + ":" + ex.Message);
                    }
                }
            }
            builder.AppendLine("}");
            return builder.ToString();

        }
    }
    #endregion
    
    #region Parsing
    public static class InputParser
    {

        public static TurnState GetTurnState(GameState game)
        {
            var turnState = new TurnState
            {
                Gold = int.Parse(Console.ReadLine()),
                EnemyGold = int.Parse(Console.ReadLine()),
                RoundType =
                    int.Parse(Console.ReadLine()), // a positive value will show the number of heroes that await a command
                EntityCount = int.Parse(Console.ReadLine())
            };
            for (var i = 0; i < turnState.EntityCount; i++)
            {
                var turnInput = Console.ReadLine().Split(' ').ToList();
                turnState.Units.Add(new Unit
                {
                    Id = int.Parse(turnInput[0]),
                    Team = int.Parse(turnInput[1]),
                    UnitType = (UnitType)Enum.Parse(typeof(UnitType), turnInput[2], true), // UNIT, HERO, TOWER, can also be GROOT from wood1
                    X = int.Parse(turnInput[3]),
                    Y = int.Parse(turnInput[4]),
                    AttackRange = int.Parse(turnInput[5]),
                    Health = int.Parse(turnInput[6]),
                    MaxHealth = int.Parse(turnInput[7]),
                    Shield = int.Parse(turnInput[8]), // useful in bronze
                    AttackDamage = int.Parse(turnInput[9]),
                    MovementSpeed = int.Parse(turnInput[10]),
                    StunDuration = int.Parse(turnInput[11]), // useful in bronze
                    GoldValue = int.Parse(turnInput[12]),
                    CountDown1 = int.Parse(turnInput[13]), // all countDown and mana variables are useful starting in bronze
                    CountDown2 = int.Parse(turnInput[14]),
                    CountDown3 = int.Parse(turnInput[15]),
                    Mana = int.Parse(turnInput[16]),
                    MaxMana = int.Parse(turnInput[17]),
                    ManaRegeneration = int.Parse(turnInput[18]),
                    HeroType = turnInput[19], // DEADPOOL, VALKYRIE, DOCTOR_STRANGE, HULK, IRONMAN
                    IsVisible = int.Parse(turnInput[20]), // 0 if it isn't
                    ItemsOwned = int.Parse(turnInput[21]), // useful from wood1
                });
            }
            turnState.Game = game;
            return turnState;
        }

        public static GameState GetGameState()
        {
            List<string> inputs;
            var state = new GameState
            {
                MyTeam = int.Parse(Console.ReadLine())
            };

            var bushAndSpawnPointCount = int.Parse(Console.ReadLine()); // usefrul from wood1, represents the number of bushes and the number of places where neutral units can spawn
            for (var i = 0; i < bushAndSpawnPointCount; i++)
            {
                inputs = Console.ReadLine().Split(' ').ToList();
                state.Entities.Add(new Entity
                {
                    EntityType = inputs[0],// BUSH, from wood1 it can also be SPAWN
                    X = int.Parse(inputs[1]),
                    Y = int.Parse(inputs[2]),
                    Radius = int.Parse(inputs[3])
                });
            }

            var itemCount = int.Parse(Console.ReadLine()); // useful from wood2
            for (var i = 0; i < itemCount; i++)
            {
                inputs = Console.ReadLine().Split(' ').ToList();
                state.Items.Add(new Item
                {
                    ItemName = inputs[0], // contains keywords such as BRONZE, SILVER and BLADE, BOOTS connected by "_" to help you sort easier
                    ItemCost = int.Parse(inputs[1]), // BRONZE items have lowest cost, the most expensive items are LEGENDARY
                    Damage = int.Parse(inputs[2]), // keyword BLADE is present if the most important item stat is damage
                    Health = int.Parse(inputs[3]),
                    MaxHealth = int.Parse(inputs[4]),
                    Mana = int.Parse(inputs[5]),
                    MaxMana = int.Parse(inputs[6]),
                    MoveSpeed = int.Parse(inputs[7]), // keyword BOOTS is present if the most important item stat is moveSpeed
                    ManaRegeneration = int.Parse(inputs[8]),
                    IsPotion = int.Parse(inputs[9]), // 0 if it's not instantly consumed
                });
            }
            return state;
        }
    }
    #endregion

}