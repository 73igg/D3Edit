using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using D3Edit.Filetypes.Gam;

namespace D3Edit.Core
{
    public static class JsonToGam
    {
        public static int Convert(string inPath, string outPath)
        {
            if (!File.Exists(inPath))
                throw new FileNotFoundException("Input file not found.", inPath);

            string outName = Path.GetFileName(outPath);
            string json = File.ReadAllText(inPath, Encoding.UTF8);

            if (Contains(outName, "experiencetablealt"))
            {
                var f = JsonConvert.DeserializeObject<ExperienceAltTableJsonFile>(json)
                        ?? throw new InvalidDataException("ExperienceTableAlt JSON deserialize failed.");
                ExperienceAltTableIO.WriteGamFile(outPath, f);
                Console.WriteLine($"OK: ExperienceTableAlt -> {CountSafe.Of(f)} records");
                return 0;
            }
            if (Contains(outName, "experiencetable"))
            {
                var f = JsonConvert.DeserializeObject<ExperienceTableJsonFile>(json)
                        ?? throw new InvalidDataException("ExperienceTable JSON deserialize failed.");
                ExperienceTableIO.WriteGamFile(outPath, f);
                Console.WriteLine($"OK: ExperienceTable -> {CountSafe.Of(f)} records");
                return 0;
            }

            if (Contains(outName, "characters"))
            {
                var chr = JsonConvert.DeserializeObject<CharactersJsonFile>(json)
                          ?? throw new InvalidDataException("Characters JSON deserialize failed.");
                CharactersIO.WriteGamFile(outPath, chr);
                Console.WriteLine($"OK: Characters -> {CountSafe.Of(chr)} records");
                return 0;
            }

            if (Contains(outName, "affixlist"))
            {
                var opts = new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };

                if (json.TrimStart().StartsWith("["))
                {
                    var recs = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<AffixRecord>>(json, opts)
                               ?? new System.Collections.Generic.List<AffixRecord>();
                    AffixListWriter.WriteAll(outPath, Header.Default(), recs);
                    Console.WriteLine($"OK: AffixList -> {recs.Count} records");
                    return 0;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("AffixList JSON must be array or object.");

                Header header = Header.Default();
                if (TryGetCaseInsensitive(root, "header", out var headerProp))
                    header = headerProp.Deserialize<Header>(opts) ?? Header.Default();

                if (!TryGetCaseInsensitive(root, "records", out var recordsProp))
                    throw new InvalidDataException("AffixList object JSON must contain 'records'.");

                var recsObj = recordsProp.Deserialize<System.Collections.Generic.List<AffixRecord>>(opts)
                              ?? new System.Collections.Generic.List<AffixRecord>();
                AffixListWriter.WriteAll(outPath, header, recsObj);
                Console.WriteLine($"OK: AffixList -> {recsObj.Count} records");
                return 0;
            }

            if (Contains(outName, "items"))
            {
                var game = JsonConvert.DeserializeObject<GameFileCollection>(json)
                           ?? throw new InvalidDataException("Items JSON deserialize failed.");
                ItemsIO.WriteGamFile(outPath, game);
                Console.WriteLine($"OK: Items -> {game.Items?.Count ?? 0} records");
                return 0;
            }

            if (Contains(outName, "currency"))
            {
                var cur = JsonConvert.DeserializeObject<CurrencyJsonFile>(json)
                          ?? throw new InvalidDataException("Currency JSON deserialize failed.");
                CurrencyIO.WriteGamFile(outPath, cur);
                Console.WriteLine($"OK: Currency -> {CountSafe.Of(cur)} records");
                return 0;
            }

            if (Contains(outName, "handicap"))
            {
                var h = JsonConvert.DeserializeObject<HandicapLevelsJsonFile>(json)
                        ?? throw new InvalidDataException("HandicapLevels JSON deserialize failed.");
                HandicapLevelsIO.WriteGamFile(outPath, h);
                Console.WriteLine($"OK: HandicapLevels -> {CountSafe.Of(h)} records");
                return 0;
            }

            if (Contains(outName, "hireling"))
            {
                var h = JsonConvert.DeserializeObject<HirelingsJsonFile>(json)
                        ?? throw new InvalidDataException("Hirelings JSON deserialize failed.");
                HirelingsIO.WriteGamFile(outPath, h);
                Console.WriteLine($"OK: Hirelings -> {CountSafe.Of(h)} records");
                return 0;
            }

            if (Contains(outName, "itemsalvage"))
            {
                var s = JsonConvert.DeserializeObject<ItemSalvageLevelsJsonFile>(json)
                        ?? throw new InvalidDataException("ItemSalvageLevels JSON deserialize failed.");
                ItemSalvageLevelsIO.WriteGamFile(outPath, s);
                Console.WriteLine($"OK: ItemSalvageLevels -> {CountSafe.Of(s)} records");
                return 0;
            }

            if (Contains(outName, "itemtypes"))
            {
                var t = JsonConvert.DeserializeObject<ItemTypesJsonFile>(json)
                        ?? throw new InvalidDataException("ItemTypes JSON deserialize failed.");
                ItemTypesIO.WriteGamFile(outPath, t);
                Console.WriteLine($"OK: ItemTypes -> {CountSafe.Of(t)} records");
                return 0;
            }

            if (Contains(outName, "label"))
            {
                var lbl = JsonConvert.DeserializeObject<LabelGBIDsJsonFile>(json)
                          ?? throw new InvalidDataException("Labels JSON deserialize failed.");
                LabelGBIDsIO.WriteGamFile(outPath, lbl);
                Console.WriteLine($"OK: Labels -> {CountSafe.Of(lbl)} records");
                return 0;
            }

            if (Contains(outName, "monsteraffix"))
            {
                var m = JsonConvert.DeserializeObject<MonsterAffixesJsonFile>(json)
                        ?? throw new InvalidDataException("MonsterAffixes JSON deserialize failed.");
                MonsterAffixesIO.WriteGamFile(outPath, m);
                Console.WriteLine($"OK: MonsterAffixes -> {CountSafe.Of(m)} records");
                return 0;
            }

            if (Contains(outName, "monsterlevel"))
            {
                var ml = JsonConvert.DeserializeObject<MonsterLevelsJsonFile>(json)
                         ?? throw new InvalidDataException("MonsterLevels JSON deserialize failed.");
                MonsterLevelsIO.WriteGamFile(outPath, ml);
                Console.WriteLine($"OK: MonsterLevels -> {CountSafe.Of(ml)} records");
                return 0;
            }

            if (Contains(outName, "monsternames"))
            {
                var n = JsonConvert.DeserializeObject<MonsterNamesJsonFile>(json)
                        ?? throw new InvalidDataException("MonsterNames JSON deserialize failed.");
                MonsterNamesIO.WriteGamFile(outPath, n);
                Console.WriteLine($"OK: MonsterNames -> {CountSafe.Of(n)} records");
                return 0;
            }

            if (Contains(outName, "paragon"))
            {
                var p = JsonConvert.DeserializeObject<ParagonBonusesJsonFile>(json)
                        ?? throw new InvalidDataException("ParagonBonuses JSON deserialize failed.");
                ParagonBonusesIO.WriteGamFile(outPath, p);
                Console.WriteLine($"OK: ParagonBonuses -> {CountSafe.Of(p)} records");
                return 0;
            }

            if (Contains(outName, "powerformula"))
            {
                var pf = JsonConvert.DeserializeObject<PowerFormulaTablesJsonFile>(json)
                         ?? throw new InvalidDataException("PowerFormulaTables JSON deserialize failed.");
                PowerFormulaTablesIO.WriteGamFile(outPath, pf);
                Console.WriteLine($"OK: PowerFormulaTables -> {CountSafe.Of(pf)} records");
                return 0;
            }

            if (Contains(outName, "rarename") || Contains(outName, "rareitem"))
            {
                var r = JsonConvert.DeserializeObject<RareItemNamesJsonFile>(json)
                        ?? throw new InvalidDataException("RareItemNames JSON deserialize failed.");
                RareItemNamesIO.WriteGamFile(outPath, r);
                Console.WriteLine($"OK: RareItemNames -> {CountSafe.Of(r)} records");
                return 0;
            }

            if (Contains(outName, "transmute"))
            {
                var tr = JsonConvert.DeserializeObject<TransmuteRecipesJsonFile>(json)
                         ?? throw new InvalidDataException("TransmuteRecipes JSON deserialize failed.");
                TransmuteRecipesIO.WriteGamFile(outPath, tr);
                Console.WriteLine($"OK: TransmuteRecipes -> {CountSafe.Of(tr)} records");
                return 0;
            }

            if (Contains(outName, "recipe"))
            {
                var rec = JsonConvert.DeserializeObject<RecipesJsonFile>(json)
                          ?? throw new InvalidDataException("Recipes JSON deserialize failed.");
                RecipesIO.WriteGamFile(outPath, rec);
                Console.WriteLine($"OK: Recipes -> {CountSafe.Of(rec)} records");
                return 0;
            }

            if (Contains(outName, "setitem"))
            {
                var rec = JsonConvert.DeserializeObject<SetItemBonusesJsonFile>(json)
                          ?? throw new InvalidDataException("SetItemBonuses JSON deserialize failed.");
                SetItemBonusesIO.WriteGamFile(outPath, rec);
                Console.WriteLine($"OK: SetItemBonuses -> {CountSafe.Of(rec)} records");
                return 0;
            }

            if (Contains(outName, "socketed"))
            {
                var rec = JsonConvert.DeserializeObject<SocketedEffectsJsonFile>(json)
                          ?? throw new InvalidDataException("SocketedEffects JSON deserialize failed.");
                SocketedEffectsIO.WriteGamFile(outPath, rec);
                Console.WriteLine($"OK: SocketedEffects-> {CountSafe.Of(rec)} records");
                return 0;
            }

            if (Contains(outName, "enchant"))
            {
                var f = JsonConvert.DeserializeObject<EnchantCostScalarsJsonFile>(json)
                        ?? throw new InvalidDataException("EnchantCostScalars JSON deserialize failed.");
                EnchantCostScalars.WriteGamFile(outPath, f);
                Console.WriteLine($"OK: EnchantCostScalars -> {CountSafe.Of(f)} records");
                return 0;
            }

            if (Contains(outName, "legacy"))
            {
                var lc = JsonConvert.DeserializeObject<LegacyItemConversionsJsonFile>(json)
                         ?? throw new InvalidDataException("LegacyItemConversions JSON deserialize failed.");
                LegacyItemConversionsIO.WriteGamFile(outPath, lc);
                Console.WriteLine($"OK: LegacyItemConversions -> {CountSafe.Of(lc)} records");
                return 0;
            }

            if (Contains(outName, "tieredloot") || Contains(outName, "lootrun"))
            {
                var tl = JsonConvert.DeserializeObject<TieredLootRunLevelsJsonFile>(json)
                         ?? throw new InvalidDataException("TieredLootRunLevels JSON deserialize failed.");
                TieredLootRunLevelsIO.WriteGamFile(outPath, tl);
                Console.WriteLine($"OK: TieredLootRunLevels -> {CountSafe.Of(tl)} records");
                return 0;
            }

            throw new InvalidDataException("Output filename must match to choose writer.");
        }

        private static bool Contains(string fileName, string token) =>
            fileName?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool TryGetCaseInsensitive(JsonElement obj, string name, out JsonElement value)
        {
            foreach (var p in obj.EnumerateObject())
            {
                if (p.NameEquals(name) || string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }
}
