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
                
                hero.Unit = turn.My.Hero;
                hero.ExecuteTactics(turn);
            }
        }
    }
    #endregion

    public class HeroController
    {
        public Unit Unit { get; set; }
        public List<ITactic> Tactics { get; set; }
        public Queue<Action> ActionBuffer { get; set; } = new Queue<Action>();

        private readonly HeroType _heroType;
        private readonly IOutputChannel _output;
        
        public HeroController(HeroType heroType, List<ITactic> tactics, IOutputChannel output)
        {
            _heroType = heroType;
            _output = output;
            Tactics = tactics;
        }

        public TacticScore SelectTactic(TurnState turn)
        {
            var rank = new Dictionary<ITactic, TacticScore>();
            foreach (var item in Tactics)
            {
                var score = item.RankTactic(turn);
                rank.Add(item, score);
            }

            var rankedDictionary = rank.OrderByDescending(x => x.Value.Score);
            return rankedDictionary.First().Value;
        }

        public void ExecuteTactics(TurnState turn)
        {
            if (ActionBuffer.Count <= 0)
            {
                var scoredTactic = SelectTactic(turn);
                _output.Debug($"Executing Tactic: {scoredTactic.Tactic.GetType().Name}, because {scoredTactic.Reason}");
                var returned = scoredTactic.Tactic.DecideWhatToDo(this, turn, scoredTactic);
                foreach (var a in returned)
                {
                    ActionBuffer.Enqueue(a);
                }
            }

            ActionBuffer.Dequeue()();
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
        public void Buy(string item) => _output.WriteLine($"BUY {item}");

    }

    public class BuffHealthIfMelee : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var healthBuff = turn.Game.Items.Where(x => x.IsPotion == 0 && x.Health > 0 && x.ItemCost <= turn.Gold).ToList();
            if (healthBuff.Any() && turn.My.Hero.ItemsOwned == 0)
            {
                return new TacticScore(this, 2000, "Health buff availabile, buying.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var healingBuff = turn.Game.Items
                .Where(x => x.IsPotion == 0 && x.Health > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            yield return () => controller.Buy(healingBuff.First().ItemName);
        }
    }

    public class MoveWithFrontLine : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.HealthPercentage < 15)
            {
                return TacticScore.DoNotUse;
            }

            var frontLineNearTower = turn.My.Trash.UnitsInRangeOf(turn.Enemy.Tower).Any();
            var amMelee = turn.My.Hero.AttackRange < 150;

            if (frontLineNearTower && amMelee)
            {
                return TacticScore.DoNotUse;
            }

            if (!turn.Enemy.UnitsInRangeOf(turn.My.Hero).Any())
            {
                return new TacticScore(this, 1500, "Nothing in range, moving with front line.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var frontLine = turn.My.Trash.Max(x=>x.X);
            var leader = turn.My.Trash.First(x => x.X == frontLine);

            yield return () => controller.Move(leader.X, leader.Y);
        }
    }

    public class IgnoreCampersIfMelee : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.Enemy.Hero.NearTo(turn.Enemy.Tower) && !turn.Enemy.Hero.IsRanged)
            {
                return new TacticScore(this, 54, "Enemy is camping, stay on backline.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var backline = turn.My.Trash.Min(x => x.X);
            var trail = turn.My.Trash.First(x => x.X == backline);
            yield return () => controller.Move(trail.X, trail.Y);
        }
    }

    public class FleeWhenWeak : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.HealthPercentage < 15 && !turn.My.Hero.NearTo(turn.My.Tower))
            {
                return new TacticScore(this, 1100, "Health less than 15%");
            }

            return TacticScore.DoNotUse;
        }
        
        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var tower = turn.My.First(u => u.UnitType == UnitType.Tower);

            yield return () => controller.Move(tower.X, tower.Y);
        }
    }

    public class GoToBackOfPackWhenHurt: ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.HealthPercentage < 15)
            {
                return TacticScore.DoNotUse; // Prefer flee
            }

            if (turn.My.Hero.HealthPercentage < 50)
            {
                return new TacticScore(this, 1110, "Health less than 50%");
            }

            return new TacticScore(this, 1, "Don't know what to do, hug the back of the pack.");
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var backline = turn.My.Trash.Min(x => x.X);
            var trail = turn.My.Trash.First(x => x.X == backline);

            yield return () => controller.Move(trail.X, trail.Y);

            var healingPotions = turn.Game.Items
                .Where(x => x.IsPotion == 1 && x.Health > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            if (healingPotions.Any())
            {
                yield return () => controller.Buy(healingPotions.First().ItemName);
            }
        }
    }

    public class DenyNearbyUnits : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var myVunerableUnits =
                turn.My.UnitsInRangeOf(turn.My.Hero)
                    .Where(u => u.Health <= turn.My.Hero.AttackDamage)
                    .ToUnitCollection();

            if (myVunerableUnits.Count > 0)
            {
                return new TacticScore(this, 200, "Vunerable units within reach - murder them to deny gold");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var myVunerableUnits =
                turn.My.UnitsInRangeOf(turn.My.Hero)
                    .Where(u => u.Health <= turn.My.Hero.AttackDamage)
                    .OrderBy(u => u.Health)
                    .ToUnitCollection();

            yield return () => controller.Attack(myVunerableUnits.First());
        }
    }

    public class AttackNearbyEnemiesBasedOnThreat : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.HealthPercentage < 15)
            {
                return TacticScore.DoNotUse;
            }

            var target = turn.Enemy.UnitsInRangeOf(turn.My.Hero).ToList();
            if (target.Any())
            {
                return new TacticScore(this, 50, "Attack close enemies.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var target = turn.Enemy.UnitsInRangeOf(turn.My.Hero).ToList();

            var threat = new ThreatTable(target);
            var sorted = threat.OrderBy(x => x.Value);
            var keyValuePair = sorted.Last();
            var unit = keyValuePair.Key;
            
            yield return () => controller.Attack(unit);
        }
    }

    public class AvoidBeingKited : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var backOfMyPackX = turn.My.Trash.Min(x => x.X);
            var distanceFromPack = Math.Abs(turn.My.Hero.X - backOfMyPackX);

            if (turn.Enemy.Hero.IsRanged 
                && turn.Enemy.Hero.CanAttack(turn.My.Hero)
                && distanceFromPack > (turn.My.Hero.MovementSpeed * 6))
            {
                return new TacticScore(this, 56, "I think I'm being kited, let's not do that.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var backline = turn.My.Trash.Min(x => x.X);
            var trail = turn.My.Trash.First(x => x.X == backline);

            yield return () => controller.Move(trail.X, trail.Y);
        }
    }

    public class AttackHero : ITactic
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.Enemy.Tower.CanAttack(turn.My.Hero) && turn.Enemy.Hero.HealthPercentage > 30)
            {
                return TacticScore.DoNotUse; // Prefer tower attack when being beat on.
            }

            if (turn.Enemy.Hero.HealthPercentage <= 20)
            {
                return new TacticScore(this, int.MaxValue, "Nuke hero, they're weak.");
            }

            if (turn.Enemy.Hero.IsRanged && turn.Enemy.Hero.CanAttack(turn.My.Hero))
            {
                return new TacticScore(this, 55, "Hero is ranged and can attack me, try rush him");
            }

            if (turn.My.Hero.NearTo(turn.Enemy.Hero))
            {
                return new TacticScore(this, 55, "Hero is near, sod threat tables, get him!");
            }
            
            if (turn.My.Hero.CanAttack(turn.Enemy.Hero))
            {
                return new TacticScore(this, 60, "Hero in range, whack him.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            yield return () => controller.AttackNearest("HERO");
        }
    }

    #region Domain

    public class ThreatTable : Dictionary<Unit, int>
    {
        public ThreatTable(IEnumerable<Unit> units)
        {
            foreach (var enemy in units)
            {
                var threat = (int)((double)enemy.HealthPercentage / (double)enemy.AttackDamage * 100);
                Add(enemy, threat);
            }
        }
    }

    public class TacticScore
    {
        public ITactic Tactic { get; }
        public int Score { get; }
        public string Reason { get; }

        public TacticScore(ITactic tactic, int score, string reason)
        {
            Tactic = tactic;
            Score = score;
            Reason = reason;
        }

        public static TacticScore DoNotUse { get; } = new TacticScore(null, 0, "Do not use");
    }

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
        public void WriteLine(string msg) => Console.WriteLine(msg);
        public void Debug(string msg) => Console.Error.WriteLine(msg);
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
        public UnitCollection Enemy => Units.Where(x => x.Team != Game.MyTeam).ToUnitCollection();
        public UnitCollection My => Units.Where(x => x.Team == Game.MyTeam).ToUnitCollection();

        public override string ToString() => this.ToDebugString();
    }

    public class UnitCollection : List<Unit>
    {
        public UnitCollection(IEnumerable<Unit> units) : base(units)
        {
        }

        public IEnumerable<Unit> UnitsInRangeOf(Unit other)
        {
            return this.Where(other.CanAttack);
        }

        public Unit Tower => this.SingleOrDefault(x => x.UnitType == UnitType.Tower);
        public Unit Hero => this.SingleOrDefault(x => x.UnitType == UnitType.Hero);
        public UnitCollection Trash => this.Where(x => x.UnitType != UnitType.Hero && x.UnitType != UnitType.Tower && x.UnitType != UnitType.Groot).ToUnitCollection();
    }

    public static class UnitColExtensions
    {
        public static UnitCollection ToUnitCollection(this IEnumerable<Unit> src) => new UnitCollection(src);
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

        public int HealthPercentage => (int)((double)Health / (double)MaxHealth * 100);
        public bool IsRanged => AttackRange > 150;

        public bool NearTo(Unit other) => NearTo(other.X, other.Y);
        public bool NearTo(int x, int y)
        {
            var smallestX = X - MovementSpeed * 3;
            var largestX = X + MovementSpeed * 3;
            var smallestY = Y - MovementSpeed * 3;
            var largestY = Y + MovementSpeed * 3;

            if (x >= smallestX && x <= largestX
                && y >= smallestY && y <= largestY)
            {
                return true;
            }

            return false;
        }

        public bool CanAttack(Unit other) => CanAttack(other.X, other.Y);
        public bool CanAttack(int x, int y)
        {
            var smallestX = X - AttackRange;
            var largestX = X + AttackRange;
            var smallestY = Y - AttackRange;
            var largestY = Y + AttackRange;

            if (x >= smallestX && x <= largestX
                && y >= smallestY && y <= largestY)
            {
                return true;
            }

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

        public int DistanceFrom(WorldObject obj) => DistanceFrom(obj.X, obj.Y);
        public int DistanceFrom(int x, int y)
        {
            return (int)Math.Sqrt(Math.Pow((X - x), 2) + Math.Pow((Y - y), 2));
        }
    }

    public interface ITactic
    {
        TacticScore RankTactic(TurnState turn);
        IEnumerable<Action> DecideWhatToDo(HeroController controller, TurnState turn, TacticScore tacticScore);
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