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
                var returned = scoredTactic.Strategy.RetrieveActions(this, turn, scoredTactic);
                foreach (var a in returned)
                {
                    ActionBuffer.Enqueue(a);
                }
            }

            if (ActionBuffer.Count > 0)
            {
                ActionBuffer.Dequeue()();
            }
            else
            {
                Wait();
            }
        }

        public void Spawn() => RawWrite(_heroType.ToString().ToUpper());
        public void Wait() => RawWrite("WAIT");
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

    public class BuffHealthIfMelee : IStrategy
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

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var healingBuff = turn.Game.Items
                .Where(x => x.IsPotion == 0 && x.Health > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            foreach (var item in healingBuff)
            {
                yield return () => controller.Buy(item.ItemName, "Ho ho ho.");
            }
        }
    }


    public class MoveWithFrontLine : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.HealthPercentage < 5)
            {
                return TacticScore.DoNotUse;
            }

            var frontLineNearTower = turn.My.Trash.UnitsInRangeOf(turn.Enemy.Tower).Any();
            if (frontLineNearTower && turn.My.Hero.IsMelee)
            {
                return TacticScore.DoNotUse;
            }

            if (!turn.Enemy.UnitsInRangeOf(turn.My.Hero).Any())
            {
                return new TacticScore(this, 1500, "Nothing in range, moving with front line.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var frontLine = turn.My.Trash.FurthestForwards(x => x.X, turn.Game.MyTeam);
            var leader = turn.My.Trash.FirstOrDefault(x => x.X == frontLine) ?? turn.My.Tower;

            yield return () => controller.Move(leader.X, leader.Y, "March!");
        }
    }

    public class IgnoreCampersIfMelee : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var pool = turn.Enemy.Except(turn.Enemy.UnitsInRangeOf(turn.Enemy.Tower));

            if (turn.Enemy.Hero.NearTo(turn.Enemy.Tower) && !turn.Enemy.Hero.IsRanged && !pool.Any())
            {
                return new TacticScore(this, 54, "Enemy is camping, avoid the tower.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            yield return () => controller.AttackNearest("HERO");
        }
    }

    public class FleeWhenWeak : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.HealthPercentage < 20 && !turn.My.Hero.NearTo(turn.My.Tower))
            {
                return new TacticScore(this, 1100, "Health less than 20%");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var tower = turn.My.First(u => u.UnitType == UnitType.Tower);

            yield return () => controller.Move(tower.X, tower.Y, "Ouch!");
        }
    }

    public class HealIfPossible: IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var healingPotions = turn.Game.Items
                .Where(x => x.IsPotion == 1 && x.Health > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            if (turn.My.Hero.HealthPercentage < 50 && healingPotions.Any())
            {
                return new TacticScore(this, 1115, "Health less than 50%");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var healingPotions = turn.Game.Items
                .Where(x => x.IsPotion == 1 && x.Health > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            yield return () => controller.Buy(healingPotions.First().ItemName, "Ahh that's better. Come on!");
        }
    }

    public class DenyNearbyUnits : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var myVunerableUnits =
                turn.My.Trash.UnitsInRangeOf(turn.My.Hero)
                    .Where(u => u.Health <= turn.My.Hero.AttackDamage)
                    .ToUnitCollection();

            if (myVunerableUnits.Count > 0)
            {
                return new TacticScore(this, 1501, "Vunerable units within reach - murder them to deny gold");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var myVunerableUnits =
                turn.My.UnitsInRangeOf(turn.My.Hero)
                    .Where(u => u.Health <= turn.My.Hero.AttackDamage)
                    .OrderBy(u => u.Health)
                    .ToUnitCollection();

            yield return () => controller.Attack(myVunerableUnits.First(), "No You Don't");
        }
    }

    public class HideInTowerWhenICantDecidedWhatToDo : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            return new TacticScore(this, 1, "I'm scared.");
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            if (turn.My.Tower.X != turn.My.Hero.X || turn.My.Tower.Y != turn.My.Hero.Y)
            {
                yield return () => controller.Move(turn.My.Tower.X, turn.My.Tower.Y);
            }
            else
            {
                yield return controller.Wait;
            }
        }
    }

    public class AttackHeroNearTowerOnlyOnceEveryoneIsDead : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.Enemy.Tower.NearTo(turn.My.Hero)
                && !turn.Enemy.Trash.Any())
            {
                return new TacticScore(this, 1225, "Kamakazi!");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            yield return () => controller.AttackNearest("HERO");
        }
    }


    public class AttackNearbyEnemiesBasedOnThreat : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.HealthPercentage < 20)
            {
                return TacticScore.DoNotUse;
            }

            var target = turn.Enemy.UnitsInRangeOf(turn.My.Hero);
            if (target.Any())
            {
                return new TacticScore(this, 65, "Attack close enemies.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var target = turn.Enemy.UnitsInRangeOf(turn.My.Hero).Except(turn.Enemy.UnitsInRangeOf(turn.Enemy.Tower));

            if (target.Any())
            {
                var threat = new ThreatTable(target);
                var sorted = threat.OrderBy(x => x.Value);
                var keyValuePair = sorted.Last();
                var unit = keyValuePair.Key;

                yield return () => controller.Attack(unit, "I see you.");
            }
            else
            {
                yield return controller.Wait;
            }
        }
    }

    public class AvoidBeingKited : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (!turn.My.Trash.Any())
            {
                return TacticScore.DoNotUse;
            }

            var backOfMyPackX = turn.My.Trash.Min(x => x.X);
            var distanceFromPack = Math.Abs(turn.My.Hero.X - backOfMyPackX);

            if (turn.Enemy.Hero.IsRanged 
                && turn.Enemy.Hero.CanAttack(turn.My.Hero)
                && distanceFromPack > (turn.My.Hero.MovementSpeed * 4))
            {
                return new TacticScore(this, 56, "I think I'm being kited, let's not do that.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var backline = turn.My.Trash.FurthestBackwards(x => x.X, turn.Game.MyTeam);
            var trail = turn.My.Trash.FirstOrDefault(x => x.X == backline) ?? turn.My.Tower;

            yield return () => controller.Move(trail.X, trail.Y, "Nice try, you come here.");
        }
    }

    public class AttackHero : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.Enemy.Tower.CanAttack(turn.My.Hero) && turn.Enemy.Hero.HealthPercentage > 30)
            {
                return TacticScore.DoNotUse; // Prefer tower attack when being t on.
            }

            if (turn.Enemy.Hero.HealthPercentage <= 20)
            {
                return new TacticScore(this, int.MaxValue, "Nuke hero, they're weak.");
            }

            if (turn.Enemy.Hero.CanAttack(turn.My.Hero) 
                && turn.My.Hero.HealthPercentage > 80)
            {
                return new TacticScore(this, 55, "Hero is ranged and can attack me, try rush him");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            if (turn.Enemy.Hero.HealthPercentage > 30)
            {
                yield return () => controller.AttackNearest("HERO", "FIGHT ME.");
            }
            else
            {
                yield return () => controller.AttackNearest("HERO", "You die now.");
            }
        }
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
                    threat *= 2;
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
            return this.Where(u => other.CanAttack(u));
        }

        public Unit Tower => this.SingleOrDefault(x => x.UnitType == UnitType.Tower);
        public Unit Hero => this.SingleOrDefault(x => x.UnitType == UnitType.Hero);
        public UnitCollection Trash => this.Where(x => x.UnitType != UnitType.Hero && x.UnitType != UnitType.Tower && x.UnitType != UnitType.Groot).ToUnitCollection();
    }

    public static class UnitColExtensions
    {
        public static UnitCollection ToUnitCollection(this IEnumerable<Unit> src) => new UnitCollection(src);
        public static int FurthestForwards(this IEnumerable<Unit> source, Func<Unit, int> selector, int team)
        {
            return team == 0 ? source.Select(selector).Max() : source.Select(selector).Min();
        }

        public static int FurthestBackwards(this IEnumerable<Unit> source, Func<Unit, int> selector, int team)
        {
            return team == 0 ? source.Select(selector).Min() : source.Select(selector).Max();
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
            var smallestX = X - MovementSpeed * 2;
            var largestX = X + MovementSpeed * 2;
            var smallestY = Y - MovementSpeed * 2;
            var largestY = Y + MovementSpeed * 2;

            if (x >= smallestX && x <= largestX
                && y >= smallestY && y <= largestY)
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
        IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore);
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