using CoreRCON.Parsers.Standard;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Csgo
{
    public class DamageEvent : IParseable
    {
        public Player Attacked { get; set; }
        public Player Target { get; set; }
        public int Damage { get; set; }
        public int ArmorDamage { get; set; }
        public int PostHealth { get; set; }
        public int PostArmor { get; set; }
        public string HitLocation { get; set; }
    }

    public class DamageEventParser : DefaultParser<DamageEvent>
    {
        //Todo parse position (square bracket content)
        public override string Pattern { get; } = $"(?<Attacker>{playerParser.Pattern}) \\[.*?\\] attacked (?<Target>{playerParser.Pattern}) \\[.*?\\] with \"(?<Weapon>.+?)\" " +
            "\\(damage \"(?<Damage>\\d+)\"\\) " +
            "\\(damage_armor \"(?<ArmorDamage>\\d+)\"\\) " +
            "\\(health \"(?<Health>\\d+)\"\\) " +
            "\\(armor \"(?<Armor>\\d+)\"\\) " +
            "\\(hitgroup \"(?<Hitgroup>.*?)\"\\)";
        private static PlayerParser playerParser { get; } = new PlayerParser();

        public override DamageEvent Load(GroupCollection groups)
        {
            return new DamageEvent
            {
                Target = playerParser.Parse(groups["Target"]),
                Attacked = playerParser.Parse(groups["Attacker"]),
                Damage = int.Parse(groups["Damage"].Value),
                ArmorDamage = int.Parse(groups["ArmorDamage"].Value),
                PostHealth = int.Parse(groups["Health"].Value),
                PostArmor = int.Parse(groups["Armor"].Value),
                HitLocation = groups["Hitgroup"].Value
            };
        }
    }
}