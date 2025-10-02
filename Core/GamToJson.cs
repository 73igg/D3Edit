using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using D3Edit.Filetypes.Gam;

namespace D3Edit.Core
{
    public static class GamToJson
    {
        public static int Convert(string inPath, string outPath)
        {
            if (!File.Exists(inPath))
                throw new FileNotFoundException("Input file not found.", inPath);

            string name = Path.GetFileName(inPath);

            if (Contains(name, "experiencetablealt"))
            {
                var f = ExperienceAltTableIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(f, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: ExperienceTableAlt -> {CountSafe.Of(f)} records");
                return 0;
            }

            if (Contains(name, "experiencetable"))
            {
                var f = ExperienceTableIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(f, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: ExperienceTable -> {CountSafe.Of(f)} records");
                return 0;
            }

            if (Contains(name, "characters"))
            {
                var chr = CharactersIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(chr, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: Characters -> {CountSafe.Of(chr)} records");
                return 0;
            }

            if (Contains(name, "affixlist"))
            {
                var (header, recs) = AffixListReader.ReadAll(inPath);
                var obj = new AffixJsonFile { Header = header, Records = recs };
                var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) } };
                File.WriteAllText(outPath, System.Text.Json.JsonSerializer.Serialize(obj, opts), new UTF8Encoding(false));
                Console.WriteLine($"OK: AffixList -> {recs.Count} records");
                return 0;
            }

            if (Contains(name, "items"))
            {
                var game = ItemsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(game, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: Items -> {game.Items?.Count ?? 0} records");
                return 0;
            }

            if (Contains(name, "currency"))
            {
                var cur = CurrencyIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(cur, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: Currency -> {CountSafe.Of(cur)} records");
                return 0;
            }

            if (Contains(name, "handicap"))
            {
                var h = HandicapLevelsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(h, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: HandicapLevels -> {CountSafe.Of(h)} records");
                return 0;
            }

            if (Contains(name, "hireling"))
            {
                var h = HirelingsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(h, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: Hirelings -> {CountSafe.Of(h)} records");
                return 0;
            }

            if (Contains(name, "itemsalvage"))
            {
                var s = ItemSalvageLevelsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(s, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: ItemSalvageLevels -> {CountSafe.Of(s)} records");
                return 0;
            }

            if (Contains(name, "itemtypes"))
            {
                var t = ItemTypesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(t, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: ItemTypes -> {CountSafe.Of(t)} records");
                return 0;
            }

            if (Contains(name, "label"))
            {
                var lbl = LabelGBIDsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(lbl, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: Labels -> {CountSafe.Of(lbl)} records");
                return 0;
            }

            if (Contains(name, "monsteraffix"))
            {
                var m = MonsterAffixesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(m, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: MonsterAffixes -> {CountSafe.Of(m)} records");
                return 0;
            }

            if (Contains(name, "monsterlevel"))
            {
                var ml = MonsterLevelsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(ml, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: MonsterLevels -> {CountSafe.Of(ml)} records");
                return 0;
            }

            if (Contains(name, "monsternames"))
            {
                var n = MonsterNamesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(n, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: MonsterNames -> {CountSafe.Of(n)} records");
                return 0;
            }

            if (Contains(name, "paragon"))
            {
                var p = ParagonBonusesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(p, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: ParagonBonuses -> {CountSafe.Of(p)} records");
                return 0;
            }

            if (Contains(name, "powerformula"))
            {
                var pf = PowerFormulaTablesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(pf, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: PowerFormulaTables -> {CountSafe.Of(pf)} records");
                return 0;
            }

            if (Contains(name, "rarename") || Contains(name, "rareitem"))
            {
                var r = RareItemNamesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(r, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: RareItemNames -> {CountSafe.Of(r)} records");
                return 0;
            }

            if (Contains(name, "transmute"))
            {
                var tr = TransmuteRecipesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(tr, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: TransmuteRecipes -> {CountSafe.Of(tr)} records");
                return 0;
            }

            if (Contains(name, "recipe"))
            {
                var rec = RecipesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(rec, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: Recipes -> {CountSafe.Of(rec)} records");
                return 0;
            }

            if (Contains(name, "setitem"))
            {
                var rec = SetItemBonusesIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(rec, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: SetItemBonuses -> {CountSafe.Of(rec)} records");
                return 0;
            }

            if (Contains(name, "socketed"))
            {
                var rec = SocketedEffectsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(rec, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: SocketedEffects -> {CountSafe.Of(rec)} records");
                return 0;
            }

            if (Contains(name, "enchant"))
            {
                var f = EnchantCostScalars.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(f, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: EnchantCostScalars -> {CountSafe.Of(f)} records");
                return 0;
            }

            if (Contains(name, "legacy"))
            {
                var lc = LegacyItemConversionsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(lc, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: LegacyItemConversions -> {CountSafe.Of(lc)} records");
                return 0;
            }

            if (Contains(name, "tieredloot") || Contains(name, "lootrun"))
            {
                var tl = TieredLootRunLevelsIO.ReadGamFile(inPath);
                File.WriteAllText(outPath, JsonConvert.SerializeObject(tl, Formatting.Indented), new UTF8Encoding(false));
                Console.WriteLine($"OK: TieredLootRunLevels -> {CountSafe.Of(tl)} records");
                return 0;
            }

            throw new InvalidDataException("Make sure the filename contains the actual name of the gam file you would like to parse.");
        }

        private static bool Contains(string fileName, string token) =>
            fileName?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
