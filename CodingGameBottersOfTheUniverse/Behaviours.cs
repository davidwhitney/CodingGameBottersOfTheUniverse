using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodingGameBottersOfTheUniverse
{

    public class PurchaseMeleeDamageBuffs : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var healthBuff = turn.Game.Items.Where(x => x.IsPotion == 0 && x.Damage > 0 && x.ItemCost <= turn.Gold).ToList();
            if (healthBuff.Any() && turn.My.Hero.ItemsOwned == 0)
            {
                return new TacticScore(this, 2000, "DMG buff availabile, buying.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var healingBuff = turn.Game.Items
                .Where(x => x.IsPotion == 0 && x.Damage > 0 && x.ItemCost <= turn.Gold)
                .OrderByDescending(x => x.Health)
                .ToList();

            foreach (var item in healingBuff)
            {
                yield return () => controller.Buy(item.ItemName, "Ho ho ho.");
            }
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
            yield return () => controller.Move(turn.My.Tower.X, turn.My.Tower.Y, "Ouch!");
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
                return new TacticScore(this, 2500, "Health less than 50%");
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

    public class LashOutWhenConfused : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            return new TacticScore(this, 1, "I'm scared.");
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            yield return () => controller.AttackNearest("UNIT");
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


    public class RetreatIfIBecomeLeader : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var myTrash = turn.My.Trash;
            var inFrontOfAll = myTrash.All(t => turn.My.Hero.IsInFrontOf(t, turn.Game.MyTeam));

            if (inFrontOfAll)
            {
                return new TacticScore(this, 1700, "Hero became the leader. Oops.");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            yield return () => controller.Move(turn.My.Tower.X, turn.My.Hero.Y, "Cover me!");
        }
    }

    public class KeepAtRange : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.My.Hero.IsRanged && turn.My.Hero.AttackRange > turn.Enemy.Hero.AttackRange)
            {
                if (turn.My.Hero.DistanceFrom(turn.Enemy.Hero) <= turn.Enemy.Hero.AttackRange)
                {
                    return new TacticScore(this, 1700, "Move to out-range the hero.");
                }
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            var targetX = (turn.My.Hero.AttackRange - 1) + turn.Enemy.Hero.X;
            var targetY = (turn.My.Hero.AttackRange - 1) + turn.Enemy.Hero.Y;
            yield return () => controller.Move(targetX, targetY, "Back off baby");
        }
    }


    public class TargetCheapHacks : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            var distanceY = Math.Abs(turn.Enemy.Hero.Y - turn.My.Hero.Y);
            if (distanceY > 100)
            {
                return new TacticScore(this, 3000, "Hero is hiding, let's go kill them");
            }

            return TacticScore.DoNotUse;
        }

        public IEnumerable<Action> RetrieveActions(HeroController controller, TurnState turn, TacticScore tacticScore)
        {
            yield return () => controller.Attack(turn.Enemy.Hero, "You can't hide from me");
        }
    }


    public class AttackHero : IStrategy
    {
        public TacticScore RankTactic(TurnState turn)
        {
            if (turn.Enemy.Hero.HealthPercentage <= 20)
            {
                return new TacticScore(this, int.MaxValue, "Nuke hero, they're weak.");
            }

            if (turn.Enemy.Hero.CanAttack(turn.My.Hero))
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
}
