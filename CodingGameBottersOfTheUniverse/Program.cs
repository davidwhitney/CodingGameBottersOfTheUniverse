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
            var allTactics = typeof(IStrategy).Assembly.GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IStrategy)))
                .Select(Activator.CreateInstance)
                .Cast<IStrategy>()
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
        public List<IStrategy> Tactics { get; set; }
        public Queue<Action> ActionBuffer { get; set; } = new Queue<Action>();

        private readonly HeroType _heroType;
        private readonly IOutputChannel _output;
        
        public HeroController(HeroType heroType, List<IStrategy> tactics, IOutputChannel output)
        {
            _heroType = heroType;
            _output = output;
            Tactics = tactics;
        }

        public TacticScore SelectTactic(TurnState turn)
        {
            var rank = new Dictionary<IStrategy, TacticScore>();
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
            if (ActionBuffer.Count == 0)
            {
                var scoredTactic = SelectTactic(turn);
                _output.Debug($"{scoredTactic.Strategy.GetType().Name}, because {scoredTactic.Reason}");
                var returned = scoredTactic.Strategy.Do(this, turn, scoredTactic);
                foreach (var a in returned)
                {
                    ActionBuffer.Enqueue(a);
                }
            }

            if (ActionBuffer.Count > 0)
            {
                ActionBuffer.Dequeue()();
            }
        }

        public void Spawn() => RawWrite(_heroType.ToString().ToUpper());
        public void Wait() => RawWrite("WAIT");

        public void MoveBehind(Unit unit, int buffer = 0, string customMessage = "")
        {
            buffer *= Unit.Team == 0 ? -1 : 1;
            Move(unit.X + buffer, unit.Y, customMessage);
        }

        public void Move(Unit unit, string customMessage = "") => Move(unit.X, unit.Y, customMessage);
        public void Move(int x, int y, string customMessage = "") => RawWrite($"MOVE {x} {y}", customMessage);
        public void MoveAttack(int x, int y, int unitId, string customMessage = "") => RawWrite($"MOVE_ATTACK {x} {y} {unitId}", customMessage);
        public void MoveAttack(int x, int y, Unit unit, string customMessage = "") => RawWrite($"MOVE_ATTACK {x} {y} {unit.Id}", customMessage);
        public void Attack(int unitId, string customMessage = "") => RawWrite($"ATTACK {unitId}", customMessage);
        public void Attack(Unit unit, string customMessage = "") => RawWrite($"ATTACK {unit.Id}", customMessage);
        public void AttackNearest(string unitType, string customMessage = "") => RawWrite($"ATTACK_NEAREST {unitType}", customMessage);
        public int TimeToMove(int distance, string customMessage = "") => distance / Unit.MovementSpeed;
        public void Buy(string item, string customMessage = "") => RawWrite($"BUY {item}", customMessage);

        private void RawWrite(string command, string customMessage = null)
            => _output.WriteLine(!string.IsNullOrWhiteSpace(customMessage) ? string.Join(";", command, customMessage) : command);
    }
    
    #region Domain

    public class ThreatTable : Dictionary<Unit, int>
    {
        public ThreatTable(IEnumerable<Unit> units)
        {
            foreach (var enemy in units)
            {
                var threat = (int)((double)enemy.HealthPercentage / ((double)enemy.AttackDamage + (double)enemy.AttackRange*5) * 100);

                if (enemy.HeroType != "-")
                {
                    threat *= 5;
                }

                if (enemy.HealthPercentage < 50)
                {
                    threat *= 2;
                }

                Add(enemy, threat);
            }
        }
    }

    public class TacticScore
    {
        public IStrategy Strategy { get; }
        public int Score { get; }
        public string Reason { get; }

        public TacticScore(IStrategy strategy, int score, string reason)
        {
            Strategy = strategy;
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
        public int TurnNumber { get; set; }
    }

    public class TurnState
    {
        public GameState Game { get; set; }
        public int EntityCount { get; set; }
        public int Gold { get; set; }
        public int GoldOfEnemy { get; set; }
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
            return this.Where(u => other.CanAttack(u));
        }

        public Unit Tower => this.SingleOrDefault(x => x.UnitType == UnitType.Tower);
        public Unit Hero => this.SingleOrDefault(x => x.UnitType == UnitType.Hero);
        public UnitCollection Trash => this.Where(x => x.UnitType != UnitType.Hero && x.UnitType != UnitType.Tower && x.UnitType != UnitType.Groot).ToUnitCollection();
    }

    public static class UnitColExtensions
    {
        public static UnitCollection ToUnitCollection(this IEnumerable<Unit> src) => new UnitCollection(src);
        public static Unit FurthestForwardsOrDefault(this IEnumerable<Unit> source)
        {
            var enumerable = source.Select(x => x.X).ToList();
            if (!enumerable.Any()) return null;

            var team = source.First().Team;
            var xPosOfFrontLine = team == 0 ? enumerable.Max() : enumerable.Min();
            return source.FirstOrDefault(x => x.X == xPosOfFrontLine);
        }

        public static Unit FurthestBackwardsOrDefault(this IEnumerable<Unit> source)
        {
            var enumerable = source.Select(x => x.X).ToList();
            if (!enumerable.Any()) return null;

            var team = source.First().Team;
            var xPosOfBackline = team == 0 ? enumerable.Min() : enumerable.Max();
            return source.FirstOrDefault(x => x.X == xPosOfBackline);
        }
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
        public bool IsMelee => AttackRange <= 150;
        public bool IsCoveredBy(Unit tower) => NearTo(tower) && !IsRanged;

        public bool NearTo(Unit other) => NearTo(other.X, other.Y);
        public bool NearTo(int x, int y)
        {
            var smallestX = X - (MovementSpeed * 1.50);
            var largestX = X + (MovementSpeed * 1.50);
            var smallestY = Y - (MovementSpeed * 1.50);
            var largestY = Y + (MovementSpeed * 1.50);

            if (x >= smallestX && x <= largestX
                && y >= smallestY && y <= largestY)
            {
                return true;
            }

            return false;
        }

        public bool IsBehind(Unit other) => !IsInFrontOf(other.X, other.Y);
        public bool IsBehind(int x, int y) => !IsInFrontOf(x, y);
        public bool IsInFrontOf(Unit other) => IsInFrontOf(other.X, other.Y);

        public bool IsInFrontOf(int x, int y)
        {
            if (Team == 0 && X >= x)
            {
                return true;
            }

            if (Team == 1 && X <= x)
            {
                return true;
            }

            return false;
        }

        public bool CanAttack(Unit other, bool buffer = false) => CanAttack(other.X, other.Y, buffer);
        public bool CanAttack(int x, int y, bool buffer = false)
        {
            var extra = buffer ? 30 : 0;

            var smallestX = X - (AttackRange + extra);
            var largestX = X + (AttackRange + extra);
            var smallestY = Y - (AttackRange + extra);
            var largestY = Y + (AttackRange + extra);

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

    public interface IStrategy
    {
        TacticScore RankTactic(TurnState turn);
        IEnumerable<Action> Do(HeroController controller, TurnState turn, TacticScore tacticScore);
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
            game.TurnNumber++;
            var turnState = new TurnState
            {
                Gold = int.Parse(Console.ReadLine()),
                GoldOfEnemy = int.Parse(Console.ReadLine()),
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

    public class Wait : IStrategy
    {
        public TacticScore RankTactic(TurnState turn) => new TacticScore(this, 0, "This seems unwise.");

        public IEnumerable<Action> Do(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            yield return () => controller.Move(turn.My.Tower);
        }
    }

    public class MoveWithFrontLine : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.Enemy.Tower.CanAttack(turn.My.Hero.X + turn.My.Hero.MovementSpeed, turn.My.Hero.Y))
            {
                return TacticScore.DoNotUse;
            }

            if (turn.My.Hero.HealthPercentage < 20)
            {
                return TacticScore.DoNotUse;
            }
            
            return new TacticScore(this, 1, "Default, move.");
        }

        public IEnumerable<Action> Do(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var leader = turn.My.Trash.FurthestForwardsOrDefault() ?? turn.My.Tower;
            yield return () => controller.MoveBehind(leader, 2, "March!");
        }
    }

    public class AttackNearbyEnemiesBasedOnThreat : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var frontLine = turn.My.Trash.FurthestForwardsOrDefault();
            if (frontLine == null)
            {
                return TacticScore.DoNotUse;
            }

            if (turn.My.Hero.IsInFrontOf(frontLine))
            {
                return TacticScore.DoNotUse;
            }

            var target = turn.Enemy;
            if (target.Any())
            {
                return new TacticScore(this, 10, "Attack close enemies.");
            }
            Console.Error.WriteLine("Nobody in range.");
            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> Do(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var target = turn.Enemy;

            var threat = new ThreatTable(target);
            var sorted = threat.OrderBy(x => x.Value);
            var keyValuePair = sorted.Last();
            var unit = keyValuePair.Key;

            foreach (var item in sorted)
            {
                Console.Error.WriteLine(item.ToDebugString());
            }

            yield return () => controller.Attack(unit, "Smackdown.");

        }
    }
    
    public class AttackHero : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.Enemy.Hero.IsCoveredBy(turn.Enemy.Tower))
            {
                return TacticScore.DoNotUse;
            }

            if (turn.Enemy.Hero.HealthPercentage < 30)
            {
                return new TacticScore(this, 16, "Enemy hero is dying, focus.");
            }

            //if (turn.My.Hero.CanAttack(turn.Enemy.Hero))
            //{
            //    return new TacticScore(this, 16, "Enemy hero is in range.");
            //}

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> Do(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var msg = turn.Enemy.Hero.HealthPercentage > 30 ? "FIGHT ME." : "You die now.";
            yield return () => controller.AttackNearest("HERO", msg);
        }
    }


    public class HealIfPossible : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var healingPotions = turn.Game.Items
                .Where(x => x.IsPotion == 1 && x.Health > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            if (turn.My.Hero.HealthPercentage < 50 && healingPotions.Any())
            {
                return new TacticScore(this, 50, "Health less than 50%");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> Do(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var healingPotions = turn.Game.Items
                .Where(x => x.IsPotion == 1 && x.Health > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            yield return () => controller.Buy(healingPotions.First().ItemName, "Ahh that's better. Come on!");
        }
    }

    public class BuffUp : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            IEnumerable<Item> itemsIWant = turn.My.Hero.IsRanged
                ? turn.Game.Items.Where(x => x.IsPotion == 0 && x.Damage > 0 && x.ItemCost <= turn.Gold).ToList()
                : turn.Game.Items.Where(x => x.IsPotion == 0 && x.Health > 0 && x.ItemCost <= turn.Gold).ToList();

            if (itemsIWant.Any() && turn.My.Hero.ItemsOwned == 0)
            {
                return new TacticScore(this, 2000, "Buff availabile, buying.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> Do(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            IEnumerable<Item> itemsIWant = turn.My.Hero.IsRanged
                ? turn.Game.Items.Where(x => x.IsPotion == 0 && x.Damage > 0 && x.ItemCost <= turn.Gold).ToList()
                : turn.Game.Items.Where(x => x.IsPotion == 0 && x.Health > 0 && x.ItemCost <= turn.Gold).ToList();

            foreach (var item in itemsIWant)
            {
                yield return () => controller.Buy(item.ItemName, "Ho ho ho.");
            }
        }
    }

}